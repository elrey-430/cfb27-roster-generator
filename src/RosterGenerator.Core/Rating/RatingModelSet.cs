using System.Text.Json;
using System.Text.Json.Serialization;

using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>
/// One position group's attribute SHAPE model. It does not define the
/// overall rating — that comes from EA's own formulas
/// (<see cref="OverallFormulaSet"/>).
/// </summary>
public sealed class PositionRatingModel
{
    /// <summary>Attribute values for a player at the model's reference talent.</summary>
    public Dictionary<string, double> Baseline { get; init; } = new();

    /// <summary>Attribute change per point of talent above/below the reference.</summary>
    public Dictionary<string, double> TalentSensitivity { get; init; } = new();

    /// <summary>Per-attribute [min, max] sanity bounds for this position.</summary>
    public Dictionary<string, double[]> Caps { get; init; } = new();
}

/// <summary>One statistic's contribution to the production signal.</summary>
public sealed class ProductionStatModel
{
    /// <summary>Canonical stat name (see <see cref="StatKeys"/>).</summary>
    public string Stat { get; init; } = "";

    /// <summary>Relative weight within the position's production score.</summary>
    public double Weight { get; init; }

    /// <summary>[statValue, score] points, interpolated piecewise-linearly.</summary>
    public double[][] Curve { get; init; } = Array.Empty<double[]>();
}

/// <summary>
/// One thing a football player can be doing on the field — passing, rushing,
/// covering — expressed as the statistics that measure it and the attributes
/// that perform it.
/// </summary>
public sealed class ProductionRoleModel
{
    /// <summary>Attributes this role is performed with.</summary>
    public List<string> Attributes { get; init; } = new();

    /// <summary>Statistics that measure it, scored on the overall scale.</summary>
    public List<ProductionStatModel> Stats { get; init; } = new();
}

/// <summary>Which roles a position group is judged on.</summary>
public sealed class RoleUseModel
{
    /// <summary>Roles the group's talent score already accounts for.</summary>
    public List<string> Primary { get; init; } = new();

    /// <summary>
    /// Roles the group can also fill. These shape attributes like a primary
    /// role and additionally raise the target overall, because production the
    /// primary curves cannot see still won games.
    /// </summary>
    public List<string> Secondary { get; init; } = new();
}

/// <summary>
/// Turns what a player actually did into movement on the attributes that did
/// it. See the <c>//</c> note in <c>data/RatingModels.json</c>.
/// </summary>
public sealed class ProductionEmphasisModel
{
    /// <summary>Role score below which production says nothing worth acting on.</summary>
    public double Threshold { get; init; } = 68;

    /// <summary>Role-score points that amount to one standard deviation of movement.</summary>
    public double ScorePerSigma { get; init; } = 12;

    /// <summary>Most standard deviations any attribute may be moved.</summary>
    public double MaxSigma { get; init; } = 2;

    /// <summary>Most overall points a secondary role may add.</summary>
    public double SecondaryOverallBonusMax { get; init; } = 4;

    /// <summary>Role score at which the secondary bonus is fully earned.</summary>
    public double SecondaryOverallBonusCeiling { get; init; } = 95;

    /// <summary>Role name → its statistics and attributes.</summary>
    public Dictionary<string, ProductionRoleModel> Roles { get; init; } = new();

    /// <summary>Position group → the roles it is judged on.</summary>
    public Dictionary<string, RoleUseModel> Groups { get; init; } = new();
}

/// <summary>Class-year experience adjustments and caps.</summary>
public sealed class ClassYearExperienceModel
{
    /// <summary>Points added to experience-driven attributes.</summary>
    public int Shift { get; init; }

    /// <summary>Maximum awareness for this class year.</summary>
    public int AwarenessCap { get; init; }

    /// <summary>Maximum play recognition for this class year.</summary>
    public int PlayRecognitionCap { get; init; }

    /// <summary>Overall cap applied when confidence is Low (little evidence).</summary>
    public int LowConfidenceOverallCap { get; init; }
}

/// <summary>Typical size for a position group, derived from real save data.</summary>
public sealed class PhysiqueModel
{
    /// <summary>Median height in inches for this position group.</summary>
    public int ReferenceHeightInches { get; init; }

    /// <summary>Median weight in pounds for this position group.</summary>
    public int ReferenceWeightPounds { get; init; }
}

/// <summary>How size differences move physical attributes.</summary>
public sealed class PhysiqueEffectsModel
{
    /// <summary>Strength points per 10 lb above the position reference.</summary>
    public double StrengthPerTenPoundsOverReference { get; init; }

