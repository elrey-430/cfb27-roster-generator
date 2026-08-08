using System.Buffers.Binary;

namespace RosterGenerator.Core.Legacy;

/// <summary>
/// Which way round a legacy file stores its numbers.
/// </summary>
public enum LegacyByteOrder
{
    /// <summary>PS2-era files: little-endian, bit 0 the least significant.</summary>
    Little,

    /// <summary>PS3-era files: big-endian, bit 0 the most significant.</summary>
    Big,
}

/// <summary>
/// One column of a legacy table: where its bits live inside a record.
/// </summary>
/// <param name="Name">The four-character field name, e.g. <c>PGID</c>.</param>
/// <param name="StartBit">First bit of the field, counted from the start of the record.</param>
/// <param name="Bits">Field width in bits.</param>
public sealed record LegacyField(string Name, int StartBit, int Bits);

/// <summary>
/// One table inside a legacy <c>DB</c> file: a fixed-length, bit-packed record
/// array with a named column list.
/// </summary>
public sealed class LegacyTable
{
    private readonly byte[] _data;
    private readonly int _offset;
    private readonly LegacyByteOrder _order;

    internal LegacyTable(string name, int recordBytes, IReadOnlyList<LegacyField> fields,
        byte[] data, int offset, int length, int allocated, int used, LegacyByteOrder order)
    {
        Name = name;
        RecordBytes = recordBytes;
        Fields = fields;
        _data = data;
        _offset = offset;
        _order = order;
        Capacity = recordBytes > 0 ? length / recordBytes : 0;
        Allocated = allocated;
        DeclaredUsed = used;
        ByName = fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
    }

    /// <summary>The four-character table name, e.g. <c>PLAY</c>.</summary>
    public string Name { get; }

    /// <summary>Bytes per record.</summary>
    public int RecordBytes { get; }

    /// <summary>Columns, in the order the file lists them.</summary>
    public IReadOnlyList<LegacyField> Fields { get; }

    /// <summary>Columns by name.</summary>
    public IReadOnlyDictionary<string, LegacyField> ByName { get; }

    /// <summary>How many records the space between this table and the next could hold.</summary>
    public int Capacity { get; }

    /// <summary>Records the table has room for, as the table header states it.</summary>
    public int Allocated { get; }

    /// <summary>Records in use, as the table header states it.</summary>
    public int DeclaredUsed { get; }

    /// <summary>True when the table has a column of this name.</summary>
    public bool Has(string field) => ByName.ContainsKey(field);

    /// <summary>
    /// Reads one field out of one record.
    ///
    /// <para>A record is a bit stream whose direction follows the file's byte
    /// order. On a little-endian file bit <c>n</c> is bit <c>n % 8</c> of byte
    /// <c>n / 8</c> counted from the least significant end; on a big-endian one
    /// it is counted from the most significant. Fields are free to straddle
    /// byte boundaries and many of them do.</para>
    /// </summary>
    public int Read(int record, string field)
    {
        if (!ByName.TryGetValue(field, out var column))
        {
            throw new KeyNotFoundException($"Table '{Name}' has no field '{field}'.");
        }

        var start = record * RecordBytes * 8 + column.StartBit;
        if (_order == LegacyByteOrder.Big)
        {
            var big = 0;
            for (var i = 0; i < column.Bits; i++)
            {
                var p = start + i;
                big = (big << 1) | ((_data[_offset + (p >> 3)] >> (7 - (p & 7))) & 1);
            }

            return big;
        }

        var value = 0;
        for (var taken = 0; taken < column.Bits;)
        {
            var byteIndex = _offset + (start >> 3);
            var bitInByte = start & 7;
            var take = Math.Min(8 - bitInByte, column.Bits - taken);
            var chunk = (_data[byteIndex] >> bitInByte) & ((1 << take) - 1);
            value |= chunk << taken;
            taken += take;
            start += take;
        }

        return value;
    }

    /// <summary>
    /// Reads a field that stores negative numbers in two's complement.
    /// </summary>
    public int ReadSigned(int record, string field)
    {
        var bits = ByName[field].Bits;
        var value = Read(record, field);
        var sign = 1 << (bits - 1);
        return (value & sign) != 0 ? value - (1 << bits) : value;
    }

