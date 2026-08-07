namespace RosterGenerator.Core.Historical;

/// <summary>
/// The historical evidence available for one player, beyond the basic
/// identity fields. Every field is optional: the rating engine uses whatever
/// is present, reports what was missing, and lowers its confidence score
/// accordingly. Stats are held in a dictionary keyed by canonical stat name
/// (see <see cref="StatKeys"/>) so new stats can be supported by editing
/// <c>data/RatingModels.json</c> alone.
/// </summary>
public sealed record RatingEvidence
{
    /// <summary>An evidence set with nothing in it.</summary>
    public static readonly RatingEvidence Empty = new();

    /// <summary>Depth-chart role: Starter / Backup / Reserve / Walk-on.</summary>
    public string? Role { get; init; }

    /// <summary>Recruiting star rating, 1–5.</summary>
    public int? StarRating { get; init; }

    /// <summary>Verified 40-yard dash time in seconds.</summary>
    public double? FortyYardDash { get; init; }

    /// <summary>225 lb bench press repetitions.</summary>
    public int? BenchPressReps { get; init; }

    /// <summary>Vertical jump in inches.</summary>
    public double? VerticalJumpInches { get; init; }

    /// <summary>20-yard shuttle time in seconds.</summary>
    public double? ShuttleSeconds { get; init; }

    /// <summary>Three-cone drill time in seconds.</summary>
    public double? ThreeConeSeconds { get; init; }

    /// <summary>
    /// NFL draft slot as an overall pick number (1 = first overall).
    /// Retrospective, and the single strongest talent signal available.
    /// </summary>
    public int? DraftPickOverall { get; init; }

    /// <summary>Draft round, used only to estimate a pick when the overall pick is unknown.</summary>
    public int? DraftRound { get; init; }

    /// <summary>
    /// True when this player was drafted at all — a pick number, a round, or
    /// both. Explicitly false for an undrafted free agent, which is a fact
    /// about the player rather than a gap in what is known about them.
    /// </summary>
    public bool WasDrafted =>
        !UndraftedFreeAgent && (DraftPickOverall is not null || DraftRound is not null);

    /// <summary>True when the player went undrafted but signed as a free agent.</summary>
    public bool UndraftedFreeAgent { get; init; }

    /// <summary>Award names (matched case-insensitively against the model's award table).</summary>
    public IReadOnlyList<string> Awards { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Awards the player was a contender for without winning — a Heisman
    /// finalist, a conference player-of-the-year candidate, an All-America
    /// watch-list name.
    ///
    /// Being in the conversation for a major award is real evidence about a
    /// season and often the only evidence that survives when something else
    /// distorts the record: an injury in November, a team that collapsed
    /// around the player, a position the NFL does not value. It is scored
    /// from the same vocabulary as <see cref="Awards"/>, discounted, so a
    /// user needs no second list of names.
    /// </summary>
    public IReadOnlyList<string> AwardContender { get; init; } = Array.Empty<string>();

    /// <summary>Season statistics keyed by canonical stat name.</summary>
    public IReadOnlyDictionary<string, double> Stats { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where this player ranked on his own squad in an earlier game's roster,
    /// 0 (the best player on the team) to 100 (the last man on it).
    ///
    /// <para>A rank, not a rating, because an older game's rating scale means
    /// nothing here — those numbers are held in five or six bits and were
    /// never anchored to anything this tool can read. The ordering, though, is
    /// somebody's considered judgement about who was good, and an ordering
    /// survives translation between two games that a number does not.</para>
    /// </summary>
    public double? LegacyRankPercentile { get; init; }

    /// <summary>
    /// Where the player ranked among others at his position in the same
    /// source roster, one entry per rating column, 0 (highest) to 100
    /// (lowest).
    ///
    /// <para>Only the eighteen attributes the older games actually recorded
    /// ever appear here. Like the rank above these are positions in an order,
    /// so the fastest receiver in the source file stays the fastest without
    /// anybody having to decide what his old speed rating "meant" — and a
    /// player keeps the shape that made him himself, rather than collapsing
    /// into the average of everyone else at his rank.</para>
    /// </summary>
    public IReadOnlyDictionary<string, double> LegacyRatingPercentiles { get; init; } =
        new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>True when no evidence at all was supplied.</summary>
    public bool IsEmpty =>
        Role is null && StarRating is null && FortyYardDash is null && BenchPressReps is null &&
        VerticalJumpInches is null && ShuttleSeconds is null && ThreeConeSeconds is null &&
        DraftPickOverall is null && DraftRound is null && !UndraftedFreeAgent &&
        Awards.Count == 0 && AwardContender.Count == 0 && Stats.Count == 0 &&
        LegacyRankPercentile is null && LegacyRatingPercentiles.Count == 0;
}

/// <summary>
/// Canonical statistic names understood by the rating model. Derived stats
/// (completion %, yards per carry, field-goal %) are computed automatically
/// when their components are present.
/// </summary>
public static class StatKeys
{
    /// <summary>Passing yards.</summary>
    public const string PassYards = "PassYards";
    /// <summary>Passing touchdowns.</summary>
    public const string PassTD = "PassTD";
    /// <summary>Interceptions thrown.</summary>
    public const string PassInt = "PassInt";
    /// <summary>Pass completions.</summary>
    public const string Completions = "Completions";
    /// <summary>Pass attempts.</summary>
    public const string Attempts = "Attempts";
    /// <summary>Completion percentage (derived when possible).</summary>
    public const string CompletionPct = "CompletionPct";
    /// <summary>Rushing yards.</summary>
    public const string RushYards = "RushYards";
    /// <summary>Rushing touchdowns.</summary>
    public const string RushTD = "RushTD";
    /// <summary>Rushing attempts.</summary>
    public const string RushAttempts = "RushAttempts";
    /// <summary>Yards per carry (derived when possible).</summary>
    public const string YardsPerCarry = "YardsPerCarry";
    /// <summary>Receiving yards.</summary>
    public const string RecYards = "RecYards";
    /// <summary>Receiving touchdowns.</summary>
    public const string RecTD = "RecTD";
    /// <summary>Receptions.</summary>
    public const string Receptions = "Receptions";
    /// <summary>Total tackles.</summary>
    public const string Tackles = "Tackles";
    /// <summary>Sacks.</summary>
    public const string Sacks = "Sacks";
    /// <summary>Tackles for loss.</summary>
    public const string TacklesForLoss = "TacklesForLoss";
    /// <summary>Interceptions caught.</summary>
    public const string Interceptions = "Interceptions";
    /// <summary>Passes defended.</summary>
    public const string PassesDefended = "PassesDefended";
    /// <summary>Forced fumbles.</summary>
    public const string ForcedFumbles = "ForcedFumbles";
    /// <summary>Field goals made.</summary>
    public const string FieldGoalsMade = "FieldGoalsMade";
    /// <summary>Field goals attempted.</summary>
    public const string FieldGoalsAttempted = "FieldGoalsAttempted";
    /// <summary>Field goal percentage (derived when possible).</summary>
    public const string FieldGoalPct = "FieldGoalPct";
    /// <summary>Longest field goal made.</summary>
    public const string LongFieldGoal = "LongFieldGoal";
    /// <summary>Punting average.</summary>
    public const string PuntAverage = "PuntAverage";
    /// <summary>Games played.</summary>
    public const string GamesPlayed = "GamesPlayed";
    /// <summary>Games started.</summary>
    public const string GamesStarted = "GamesStarted";
}
