using System.Buffers.Binary;
using System.Text;

namespace RosterGenerator.Core.Legacy;

/// <summary>
/// One entry inside a PS2 memory-card save: the save's own directory, the
/// <c>.</c> and <c>..</c> markers every PS2 directory carries, or a file.
///
/// <para>The 512-byte header is kept exactly as it arrived rather than being
/// rebuilt from parsed fields. Everything in it that this project does not
/// understand — the cluster and directory-entry numbers the card's filesystem
/// used, the attribute word, the two blocks of padding — is somebody's save
/// data, and the only field that has any business changing is the length of a
/// file whose contents changed.</para>
/// </summary>
public sealed class Ps2SaveEntry
{
    /// <summary>Offsets within the 512-byte entry header.</summary>
    private const int ModeOffset = 0x00;
    private const int LengthOffset = 0x04;
    private const int CreatedOffset = 0x08;
    private const int ModifiedOffset = 0x18;
    private const int NameOffset = 0x40;
    private const int NameBytes = 32;

    // sceMcFileAttr*, from the PS2 SDK. A directory carries Subdir and a file
    // carries File; both carry Exists on a live entry.
    private const uint AttrFile = 0x0010;
    private const uint AttrSubdir = 0x0020;

    private readonly byte[] _header;

    internal Ps2SaveEntry(byte[] header, byte[] data)
    {
        _header = header;
        Data = data;
    }

    /// <summary>The entry's mode word, which says what kind of entry it is.</summary>
    public uint Mode => BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(ModeOffset));

    /// <summary>True for a file rather than a directory marker.</summary>
    public bool IsFile => (Mode & AttrFile) != 0;

    /// <summary>True for the save's directory, or its <c>.</c>/<c>..</c> markers.</summary>
    public bool IsDirectory => (Mode & AttrSubdir) != 0;

    /// <summary>
    /// The entry's name. For a directory entry this is the save folder as the
    /// memory card browser shows it — <c>BASLUS-20991</c> and the like.
    /// </summary>
    public string Name
    {
        get
        {
            var span = _header.AsSpan(NameOffset, NameBytes);
            var end = span.IndexOf((byte)0);
            return Encoding.ASCII.GetString(end < 0 ? span : span[..end]);
        }
    }

    /// <summary>
    /// The length field. For a file this is its size in bytes; for the save's
    /// own directory entry it is how many entries follow it.
    /// </summary>
    public int Length => (int)BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(LengthOffset));

    /// <summary>When the save says it was created, or null if unreadable.</summary>
    public DateTime? Created => ReadTimestamp(CreatedOffset);

    /// <summary>When the save says it was last written, or null if unreadable.</summary>
    public DateTime? Modified => ReadTimestamp(ModifiedOffset);

    /// <summary>This entry's contents. Empty for a directory entry.</summary>
    public byte[] Data { get; private set; }

    /// <summary>
    /// Puts new contents in this file, and fixes the one header field that has
    /// to follow — its length.
    /// </summary>
    internal void Replace(byte[] data)
    {
        Data = data;
        BinaryPrimitives.WriteUInt32LittleEndian(_header.AsSpan(LengthOffset), (uint)data.Length);
    }

    internal ReadOnlySpan<byte> Header => _header;

    /// <summary>
    /// A <c>sceMcStDateTime</c>: one reserved byte, then second, minute, hour,
    /// day, month and a little-endian year.
    /// </summary>
    private DateTime? ReadTimestamp(int offset)
    {
        try
        {
            var span = _header.AsSpan(offset, 8);
            return new DateTime(
                BinaryPrimitives.ReadUInt16LittleEndian(span[6..]),
                span[5], span[4], span[3], span[2], span[1], DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A save written by a console with no clock set carries a date
            // that is not a date. Not knowing when it was made is not a
            // reason to refuse to read it.
            return null;
        }
    }
}

