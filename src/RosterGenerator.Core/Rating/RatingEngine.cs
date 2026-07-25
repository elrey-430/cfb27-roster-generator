using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>
/// Turns historical player information into a complete, believable CFB27
/// rating set.
///
/// The pipeline is deliberately made of separately inspectable steps — no
/// step is a black box, and every number it uses comes from
/// <c>data/RatingModels.json</c> or EA's own <c>data/OverallFormulas.json</c>:
///
/// <list type="number">
/// <item>Weigh the evidence into a <b>target overall</b> (<see cref="TalentScorer"/>).</item>
/// <item>Cap the target for class year when confidence is low.</item>
/// <item>Build the attribute <b>shape</b>: position baselines moved by talent
///       sensitivity, then overridden by verified measurements (40 time,
///       bench, vertical, shuttle, three-cone), nudged by physique, and
///       shifted for experience.</item>
/// <item><b>Calibrate</b>: because EA's overall formula is linear, solve for
///       the attribute offset that makes the game's own formula return the
///       target overall.</item>
/// <item>Apply sanity caps (position, class year, global) and recompute the
///       final overall with EA's formula, so the written overall always
///       agrees with the written attributes.</item>
/// </list>
/// </summary>
public sealed class RatingEngine
{
    private readonly RatingModelSet _model;
    private readonly OverallFormulaSet _formulas;
    private readonly TalentScorer _scorer;

    /// <summary>Creates an engine over the shape model and EA's formulas.</summary>
    public RatingEngine(RatingModelSet model, OverallFormulaSet formulas)
    {
        _model = model;
        _formulas = formulas;
        _scorer = new TalentScorer(model);
    }

    /// <summary>Loads an engine from the two data files.</summary>
    public static RatingEngine Load(string ratingModelsPath, string overallFormulasPath) =>
        new(RatingModelSet.Load(ratingModelsPath), OverallFormulaSet.Load(overallFormulasPath));

    /// <summary>EA's overall formulas, for callers that need to recompute.</summary>
    public OverallFormulaSet Formulas => _formulas;

    /// <summary>
    /// Generates ratings for one player.
    /// </summary>
    /// <param name="cfb27Position">Target CFB27 position (QB, LT, ...).</param>
    /// <param name="playerType">
    /// Archetype whose EA formula to satisfy (e.g. "HB_ElusiveBack"). Taken
    /// from the roster slot the player occupies; an unknown value falls back
    /// to the position's first archetype.
    /// </param>
    /// <param name="player">Identity fields (height, weight, class year).</param>
    /// <param name="evidence">Historical performance evidence.</param>
    /// <param name="overallCeiling">
    /// Optional maximum overall, used by the roster-level depth pass to hold
    /// a backup below the established starter.
    /// </param>
    public GeneratedRatings Generate(
        string cfb27Position,
        string? playerType,
        HistoricalPlayer player,
        RatingEvidence evidence,
        int? overallCeiling = null)
    {
        var group = _model.ResolveGroup(cfb27Position);
        var positionModel = _model.GetModel(group);
        var formula = _formulas.Resolve(cfb27Position, playerType);
        var adjustments = new List<string>();

        // 1. Evidence -> target overall.
        var normalized = evidence with { Stats = TalentScorer.WithDerivedStats(evidence.Stats) };
        var talent = _scorer.Assess(group, normalized);
        var target = (int)Math.Round(talent.Score);

        // 2. Class-year ceiling when the evidence is thin. A true freshman
        //    with no record must not be handed veteran ratings; a freshman
        //    with a Heisman (High confidence) is left alone.
        var classYear = ResolveClassYear(player.ClassYear);
        var classModel = classYear is not null && _model.ClassYearExperience.TryGetValue(classYear, out var cm)
            ? cm
            : null;
        if (classModel is not null && talent.Confidence == RatingConfidence.Low &&
            target > classModel.LowConfidenceOverallCap)
        {
            adjustments.Add(
                $"Target overall reduced {target} -> {classModel.LowConfidenceOverallCap}: " +
                $"{classYear} with Low confidence evidence.");
            target = classModel.LowConfidenceOverallCap;
        }

        if (overallCeiling is int ceiling && target > ceiling)
        {
            adjustments.Add($"Target overall reduced {target} -> {ceiling} by the depth-chart consistency rule.");
            target = ceiling;
        }

        // 3. Attribute shape.
        var locked = new HashSet<string>(StringComparer.Ordinal);
        var attributes = BuildShape(positionModel, group, talent.Score, player, normalized, adjustments, locked);

        // 4. Calibrate against EA's own formula.
        void Clamp(Dictionary<string, double> values) => ApplyCaps(values, positionModel, classModel);
        Clamp(attributes);
        var achieved = OverallFormulaSet.Calibrate(formula, attributes, target, Clamp, locked,
            share: a => positionModel.TalentSensitivity.GetValueOrDefault(a, 0.15));
        if (achieved != target)
        {
            adjustments.Add(
                $"Overall settled at {achieved} rather than {target}: sanity caps for {group} " +
                "prevented the remaining adjustment.");
        }

        // 5. Freeze to integers and recompute the overall from the values
        //    actually written, so the two can never disagree.
        var final = attributes.ToDictionary(a => a.Key, a => (int)Math.Round(a.Value));
        var finalOverall = formula.Compute(final.ToDictionary(a => a.Key, a => (double)a.Value));

        return new GeneratedRatings(final, finalOverall, target, group, formula.PlayerType, talent, adjustments);
    }

