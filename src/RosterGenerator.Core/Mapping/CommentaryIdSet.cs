using System.Text.Json;

namespace RosterGenerator.Core.Mapping;

/// <summary>
/// Which surnames the commentary can say out loud, and the id that says them.
///
/// <para>The Player table's <c>PLYR_COMMENT</c> field indexes the recorded
/// commentary audio. Get it right and the announcers call your player by name;
/// leave it at whatever the replaced player had and they are called by
/// somebody else's; set it to 0 and they are never named at all.</para>
///
/// <para><b>Nothing here is invented.</b> The mapping is measured out of
/// dynasty saves the game itself generated, where the game assigned both the
/// surname and the id — 146,295 player rows across nine saves. Saves whose
/// rosters were hand-edited are deliberately excluded from that measurement: a
/// roster editor can leave <c>PLYR_COMMENT</c> pointing at the slot's previous
/// occupant, which would teach this file a name the announcers cannot say.
/// See <c>tools/build_commentary_ids.py</c>.</para>
///
/// <para>That the game works this way is not an assumption either. Renaming
/// two players in the game and exporting again shows the game rewriting
/// <c>PLYR_COMMENT</c> itself, to exactly the values this file gives for the
/// new surnames — including 0 for a surname it has no audio for.</para>
/// </summary>
public sealed class CommentaryIdSet
{
    /// <summary>
    /// The value for a player the commentary never names. It is the game's own
    /// value, held by 20% of the players in an untouched save, not a
    /// placeholder this project chose.
    /// </summary>
    public const int None = 0;

    private readonly Dictionary<string, int> _byLastName;

    private CommentaryIdSet(Dictionary<string, int> byLastName)
    {
        _byLastName = byLastName;
    }

    /// <summary>Surnames with a commentary id.</summary>
    public int Count => _byLastName.Count;

    /// <summary>An empty set, which gives every player <see cref="None"/>.</summary>
    public static CommentaryIdSet Empty { get; } =
        new(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// The commentary id for a surname, or <see cref="None"/> when the
    /// announcers have no recording of it.
    ///
    /// <para>Matching ignores case and surrounding space, because a roster
    /// somebody typed will not be consistent about either. It is otherwise
    /// exact: a near-miss would put the wrong name in the announcer's mouth,
    /// which is worse than silence.</para>
    /// </summary>
    public int ForLastName(string? lastName) =>
        lastName is { Length: > 0 } name && _byLastName.TryGetValue(name.Trim(), out var id)
            ? id
            : None;

    /// <summary>True when the commentary can say this surname.</summary>
    public bool CanSay(string? lastName) => ForLastName(lastName) != None;

    /// <summary>Loads the measured mapping.</summary>
    public static CommentaryIdSet Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("byLastName", out var names))
        {
            throw new InvalidDataException($"'{path}' has no 'byLastName' object.");
        }

        var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in names.EnumerateObject())
        {
            // Last one wins, which only matters for names differing by case.
            mapping[entry.Name.Trim()] = entry.Value.GetInt32();
        }

        return new CommentaryIdSet(mapping);
    }
}