/// <summary>
/// A PS2 memory-card save in the <c>.psu</c> form — the format uLaunchELF
/// writes, PS2 Save Builder reads, and the save sites distribute.
///
/// <para>It is a plain archive: a 512-byte entry header for the save's own
/// directory, then one for <c>.</c> and one for <c>..</c>, then a header and
/// its contents for each file, every file's contents padded out to a multiple
/// of 1024 bytes. There is no compression and no encryption anywhere in it,
/// which is why an EA roster file sits inside one exactly as it sits on
/// disk.</para>
///
/// <para><b>Why this exists.</b> The roster tables live in a file inside the
/// save, and until now getting at them meant a third-party editor: export the
/// roster out of a save, edit it, import it back. Both of those steps are this
/// class. Nothing about the roster format changes — the same
/// <see cref="EaDbFile"/> reads the bytes either way.</para>
///
/// <para><b>What it preserves.</b> Every other file in the save comes through
/// byte for byte, and so does every header field bar the length of the file
/// that changed. A memory-card save holds more than a roster — settings,
/// dynasty files, the icon the browser draws — and none of that is this
/// project's business.</para>
/// </summary>
public sealed class Ps2MemoryCardSave
{
    /// <summary>Every entry header is this long.</summary>
    private const int HeaderBytes = 512;

    /// <summary>A file's contents are padded out to a multiple of this.</summary>
    private const int DataAlignment = 1024;

    /// <summary>
    /// No real save is anywhere near this large. It exists so a file that is
    /// not a save cannot talk the reader into allocating a gigabyte.
    /// </summary>
    private const int MostEntries = 4096;

    private Ps2MemoryCardSave(string saveName, List<Ps2SaveEntry> entries)
    {
        SaveName = saveName;
        Entries = entries;
    }

    /// <summary>
    /// The save folder's name, as the memory card browser shows it — for NCAA
    /// Football 2005, <c>BASLUS-20991</c>.
    /// </summary>
    public string SaveName { get; }

    /// <summary>
    /// Every entry, in the order the save holds them: the directory, its two
    /// markers, then the files.
    /// </summary>
    public IReadOnlyList<Ps2SaveEntry> Entries { get; }

    /// <summary>The files in the save, without the directory markers.</summary>
    public IEnumerable<Ps2SaveEntry> Files => Entries.Where(e => e.IsFile);

    /// <summary>
    /// The file in this save holding an EA roster database, or null when the
    /// save has none.
    ///
    /// <para>Found by looking at what each file <em>is</em> rather than by
    /// matching its name. The names differ between games and between the
    /// roster and dynasty saves — <c>BASLUS-20991RNCAA1a</c> against
    /// <c>BASLUS-20991TRos1AA1</c> — and a save whose file happens to be named
    /// something this project has never seen is still a save worth
    /// reading.</para>
    /// </summary>
    public Ps2SaveEntry? RosterFile =>
        Files.FirstOrDefault(e => e.Data.Length > 24 && e.Data[0] == 'D' && e.Data[1] == 'B');

    /// <summary>Puts new contents in one of the save's files.</summary>
    /// <param name="entry">An entry from this save.</param>
    /// <param name="data">Its new contents.</param>
    public void Replace(Ps2SaveEntry entry, byte[] data)
    {
        if (!Entries.Contains(entry))
        {
            throw new ArgumentException("That entry does not belong to this save.", nameof(entry));
        }

        if (!entry.IsFile)
        {
            throw new ArgumentException(
                $"'{entry.Name}' is a directory entry, not a file.", nameof(entry));
        }

        entry.Replace(data);
    }