    /// <summary>Speed points per 10 lb above the position reference (negative).</summary>
    public double SpeedPerTenPoundsOverReference { get; init; }

    /// <summary>Acceleration points per 10 lb above the reference (negative).</summary>
    public double AccelerationPerTenPoundsOverReference { get; init; }

    /// <summary>Agility points per 10 lb above the reference (negative).</summary>
    public double AgilityPerTenPoundsOverReference { get; init; }

    /// <summary>Jumping points per inch above the reference height.</summary>
    public double JumpingPerInchOverReference { get; init; }

    /// <summary>Maximum points any single physique effect may move.</summary>
    public double MaxPhysiqueAdjustment { get; init; } = 8;
}

/// <summary>Global floor/ceiling applied to every generated attribute.</summary>
public sealed class GlobalCapsModel
{
    /// <summary>Absolute minimum attribute value.</summary>
    public int Min { get; init; } = 10;

    /// <summary>Absolute maximum attribute value.</summary>
    public int Max { get; init; } = 99;
}

/// <summary>
/// The complete, tunable rating model loaded from
/// <c>data/RatingModels.json</c>. Every number the engine uses lives here —
/// no rating constants are compiled into the code — so the model can be
/// re-tuned without rebuilding. Documented in <c>Ratings/Rating_Model.md</c>
/// and <c>Ratings/Position_Formulas.md</c>.
/// </summary>
public sealed class RatingModelSet
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private Dictionary<string, string>? _groupByPosition;

    /// <summary>Talent value that the baseline attribute values describe.</summary>
    public int ReferenceTalent { get; init; } = 75;

    /// <summary>Starting attribute values before position baselines are applied.</summary>
    public Dictionary<string, double> AttributeDefaults { get; init; } = new();

    /// <summary>Per-position-group rating models.</summary>
    public Dictionary<string, PositionRatingModel> Positions { get; init; } = new();

    /// <summary>Group name → the CFB27 positions it covers.</summary>
    public Dictionary<string, List<string>> PositionGroups { get; init; } = new();

    /// <summary>Relative weight of each talent signal (draft, awards, ...).</summary>
    public Dictionary<string, double> SignalWeights { get; init; } = new();

    /// <summary>
    /// Per-signal floor tolerances. A signal listed here sets a minimum on
    /// the target overall at (its implied overall − tolerance), so a strong
    /// retrospective signal cannot be averaged away by weaker ones.
    /// </summary>
    public Dictionary<string, double> SignalFloors { get; init; } = new();

    /// <summary>
    /// Per role, the overall the game itself carries at each percentile of
    /// that role's own range — measured across 11,730 players on 138 teams.
    ///
    /// <para>A single role score cannot reproduce a roster's shape, because the
    /// game spreads 14 points inside its starters and 8 to 9 inside every other
    /// role. See <see cref="RoleSpread"/> for what is done with it.</para>
    /// </summary>
    public Dictionary<string, double[][]> RoleSpread { get; init; } = new();

    /// <summary>
    /// The overall a Low-confidence player may not exceed, by depth-chart role
    /// and then class year — the 90th percentile of what the game itself
    /// carries for that kind of player, measured across 11,730 players on 138
    /// teams.
    ///
    /// <para>It replaces a cap that read class year alone, which conflated
    /// "young" with "unknown". The measurement says role dominates and class
    /// barely registers below the starting eleven: the game's backups reach 78,
    /// 77, 77, 77 by class and its reserves 73, 73, 73, 73, while its starters
    /// run 82, 84, 87, 87. A per-class cap was therefore wrong in both
    /// directions at once — it held a freshman backup ten points under where
    /// the game puts one, and let a senior reserve nine points over.</para>
    ///
    /// <para>Falls back to <c>classYearExperience</c>'s own value when the
    /// roster file names no role, or names one this table does not carry.</para>
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> LowConfidenceCapByRole { get; init; } = new();

    /// <summary>
    /// The Low-confidence overall cap for a role and class, or null when
    /// neither this table nor the class model has one.
    /// </summary>
    public double? LowConfidenceCap(string? role, string? classYear, ClassYearExperienceModel? classModel)
    {
        if (role is { Length: > 0 } && classYear is { Length: > 0 } &&
            LowConfidenceCapByRole.TryGetValue(role.Trim().ToLowerInvariant(), out var byClass) &&
            byClass.TryGetValue(classYear, out var cap))
        {
            return cap;
        }

        return classModel?.LowConfidenceOverallCap;
    }

    /// <summary>Signal-coverage thresholds for High/Medium confidence.</summary>
    public Dictionary<string, double> ConfidenceThresholds { get; init; } = new();

    /// <summary>[overallPick, score] points for the draft signal.</summary>
    public double[][] DraftScores { get; init; } = Array.Empty<double[]>();

    /// <summary>
    /// The lowest overall a drafted player may be generated at, whatever the
    /// rest of their record looks like.
    ///
    /// <para>Being drafted at all is the strongest single fact a college
    /// career can leave behind: a few hundred players out of some ten thousand
    /// in FBS. The weighted blend does not see it that way — draft is one
    /// signal of five, so a seventh-round pick with a thin stat line was
    /// landing at 77, and a sixth-round pick at 80, the same as a player the
    /// roster file says nothing at all about.</para>
    ///
    /// <para>Position caps still win: this raises a floor, it does not lift a
    /// player past what the game's own best at their position carries. Zero
    /// disables it.</para>
    /// </summary>
    public double DraftedOverallFloor { get; init; }

    /// <summary>
    /// The highest overall an <em>explicitly</em> undrafted player may reach.
    ///
    /// <para>The draft is the boundary: drafted players occupy 85 and up
    /// (<see cref="DraftedOverallFloor"/>), undrafted players 85 and down. The
    /// two meet rather than overlap, so where a player sits relative to 85 says
    /// what happened to them.</para>
    ///
    /// <para>This applies only to <c>UDFA</c> in the roster file's
    /// <c>DraftPick</c> column, never to a blank one. "Undrafted" is a
    /// statement about the player; an empty column is a gap in the record, and
    /// most all-time rosters have no draft data at all. Capping those would be
    /// asserting something nobody said. Zero disables it.</para>
    /// </summary>
    public double UndraftedOverallCeiling { get; init; }

    /// <summary>Talent score for an undrafted free agent.</summary>
    public double UndraftedFreeAgentScore { get; init; } = 67;

    /// <summary>Recruiting stars ("1".."5") → talent score.</summary>
    public Dictionary<string, double> RecruitingStarScores { get; init; } = new();

    /// <summary>Depth-chart role → talent score.</summary>
    public Dictionary<string, double> RoleScores { get; init; } = new();

    /// <summary>Award name (lower-case) → talent score.</summary>
    public Dictionary<string, double> AwardScores { get; init; } = new();

    /// <summary>
    /// [rankPercentile, overall] points: where a player sits in his own squad's
    /// order against the overall the game actually gives a player in that slot.
    ///
    /// <para>Measured over EA's own rosters — 138 squads of exactly 85, 11,730
    /// players — by sorting each squad and averaging the overall at each
    /// position in the order. This is what turns an older game's ranking into a
    /// number on this game's scale without either scale ever having to be
    /// reconciled with the other.</para>
    /// </summary>
    public double[][] LegacyRankToOverall { get; init; } = Array.Empty<double[]>();

    /// <summary>
    /// The furthest an attribute may be moved, in points, by where a player
    /// ranked at his position in an older roster.
    ///
    /// <para>Bounded, because the ranking is real information about who was
    /// fast and who was strong while the source's own scale is not: letting it
    /// move an attribute freely would import a number nobody has anchored.</para>
    /// </summary>
    public double LegacyShapeMaxShift { get; init; }

    /// <summary>[fortyTime, speedRating] points.</summary>
    public double[][] FortyYardToSpeed { get; init; } = Array.Empty<double[]>();

    /// <summary>[benchReps, strengthRating] points.</summary>
    public double[][] BenchRepsToStrength { get; init; } = Array.Empty<double[]>();

    /// <summary>[verticalInches, jumpingRating] points.</summary>
    public double[][] VerticalToJumping { get; init; } = Array.Empty<double[]>();

    /// <summary>[shuttleSeconds, agilityRating] points.</summary>
    public double[][] ShuttleToAgility { get; init; } = Array.Empty<double[]>();

    /// <summary>[threeConeSeconds, changeOfDirectionRating] points.</summary>
    public double[][] ThreeConeToChangeOfDirection { get; init; } = Array.Empty<double[]>();

    /// <summary>Class year → experience shift and caps.</summary>
    public Dictionary<string, ClassYearExperienceModel> ClassYearExperience { get; init; } = new();

    /// <summary>Extra experience points for a player who has used a redshirt.</summary>
    public int RedshirtExperienceBonus { get; init; }

    /// <summary>Attributes moved by the class-year experience shift.</summary>
    public List<string> ExperienceAttributes { get; init; } = new();

    /// <summary>Position group → the stats that make up its production score.</summary>
    public Dictionary<string, List<ProductionStatModel>> Production { get; init; } = new();

    /// <summary>How production moves the attributes that produced it.</summary>
    public ProductionEmphasisModel ProductionEmphasis { get; init; } = new();

    /// <summary>
    /// Points deducted from an award's score when the player was only in
    /// contention for it rather than winning it. Set so a Heisman finalist
    /// still outranks a first-team all-conference winner, which is the right
    /// ordering — being in the conversation for the biggest award in the sport
    /// says more than winning a league honour.
    /// </summary>
    public double AwardContenderDiscount { get; init; } = 5;

    /// <summary>
    /// How far the draft signal may sit below the contemporaneous evidence
    /// (awards, production) before it is treated as answering a different
    /// question. See <c>TalentScorer.DemoteDraftIfItDisagrees</c>.
    /// </summary>
    public double DraftDisagreementTolerance { get; init; } = 6;

    /// <summary>
    /// The share of its weight the draft signal keeps once it disagrees. Not
    /// zero: a late pick is still evidence, just weaker evidence about the
    /// season being recreated than the season's own record.
    /// </summary>
    public double DraftDisagreementWeightFactor { get; init; } = 0.35;

    /// <summary>Absolute attribute floor/ceiling.</summary>
    public GlobalCapsModel GlobalCaps { get; init; } = new();

    /// <summary>
    /// Position group → the highest overall the game itself carries at that
    /// position, measured across a full base save.
    ///
    /// Award and draft scores sit on one shared scale, but the positions do
    /// not: the best punter in a 16,257-player save is an 86 and the best
    /// receiver is a 99. Without this, a punter who led the nation and made
    /// first-team All-American was generated at 91 — better than any punter
    /// the game ships. The cap is the observed maximum, not a guess, so an
    /// genuinely elite specialist still reaches the top of their own position.
    /// </summary>
    public Dictionary<string, int> PositionOverallCaps { get; init; } = new();

    /// <summary>Position group → typical size, derived from real save data.</summary>
    public Dictionary<string, PhysiqueModel> Physique { get; init; } = new();

    /// <summary>How size differences move physical attributes.</summary>
    public PhysiqueEffectsModel PhysiqueEffects { get; init; } = new();

    /// <summary>Resolves a CFB27 position (e.g. "LT") to its group (e.g. "OL").</summary>
    /// <exception cref="KeyNotFoundException">The position is in no group.</exception>
    public string ResolveGroup(string cfb27Position)
    {
        _groupByPosition ??= PositionGroups
            .SelectMany(g => g.Value.Select(p => (Position: p, Group: g.Key)))
            .ToDictionary(x => x.Position, x => x.Group, StringComparer.Ordinal);

        if (_groupByPosition.TryGetValue(cfb27Position, out var group))
        {
            return group;
        }

        throw new KeyNotFoundException(
            $"Position '{cfb27Position}' is not assigned to a rating position group in RatingModels.json.");
    }

    /// <summary>Gets the model for a position group.</summary>
    public PositionRatingModel GetModel(string group) =>
        Positions.TryGetValue(group, out var model)
            ? model
            : throw new KeyNotFoundException($"No rating model for position group '{group}'.");

    /// <summary>
    /// Piecewise-linear interpolation over an [input, output] point list.
    /// Points may ascend or descend in input; values outside the range clamp
    /// to the nearest endpoint. This is the single curve primitive used by
    /// every physical and production formula, so all of them are inspectable
    /// and tunable in the JSON.
    /// </summary>
    public static double Interpolate(double[][] points, double input)
    {
        if (points.Length == 0)
        {
            throw new ArgumentException("Curve has no points.", nameof(points));
        }

        var ordered = points.OrderBy(p => p[0]).ToArray();
        if (input <= ordered[0][0])
        {
            return ordered[0][1];
        }

        if (input >= ordered[^1][0])
        {
            return ordered[^1][1];
        }

        for (var i = 1; i < ordered.Length; i++)
        {
            if (input <= ordered[i][0])
            {
                var (x0, y0) = (ordered[i - 1][0], ordered[i - 1][1]);
                var (x1, y1) = (ordered[i][0], ordered[i][1]);
                var t = (input - x0) / (x1 - x0);
                return y0 + t * (y1 - y0);
            }
        }

        return ordered[^1][1];
    }

    /// <summary>Loads the model file.</summary>
    public static RatingModelSet Load(string path) =>
        JsonSerializer.Deserialize<RatingModelSet>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"'{path}' does not contain a rating model.");
}