    private Dictionary<string, double> BuildShape(
        PositionRatingModel positionModel,
        string group,
        double talent,
        HistoricalPlayer player,
        RatingEvidence evidence,
        List<string> adjustments,
        HashSet<string> locked)
    {
        var attributes = new Dictionary<string, double>(_model.AttributeDefaults, StringComparer.Ordinal);
        foreach (var (attribute, value) in positionModel.Baseline)
        {
            attributes[attribute] = value;
        }

        // Talent moves each attribute by its own sensitivity, so an elite QB
        // gains far more accuracy than speed.
        var talentDelta = talent - _model.ReferenceTalent;
        foreach (var (attribute, sensitivity) in positionModel.TalentSensitivity)
        {
            if (attributes.ContainsKey(attribute))
            {
                attributes[attribute] += talentDelta * sensitivity;
            }
        }

        ApplyPhysique(attributes, group, player, adjustments);
        ApplyMeasurements(attributes, evidence, adjustments, locked);
        ApplyExperience(attributes, player);
        return attributes;
    }

    private void ApplyPhysique(
        Dictionary<string, double> attributes, string group, HistoricalPlayer player, List<string> adjustments)
    {
        if (!_model.Physique.TryGetValue(group, out var reference))
        {
            return;
        }

        var effects = _model.PhysiqueEffects;
        var max = effects.MaxPhysiqueAdjustment;

        if (player.WeightPounds is int pounds)
        {
            var tensOver = (pounds - reference.ReferenceWeightPounds) / 10.0;
            void Nudge(string attribute, double perTen)
            {
                if (attributes.ContainsKey(attribute))
                {
                    attributes[attribute] += Math.Clamp(tensOver * perTen, -max, max);
                }
            }

            Nudge("StrengthRating", effects.StrengthPerTenPoundsOverReference);
            Nudge("SpeedRating", effects.SpeedPerTenPoundsOverReference);
            Nudge("AccelerationRating", effects.AccelerationPerTenPoundsOverReference);
            Nudge("AgilityRating", effects.AgilityPerTenPoundsOverReference);
            if (Math.Abs(tensOver) >= 1.5)
            {
                adjustments.Add(
                    $"Physique: {pounds} lb vs {reference.ReferenceWeightPounds} lb typical for {group} " +
                    (tensOver > 0 ? "(stronger, slightly slower)." : "(faster, slightly weaker)."));
            }
        }

        if (player.HeightInches is int inches && attributes.ContainsKey("JumpingRating"))
        {
            var over = inches - reference.ReferenceHeightInches;
            attributes["JumpingRating"] += Math.Clamp(over * effects.JumpingPerInchOverReference, -max, max);
        }
    }