    /// <summary>
    /// Reads a field holding text as plain bytes, up to the first NUL.
    /// PS3-era files store names this way instead of a character per column.
    /// </summary>
    public string ReadText(int record, string field)
    {
        var column = ByName[field];
        var start = _offset + record * RecordBytes + column.StartBit / 8;
        var text = new System.Text.StringBuilder(column.Bits / 8);
        for (var i = 0; i < column.Bits / 8; i++)
        {
            var c = _data[start + i];
            if (c == 0)
            {
                break;
            }

            text.Append((char)c);
        }

        return text.ToString().Trim();
    }

    /// <summary>
    /// How many records are actually in use.
    ///
    /// <para>The table header states it at +20, as an allocated count followed
    /// by a used one. That was missed on the first pass through this format and
    /// stood in for by scanning back from the end for the last record with a
    /// non-zero key — which agrees with the header on every table of every file
    /// this was built against, but is a guess where the header is a fact. The
    /// scan survives as a fallback for a header that says something
    /// impossible.</para>
    /// </summary>
    public int CountUsed(string key)
    {
        if (DeclaredUsed > 0 && DeclaredUsed <= Capacity)
        {
            return DeclaredUsed;
        }

        for (var i = Capacity - 1; i >= 0; i--)
        {
            if (Read(i, key) != 0)
            {
                return i + 1;
            }
        }

        return 0;
    }
}

/// <summary>
/// Reader for the EA <c>DB</c> table container used by PS2- and PS3-era NCAA
/// Football roster and dynasty saves.
///
/// <para>The layout, confirmed against community CSV exports of two PS2 files
/// and against a PS3 NCAA 14 roster:</para>
///
/// <code>
/// header      'DB', u16 version, u32 flags, u32 dataSize, u32 0, u32 tableCount, u32 checksum
/// directory   tableCount * (char[4] name, u32 offset relative to the end of the directory)
/// table       48-byte header; [+8] record length in BYTES, [+20] allocated then used
///             record counts as two u16, [+28] column count, [+44] the bit at
///             which the first named column starts
/// columns     (char[4] name, u32 bits, u32 type, u32 endBitOffset), 16 bytes each --
///             EXCEPT the last, which is truncated to (name, bits) and takes 8
/// records     fixed length, bit-packed
/// </code>
///
/// <para>A column starts at its stored <em>end</em> offset minus its width; the
/// last column, having no stored end, starts where the previous one finished. A
/// handful of columns carry stale end offsets — see <see cref="LegacySchema"/>.
/// </para>
///
/// <para>PS3 files are the same container written big-endian, and because the
/// four-character table and column codes are stored as integers rather than
/// text their bytes come out reversed: what reads as <c>THCD</c> is
/// <c>DCHT</c>.</para>
/// </summary>
public sealed class EaDbFile
{
    private EaDbFile(IReadOnlyDictionary<string, LegacyTable> tables, LegacyByteOrder order)
    {
        Tables = tables;
        ByteOrder = order;
    }

    /// <summary>Tables by name.</summary>
    public IReadOnlyDictionary<string, LegacyTable> Tables { get; }

    /// <summary>Which way round this file stores its numbers.</summary>
    public LegacyByteOrder ByteOrder { get; }

    /// <summary>True when the file begins with the container's magic bytes.</summary>
    public static bool LooksLikeLegacyFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return stream.Length > 24 && stream.ReadByte() == 'D' && stream.ReadByte() == 'B';
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reads a legacy DB file.</summary>
    public static EaDbFile Read(string path) => Parse(File.ReadAllBytes(path), Path.GetFileName(path));

