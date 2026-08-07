using System.Buffers.Binary;

namespace RosterGenerator.Core.Legacy;

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

    internal LegacyTable(string name, int recordBytes, IReadOnlyList<LegacyField> fields,
        byte[] data, int offset, int length)
    {
        Name = name;
        RecordBytes = recordBytes;
        Fields = fields;
        _data = data;
        _offset = offset;
        Capacity = recordBytes > 0 ? length / recordBytes : 0;
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

    /// <summary>How many records the allocated space could hold.</summary>
    public int Capacity { get; }

    /// <summary>True when the table has a column of this name.</summary>
    public bool Has(string field) => ByName.ContainsKey(field);

    /// <summary>
    /// Reads one field out of one record.
    ///
    /// <para>A record is a little-endian bit stream: bit <c>n</c> is bit
    /// <c>n % 8</c> of byte <c>n / 8</c>, counted from the least significant
    /// end. Fields are free to straddle byte boundaries and several of them
    /// do.</para>
    /// </summary>
    public int Read(int record, string field)
    {
        if (!ByName.TryGetValue(field, out var column))
        {
            throw new KeyNotFoundException($"Table '{Name}' has no field '{field}'.");
        }

        var start = record * RecordBytes * 8 + column.StartBit;
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
    /// How many records are actually in use.
    ///
    /// <para>The container carries no row count anywhere: the record area is
    /// pre-allocated and the unused tail left blank. So the last record with a
    /// non-zero <paramref name="key"/> ends the table. Checked against the
    /// community exports of two different roster files — 8893/7350/119 and
    /// 4471/3995/83 rows — and exact on all six.</para>
    /// </summary>
    public int CountUsed(string key)
    {
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
/// Reader for the EA <c>DB</c> table container used by PS2-era NCAA Football
/// roster and dynasty saves.
///
/// <para>The layout, all of it confirmed against community CSV exports of two
/// real files rather than assumed:</para>
///
/// <code>
/// header      'DB', u16 version, u32 0, u32 dataSize, u32 0, u32 tableCount, u32 checksum
/// directory   tableCount * (char[4] name, u32 offset relative to the end of the directory)
/// table       48-byte header; [+8] record length in BYTES, [+28] column count,
///             [+44] the bit at which the first named column starts
/// columns     (char[4] name, u32 bits, u32 type, u32 endBitOffset), 16 bytes each --
///             EXCEPT the last, which is truncated to (name, bits) and takes 8
/// records     fixed length, bit-packed, little-endian
/// </code>
///
/// <para>A column's start is its stored <em>end</em> offset minus its width;
/// the last column, having no stored end, starts where the previous one
/// finished. Nine columns across the two tables we read carry stale end
/// offsets that point at the wrong bits — see <see cref="LegacySchema"/>.</para>
/// </summary>
public sealed class EaDbFile
{
    private EaDbFile(IReadOnlyDictionary<string, LegacyTable> tables) => Tables = tables;

    /// <summary>Tables by name.</summary>
    public IReadOnlyDictionary<string, LegacyTable> Tables { get; }

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
                $"'{label}' is not a PS2-era EA roster file (no DB header).");
        }

        var tableCount = ReadU32(data, 16);
        if (tableCount is 0 or > 4096)
        {
            throw new InvalidDataException(
                $"'{label}' claims {tableCount} tables, which is not a number this format holds.");
        }

        var dataStart = 24 + 8 * (int)tableCount;
        var directory = new List<(string Name, int Offset)>();
        for (var i = 0; i < tableCount; i++)
        {
            var name = System.Text.Encoding.ASCII.GetString(data, 24 + 8 * i, 4);
            directory.Add((name, (int)ReadU32(data, 28 + 8 * i)));
        }

        // A table runs until the next one starts, and the last to the end of
        // the file. The directory is not necessarily in offset order.
        var starts = directory.Select(d => d.Offset).OrderBy(o => o).ToList();
        var tables = new Dictionary<string, LegacyTable>(StringComparer.Ordinal);
        foreach (var (name, offset) in directory)
        {
            var next = starts.FirstOrDefault(o => o > offset, data.Length - dataStart);
            tables[name] = ReadTable(data, name, dataStart + offset, dataStart + next, label);
        }

        return new EaDbFile(tables);
    }

    private static LegacyTable ReadTable(byte[] data, string name, int start, int end, string label)
    {
        var recordBytes = (int)ReadU32(data, start + 8);
        var columnCount = (int)ReadU32(data, start + 28);
        var firstBit = (int)ReadU32(data, start + 44);
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
            var fieldName = System.Text.Encoding.ASCII.GetString(data, cursor, 4);
            var bits = (int)ReadU32(data, cursor + 4);

            int startBit;
            if (i == columnCount - 1)
            {
                // The last column stores only its name and width, so it begins
                // where the previous one ended.
                startBit = previousEnd;
                cursor += 8;
            }
            else
            {
                var endBit = (int)ReadU32(data, cursor + 12);
                startBit = endBit - bits;
                previousEnd = endBit;
                cursor += 16;
            }

            fields.Add(LegacySchema.Correct(name, fieldName, startBit, bits));
        }

        return new LegacyTable(name, recordBytes, fields, data, cursor, Math.Max(0, end - cursor));
    }

    private static uint ReadU32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
}
