namespace RosterGenerator.Core.Legacy;

/// <summary>
/// A PS2-era roster, however the user happens to have it: the bare roster file
/// a database editor works on, or still inside the memory-card save it lives in
/// on the console.
///
/// <para><b>Why both.</b> The roster tables are the same bytes either way — a
/// <c>.psu</c> save stores its files uncompressed, so the roster sits inside
/// one exactly as it sits on disk. What differs is only the wrapper, and making
/// the user strip it themselves with a third-party editor is a step that exists
/// for no reason other than that this tool could not do it.</para>
///
/// <para><b>Which one it is, is read off the file.</b> Neither form carries a
/// meaningful extension — a memory-card file has no extension at all — so
/// asking the user to declare it would be asking them to get it wrong. A save
/// announces itself by parsing as one.</para>
///
/// <para><b>You get back what you put in.</b> A save in, a save out; a bare
/// roster file in, a bare roster file out. Turning a bare file into a save
/// would mean inventing the save's directory, its icon and its timestamps —
/// authoring something the user never had rather than editing something they
/// did — so it is not offered. Going the other way is free, and
/// <see cref="SaveDatabaseOnly"/> does it whenever somebody wants the roster
/// on its own.</para>
/// </summary>
public sealed class LegacyRosterSource
{
    private readonly Ps2MemoryCardSave? _save;
    private readonly Ps2SaveEntry? _entry;

    private LegacyRosterSource(EaDbFile database, Ps2MemoryCardSave? save, Ps2SaveEntry? entry)
    {
        Database = database;
        _save = save;
        _entry = entry;
    }

    /// <summary>The roster tables.</summary>
    public EaDbFile Database { get; }

    /// <summary>True when the roster arrived inside a memory-card save.</summary>
    public bool InSave => _save is not null;

    /// <summary>
    /// The save folder's name — <c>BASLUS-20991</c> and the like — or null for
    /// a bare roster file.
    /// </summary>
    public string? SaveName => _save?.SaveName;

    /// <summary>
    /// What the roster is called inside the save, or null for a bare file.
    /// </summary>
    public string? NameInSave => _entry?.Name;

    /// <summary>
    /// How many other files the save carries alongside the roster. These are
    /// written back untouched, and saying how many there are is the honest way
    /// to tell a user their settings and icon came through.
    /// </summary>
    public int OtherFilesInSave => _save is null ? 0 : _save.Files.Count() - 1;

    /// <summary>Opens a roster, in whichever form it is in.</summary>
    /// <param name="path">A <c>.psu</c> memory-card save, or a bare roster file.</param>
    public static LegacyRosterSource Open(string path)
    {
        if (Ps2MemoryCardSave.TryRead(path, out var save) && save is not null)
        {
            var entry = save.RosterFile
                ?? throw new InvalidDataException(
                    $"'{Path.GetFileName(path)}' is a PS2 memory-card save, but none of the " +
                    $"{save.Files.Count()} file(s) in it is an EA roster database. " +
                    "This is probably a save for something other than the roster.");

            return new LegacyRosterSource(EaDbFile.Parse(entry.Data, entry.Name), save, entry);
        }

        return new LegacyRosterSource(EaDbFile.Read(path), null, null);
    }

    /// <summary>
    /// Writes the roster back out in the form it arrived in, to a NEW path.
    /// </summary>
    public void Save(string path, string? readFrom = null)
    {
        if (_save is null || _entry is null)
        {
            Database.Save(path, readFrom);
            return;
        }

        _save.Replace(_entry, Database.Bytes.ToArray());
        _save.Save(path, readFrom);
    }

    /// <summary>
    /// Writes the roster tables on their own, with no save around them — the
    /// file a database editor opens.
    /// </summary>
    public void SaveDatabaseOnly(string path, string? readFrom = null) => Database.Save(path, readFrom);

    /// <summary>
    /// What the file is, in a sentence, for a report: worth saying because it
    /// decides what the user does with the result.
    /// </summary>
    public string Describe() => InSave
        ? $"a PS2 memory-card save ({SaveName}), with the roster in '{NameInSave}'" +
          (OtherFilesInSave > 0
              ? $" alongside {OtherFilesInSave} other file(s), which come through untouched"
              : "")
        : "a bare roster file";
}