    /// <summary>Reads a legacy DB file already held in memory.</summary>
    public static EaDbFile Parse(byte[] data, string label = "file")
    {
        if (data.Length < 24 || data[0] != 'D' || data[1] != 'B')
        {
            throw new InvalidDataException(
                $"'{label}' is not a PS2- or PS3-era EA roster file (no DB header).");
        }

        // Which way round the file is written is decided by reading it both
        // ways and keeping the one whose declared size matches the file on
        // disk. A flag byte does differ between the two generations, but a
        // size that agrees to the byte is evidence rather than a guess.
        var order = DetectByteOrder(data, label);

        var tableCount = ReadU32(data, 16, order);
        if (tableCount is 0 or > 4096)
        {
            throw new InvalidDataException(
                $"'{label}' claims {tableCount} tables, which is not a number this format holds.");
        }

        var dataStart = 24 + 8 * (int)tableCount;
        var directory = new List<(string Name, int Offset)>();
        for (var i = 0; i < tableCount; i++)
        {
            directory.Add((ReadCode(data, 24 + 8 * i, order), (int)ReadU32(data, 28 + 8 * i, order)));
        }

        var starts = directory.Select(d => d.Offset).OrderBy(o => o).ToList();
        var tables = new Dictionary<string, LegacyTable>(StringComparer.Ordinal);
        foreach (var (name, offset) in directory)
        {
            var next = starts.FirstOrDefault(o => o > offset, data.Length - dataStart);
            tables[name] = ReadTable(data, name, dataStart + offset, dataStart + next, label, order);
        }

        return new EaDbFile(tables, order);
    }

    private static LegacyByteOrder DetectByteOrder(byte[] data, string label)
    {
        foreach (var order in new[] { LegacyByteOrder.Little, LegacyByteOrder.Big })
        {
            var size = ReadU32(data, 8, order);
            var count = ReadU32(data, 16, order);
            if (count is > 0 and <= 4096 && size <= (uint)data.Length &&
                size >= (uint)data.Length - 64)
            {
                return order;
            }
        }

        // Nothing agreed. The PS2 files are little-endian and far the more
        // common, so that is the assumption, and a table that then reads as
        // nonsense fails with a clearer message than this could give.
        return LegacyByteOrder.Little;
    }

    private static LegacyTable ReadTable(
        byte[] data, string name, int start, int end, string label, LegacyByteOrder order)
    {
        var recordBytes = (int)ReadU32(data, start + 8, order);

        // The column count is one byte in a four-byte slot, so it reads the
        // same either way round -- taking the low byte avoids having to know
        // which of the two spellings a given generation used.
        var columnCount = order == LegacyByteOrder.Big
            ? data[start + 28]
            : (int)ReadU32(data, start + 28, order);
        var allocated = ReadU16(data, start + 20, order);
        var used = ReadU16(data, start + 22, order);
        var firstBit = (int)ReadU32(data, start + 44, order);
        if (recordBytes <= 0 || columnCount <= 0)
        {
            throw new InvalidDataException(
                $"Table '{name}' in '{label}' declares {columnCount} column(s) of {recordBytes} " +
                "byte(s), which cannot be read.");
        }

        var fields = new List<LegacyField>(columnCount);
        var cursor = start + 48;
        var previousEnd = firstBit;
        for (var i = 0; i < columnCount; i++)
        {
            var fieldName = ReadCode(data, cursor, order);
            var bits = (int)ReadU32(data, cursor + 4, order);

            int startBit;
            if (i == columnCount - 1)
            {
                startBit = previousEnd;
                cursor += 8;
            }
            else
            {
                var endBit = (int)ReadU32(data, cursor + 12, order);
                startBit = endBit - bits;
                previousEnd = endBit;
                cursor += 16;
            }

            fields.Add(LegacySchema.Correct(name, fieldName, startBit, bits, order));
        }

        return new LegacyTable(name, recordBytes, fields, data, cursor,
            Math.Max(0, end - cursor), allocated, used, order);
    }

    /// <summary>
    /// A four-character table or column code. It is stored as an integer, so on
    /// a big-endian file its bytes arrive reversed.
    /// </summary>
    private static string ReadCode(byte[] data, int offset, LegacyByteOrder order)
    {
        var span = data.AsSpan(offset, 4);
        Span<char> code = stackalloc char[4];
        for (var i = 0; i < 4; i++)
        {
            code[i] = (char)span[order == LegacyByteOrder.Big ? 3 - i : i];
        }

        return new string(code);
    }

    private static uint ReadU32(byte[] data, int offset, LegacyByteOrder order) =>
        order == LegacyByteOrder.Big
            ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

    private static int ReadU16(byte[] data, int offset, LegacyByteOrder order) =>
        order == LegacyByteOrder.Big
            ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2))
            : BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}