    private void ApplyMeasurements(
        Dictionary<string, double> attributes, RatingEvidence evidence, List<string> adjustments,
        HashSet<string> locked)
    {
        // A verified measurement REPLACES the estimate — it is the best
        // evidence available for that attribute.
        void FromCurve(double? measurement, double[][] curve, string attribute, string label, string unit)
        {
            if (measurement is not double value || curve.Length == 0)
            {
                return;
            }

            var rating = RatingModelSet.Interpolate(curve, value);
            attributes[attribute] = rating;
            locked.Add(attribute);
            adjustments.Add($"{attribute} fixed at {rating:0} by a verified {label} ({value}{unit}).");
        }

        FromCurve(evidence.FortyYardDash, _model.FortyYardToSpeed, "SpeedRating", "40-yard dash", "s");
        if (evidence.FortyYardDash is double forty)
        {
            // Acceleration tracks the 40 but is not identical to it; the
            // short-area drills below override it when present.
            attributes["AccelerationRating"] =
                RatingModelSet.Interpolate(_model.FortyYardToSpeed, forty + 0.02);
            locked.Add("AccelerationRating");
        }

        FromCurve(evidence.BenchPressReps, _model.BenchRepsToStrength, "StrengthRating", "bench press", " reps");
        FromCurve(evidence.VerticalJumpInches, _model.VerticalToJumping, "JumpingRating", "vertical jump", "\"");
        FromCurve(evidence.ShuttleSeconds, _model.ShuttleToAgility, "AgilityRating", "20-yard shuttle", "s");
        FromCurve(evidence.ThreeConeSeconds, _model.ThreeConeToChangeOfDirection,
            "ChangeOfDirectionRating", "three-cone drill", "s");
    }

    private void ApplyExperience(Dictionary<string, double> attributes, HistoricalPlayer player)
    {
        var classYear = ResolveClassYear(player.ClassYear);
        if (classYear is null || !_model.ClassYearExperience.TryGetValue(classYear, out var classModel))
        {
            return;
        }

        var shift = (double)classModel.Shift;
        if (player.ClassYear is string label &&
            (label.Contains("redshirt", StringComparison.OrdinalIgnoreCase) ||
             label.TrimStart().StartsWith("RS", StringComparison.OrdinalIgnoreCase)))
        {
            shift += _model.RedshirtExperienceBonus;
        }

        foreach (var attribute in _model.ExperienceAttributes)
        {
            if (attributes.ContainsKey(attribute))
            {
                attributes[attribute] += shift;
            }
        }
    }

    private void ApplyCaps(
        Dictionary<string, double> attributes, PositionRatingModel positionModel, ClassYearExperienceModel? classModel)
    {
        foreach (var (attribute, bounds) in positionModel.Caps)
        {
            if (attributes.TryGetValue(attribute, out var value) && bounds.Length == 2)
            {
                attributes[attribute] = Math.Clamp(value, bounds[0], bounds[1]);
            }
        }

        if (classModel is not null)
        {
            if (attributes.TryGetValue("AwarenessRating", out var awareness))
            {
                attributes["AwarenessRating"] = Math.Min(awareness, classModel.AwarenessCap);
            }

            if (attributes.TryGetValue("PlayRecognitionRating", out var recognition))
            {
                attributes["PlayRecognitionRating"] = Math.Min(recognition, classModel.PlayRecognitionCap);
            }
        }

        foreach (var attribute in attributes.Keys.ToList())
        {
            attributes[attribute] = Math.Clamp(attributes[attribute], _model.GlobalCaps.Min, _model.GlobalCaps.Max);
        }
    }

    /// <summary>Maps a class-year label to the model's canonical key.</summary>
    private static string? ResolveClassYear(string? label) =>
        label is not null && Conversion.ClassYear.TryParse(label, out var schoolYear, out _) ? schoolYear : null;
}
