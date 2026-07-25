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
    /// Minimum height in inches. The shortest player in a 16,257-player base
    /// save is 65" (5'5"); the bound is a little wider so a real outlier is
    /// never rejected, while a typo — inches entered as feet, say — is.
    /// </summary>
    public const int HeightInchesMin = 60;

    /// <summary>Maximum height in inches. The tallest observed is 82" (6'10").</summary>
    public const int HeightInchesMax = 90;

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
    /// Value <c>PLYR_PREVTEAMID</c> carries when the player transferred from a
    /// school the dynasty does not model — the FCS and everything else outside
    /// its team list. The most common non-zero value in a base save (363
    /// players), and the only one below the Team table's id range.
    /// </summary>
    public const int PrevTeamIdNotInDynasty = 1009;

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

    /// <summary>
    /// Valid <c>PLYR_HOME_STATE</c> values: the 50 US states in PascalCase
    /// (no spaces) plus <c>NonUS</c> for international players. Confirmed by
    /// enumerating a full dynasty export and a manually edited save.
    /// </summary>
    public static readonly IReadOnlySet<string> HomeStates = new HashSet<string>(StringComparer.Ordinal)
    {
        "Alabama", "Alaska", "Arizona", "Arkansas", "California", "Colorado", "Connecticut", "Delaware",
        "Florida", "Georgia", "Hawaii", "Idaho", "Illinois", "Indiana", "Iowa", "Kansas", "Kentucky",
        "Louisiana", "Maine", "Maryland", "Massachusetts", "Michigan", "Minnesota", "Mississippi", "Missouri",
        "Montana", "Nebraska", "Nevada", "NewHampshire", "NewJersey", "NewMexico", "NewYork", "NonUS",
        "NorthCarolina", "NorthDakota", "Ohio", "Oklahoma", "Oregon", "Pennsylvania", "RhodeIsland",
        "SouthCarolina", "SouthDakota", "Tennessee", "Texas", "Utah", "Vermont", "Virginia", "Washington",
        "WestVirginia", "Wisconsin", "Wyoming",
    };

    /// <summary>Value written to <c>PLYR_HOME_STATE</c> for players from outside the US.</summary>
    public const string NonUsHomeState = "NonUS";

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
