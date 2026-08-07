namespace RosterGenerator.Core.Legacy;

/// <summary>
/// One player as the legacy roster file records him.
/// </summary>
public sealed record LegacyPlayer
{
    /// <summary>The file's own player id.</summary>
    public required int PlayerId { get; init; }

    /// <summary>First name, empty when the file carries none.</summary>
    public required string FirstName { get; init; }

    /// <summary>Last name, empty when the file carries none.</summary>
    public required string LastName { get; init; }

    /// <summary>Position label, e.g. <c>LT</c>.</summary>
    public required string Position { get; init; }

    /// <summary>Jersey number.</summary>
    public int? JerseyNumber { get; init; }

    /// <summary>Height in inches.</summary>
    public int? HeightInches { get; init; }

    /// <summary>Weight in pounds.</summary>
    public int? WeightPounds { get; init; }

    /// <summary>Class year label.</summary>
    public string? ClassYear { get; init; }

    /// <summary>EA's skin tone on the 1-8 scale CFB27 uses.</summary>
    public int? SkinTone { get; init; }

    /// <summary>The file's compressed overall. Meaningful only as an ordering.</summary>
    public required int RawOverall { get; init; }

    /// <summary>
    /// Legacy attribute values, keyed by the CFB27 rating column they map to.
    /// Compressed to five or six bits, so — like the overall — they carry an
    /// ordering rather than a scale.
    /// </summary>
    public IReadOnlyDictionary<string, int> RawAttributes { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Depth-chart role, when the chart names this player.</summary>
    public string? Role { get; init; }

    /// <summary>True when nobody ever typed a name on this player.</summary>
    public bool IsNameless => FirstName.Length == 0 && LastName.Length == 0;
}

/// <summary>One team's squad as read out of a legacy roster file.</summary>
/// <param name="TeamId">The file's team id.</param>
/// <param name="School">The school it resolves to, or null when unmapped.</param>
/// <param name="Players">The squad, in player-id order.</param>
public sealed record LegacyTeam(int TeamId, string? School, IReadOnlyList<LegacyPlayer> Players);

/// <summary>The whole of a legacy roster file.</summary>
/// <param name="Teams">Every team, in team-id order.</param>
/// <param name="Notes">
/// What the reader had to decide for itself — ambiguous team boundaries, ids
/// with no school mapping — phrased for someone reading a report.
/// </param>
public sealed record LegacyRosterFile(IReadOnlyList<LegacyTeam> Teams, IReadOnlyList<string> Notes)
{
    /// <summary>Every player across every team.</summary>
    public IEnumerable<LegacyPlayer> AllPlayers => Teams.SelectMany(t => t.Players);
}
