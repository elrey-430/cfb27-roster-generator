namespace RosterGenerator.Core.Schema;

/// <summary>
/// Static schema knowledge about the CFB27 <c>Player</c> table: which columns
/// must exist, valid enum values, rating bounds, and the empirically observed
/// team index conventions. Everything here mirrors <c>docs/Schema.md</c>;
/// change them together.
/// </summary>
public static class PlayerSchema
{
    /// <summary>Inclusive lower bound for all numeric rating columns.</summary>
    public const int RatingMin = 0;

    /// <summary>Inclusive upper bound for all numeric rating columns.</summary>
    public const int RatingMax = 99;

    /// <summary>
    /// Offset of the stored <c>Weight</c> value from real pounds:
    /// stored = pounds − 160. Confirmed by correlating the manually edited
    /// 2023 FSU save against real listed weights (exact matches where the
    /// editor's input is known) and by league-wide decoded position
    /// averages (QB ≈ 203 lb, OL ≈ 306 lb, CB ≈ 185 lb).
    /// </summary>
    public const int WeightOffsetPounds = 160;

    /// <summary>Minimum representable weight in pounds (stored 0).</summary>
    public const int WeightPoundsMin = 160;

    /// <summary>Maximum representable weight in pounds (stored 240; both bounds observed).</summary>
    public const int WeightPoundsMax = 400;

    /// <summary>Inclusive bounds for jersey numbers (observed 0–99).</summary>
    public const int JerseyNumMin = 0;

    /// <summary>Inclusive upper bound for jersey numbers.</summary>
    public const int JerseyNumMax = 99;

    /// <summary>
    /// Sentinel used by <c>TeamIndex</c>/<c>PrevTeamIndex</c> for "no team".
    /// FCS generic squads also carry this value in the Team table.
    /// </summary>
    public const int NoTeamSentinel = 255;

    /// <summary>
    /// Sentinel used by <c>PLYR_PREVTEAMID</c> for "no previous team".
    /// Note this differs from <see cref="NoTeamSentinel"/>: the two
    /// previous-team fields use different sentinels (confirmed by diffing
    /// real transfer edits).
    /// </summary>
    public const int NoPrevTeamIdSentinel = 0;

    /// <summary>
    /// Columns that must be present for a file to be treated as a Player
    /// table export. These are the bookkeeping keys plus every column the
    /// typed layer and the validation rules read.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredColumns = new[]
    {
        PlayerColumns.Row, PlayerColumns.IsEmpty,
        PlayerColumns.FirstName, PlayerColumns.LastName, PlayerColumns.JerseyNum,
        PlayerColumns.Height, PlayerColumns.SchoolYear, PlayerColumns.RedshirtStatus,
        PlayerColumns.Position, PlayerColumns.Weight,
        PlayerColumns.AssetName, PlayerColumns.GenericHeadAssetName,
        PlayerColumns.Portrait, PlayerColumns.Comment,
        PlayerColumns.TeamIndex, PlayerColumns.PrevTeamIndex, PlayerColumns.PrevTeamId,
        PlayerColumns.BaseNilValue, PlayerColumns.CurrentNilCompensation,
    };

    /// <summary>Valid <c>Position</c> values (all 21 observed in real exports).</summary>
    public static readonly IReadOnlySet<string> Positions = new HashSet<string>(StringComparer.Ordinal)
    {
        "QB", "HB", "FB", "WR", "TE",
        "LT", "LG", "C", "RG", "RT",
        "LE", "RE", "DT", "LOLB", "MLB", "ROLB",
        "CB", "FS", "SS", "K", "P",
    };

    /// <summary>Valid <c>SchoolYear</c> values.</summary>
    public static readonly IReadOnlySet<string> SchoolYears = new HashSet<string>(StringComparer.Ordinal)
    {
        "Freshman", "Sophomore", "Junior", "Senior",
    };

    /// <summary>Valid <c>RedshirtStatus</c> values.</summary>
    public static readonly IReadOnlySet<string> RedshirtStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "Eligible", "Previous", "Ineligible",
    };

    /// <summary>
    /// The 57 numeric rating columns (integers, 0–99). Columns ending in
    /// "Rating" that are actually enums (<c>RunningStyleRating</c>,
    /// <c>ProspectStarRating</c>) are deliberately excluded.
    /// </summary>
    public static readonly IReadOnlyList<string> NumericRatingColumns = new[]
    {
        "DeepRouteRunningRating", "AgilityRating", "PlayActionRating", "AccelerationRating",
        "PassBlockPowerRating", "ConfidenceRating", "AwarenessRating", "PassBlockRating",
        "OverallRating", "PassBlockFinesseRating", "BCVisionRating", "BreakTackleRating",
        "FinesseMovesRating", "BreakSackRating", "BlockSheddingRating", "ManCoverageRating",
        "MediumRouteRunningRating", "ChangeOfDirectionRating", "CatchingRating", "LongSnapRating",
        "CatchInTrafficRating", "KickReturnRating", "HitPowerRating", "CarryingRating",
        "LeadBlockRating", "JukeMoveRating", "JumpingRating", "KickAccuracyRating",
        "KickPowerRating", "InjuryRating", "ImpactBlockingRating", "ThrowAccuracyDeepRating",
        "ThrowAccuracyMidRating", "ThrowAccuracyRating", "ThrowAccuracyShortRating", "ThrowOnTheRunRating",
        "StiffArmRating", "StrengthRating", "TackleRating", "SpectacularCatchRating",
        "SpeedRating", "SpinMoveRating", "StaminaRating", "ToughnessRating",
        "ThrowUnderPressureRating", "ThrowPowerRating", "ShortRouteRunningRating", "RunBlockFinesseRating",
        "RunBlockPowerRating", "RunBlockRating", "TruckingRating", "PowerMovesRating",
        "PressRating", "PursuitRating", "ReleaseRating", "PlayRecognitionRating",
        "ZoneCoverageRating",
    };

    /// <summary>
    /// The identity-derived asset columns (Group 3). A cosmetic rename must
    /// leave them untouched; replacing a player with a different real person
    /// must update them deliberately, or portrait/head model will mismatch.
    /// </summary>
    public static readonly IReadOnlyList<string> IdentityAssetColumns = new[]
    {
        PlayerColumns.AssetName,
        PlayerColumns.GenericHeadAssetName,
        PlayerColumns.Portrait,
    };
}