    /// <summary>The save as bytes, ready to write.</summary>
    public byte[] ToBytes()
    {
        var total = Entries.Sum(e => HeaderBytes + (e.IsFile ? Padded(e.Data.Length) : 0));
        var bytes = new byte[total];
        var at = 0;
        foreach (var entry in Entries)
        {
            entry.Header.CopyTo(bytes.AsSpan(at));
            at += HeaderBytes;
            if (!entry.IsFile)
            {
                continue;
            }

            entry.Data.CopyTo(bytes.AsSpan(at));
            // The padding is left as zeroes rather than carried across from
            // the source. It is slack after the end of a file, the console
            // never reads it, and reproducing whatever happened to be there
            // would mean keeping a copy of the whole save to do it.
            at += Padded(entry.Data.Length);
        }

        return bytes;
    }

    /// <summary>
    /// Writes the save out to a NEW path, refusing to write over the one it
    /// was read from — the same rule <see cref="EaDbFile.Save"/> keeps, and for
    /// the same reason.
    /// </summary>
    public void Save(string path, string? readFrom = null)
    {
        if (readFrom is not null && Path.GetFullPath(path) == Path.GetFullPath(readFrom))
        {
            throw new InvalidOperationException(
                "Writing over the save that was read is refused. Give a different output path.");
        }

        File.WriteAllBytes(path, ToBytes());
    }

    /// <summary>True when the file parses as a <c>.psu</c> save.</summary>
    public static bool LooksLikeSave(string path)
    {
        try
        {
            return TryRead(path, out _);
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

    /// <summary>Reads a save, or answers false rather than throwing.</summary>
    public static bool TryRead(string path, out Ps2MemoryCardSave? save)
    {
        try
        {
            save = Read(path);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            save = null;
            return false;
        }
    }

    /// <summary>Reads a <c>.psu</c> save from disk.</summary>
    public static Ps2MemoryCardSave Read(string path) =>
        Parse(File.ReadAllBytes(path), Path.GetFileName(path));

    /// <summary>Reads a <c>.psu</c> save already held in memory.</summary>
    public static Ps2MemoryCardSave Parse(byte[] bytes, string label = "file")
    {
        if (bytes.Length < HeaderBytes * 3)
        {
            throw new InvalidDataException(
                $"'{label}' is too short to be a PS2 memory-card save.");
        }

        var root = new Ps2SaveEntry(bytes[..HeaderBytes], Array.Empty<byte>());
        if (!root.IsDirectory)
        {
            throw new InvalidDataException(
                $"'{label}' does not start with a save directory, so it is not a .psu save.");
        }

        var count = root.Length;
        if (count is < 2 or > MostEntries)
        {
            throw new InvalidDataException(
                $"'{label}' claims {count} entries, which is not a number a save holds.");
        }

        var entries = new List<Ps2SaveEntry> { root };
        var at = HeaderBytes;
        for (var i = 0; i < count; i++)
        {
            if (at + HeaderBytes > bytes.Length)
            {
                throw new InvalidDataException(
                    $"'{label}' ends in the middle of entry {i + 1} of {count}.");
            }

            var header = bytes[at..(at + HeaderBytes)];
            at += HeaderBytes;

            var entry = new Ps2SaveEntry(header, Array.Empty<byte>());
            if (!entry.IsFile)
            {
                entries.Add(entry);
                continue;
            }

            var length = entry.Length;
            if (length < 0 || at + length > bytes.Length)
            {
                throw new InvalidDataException(
                    $"'{label}' says '{entry.Name}' is {length} bytes, which runs off the end of the file.");
            }

            entries.Add(new Ps2SaveEntry(header, bytes[at..(at + length)]));
            at += Padded(length);
        }

        // A save whose entries do not account for the whole file is not a save
        // this reader understood, and guessing at the remainder is how a tool
        // silently truncates somebody's memory card.
        if (at != bytes.Length)
        {
            throw new InvalidDataException(
                $"'{label}' has {bytes.Length - at} byte(s) after its last entry, so it was not " +
                "read as a .psu save.");
        }

        return new Ps2MemoryCardSave(root.Name, entries);
    }

    private static int Padded(int length) =>
        (length + DataAlignment - 1) / DataAlignment * DataAlignment;
}
