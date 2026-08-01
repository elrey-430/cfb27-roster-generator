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
/// <item>Build the attribute <b>shape</b>: the archetype's MEASURED profile at
///       that overall (<see cref="ArchetypeProfileSet"/>) — falling back to the
///       position baseline for anything unmeasured — then raised on the
///       attributes the player's own production was earned with
///       (<see cref="ProductionEmphasis"/>), overridden by verified
///       measurements (40 time, bench, vertical, shuttle, three-cone), nudged
///       by physique, and shifted for experience.</item>
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

    /// <summary>The model this engine was built from, for roster-level passes.</summary>
    public RatingModelSet Model => _model;
    private readonly OverallFormulaSet _formulas;
    private readonly TalentScorer _scorer;
    private readonly ArchetypeProfileSet? _profiles;
    private readonly ProductionEmphasis _emphasis;

    /// <summary>Creates an engine over the shape model and EA's formulas.</summary>
    /// <param name="model">Tunable shape model (<c>data/RatingModels.json</c>).</param>
    /// <param name="formulas">EA's own overall formulas.</param>
    /// <param name="profiles">
    /// Measured per-archetype attribute profiles
    /// (<c>data/ArchetypeProfiles.json</c>). Optional: without them the engine
    /// falls back to the hand-written position baselines, which is the
    /// Milestone-2 behaviour and is what the older regression fixtures expect.
    /// </param>
    public RatingEngine(RatingModelSet model, OverallFormulaSet formulas, ArchetypeProfileSet? profiles = null)
    {
        _model = model;
        _formulas = formulas;
        _profiles = profiles;
        _scorer = new TalentScorer(model);
        _emphasis = new ProductionEmphasis(model);
    }

    /// <summary>Loads an engine from the data files.</summary>
    /// <param name="ratingModelsPath">Path to <c>RatingModels.json</c>.</param>
    /// <param name="overallFormulasPath">Path to <c>OverallFormulas.json</c>.</param>
    /// <param name="archetypeProfilesPath">
    /// Path to <c>ArchetypeProfiles.json</c>. A null or missing file leaves the
    /// engine on position baselines rather than failing.
    /// </param>
    public static RatingEngine Load(
        string ratingModelsPath, string overallFormulasPath, string? archetypeProfilesPath = null) =>
        new(RatingModelSet.Load(ratingModelsPath), OverallFormulaSet.Load(overallFormulasPath),
            archetypeProfilesPath is { Length: > 0 } path && File.Exists(path)
                ? ArchetypeProfileSet.Load(path)
                : null);

    /// <summary>The measured archetype profiles in use, if any.</summary>
    public ArchetypeProfileSet? Profiles => _profiles;

    /// <summary>EA's overall formulas, for callers that need to recompute.</summary>
    public OverallFormulaSet Formulas => _formulas;

    /// <summary>
    /// Depth-chart roles the model understands, for telling a user that the
    /// role they typed was not one of them. A role is optional and an empty
    /// one changes nothing, but a misspelled one would otherwise be ignored in
    /// silence — indistinguishable from leaving it blank.
    /// </summary>
    public IReadOnlyCollection<string> KnownRoles => _model.RoleScores.Keys;

    /// <summary>True when the text names a role the model can score.</summary>
    public bool IsKnownRole(string role) =>
        _model.RoleScores.ContainsKey(role.Trim().ToLowerInvariant());

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
    /// <param name="programAdjustment">
    /// Points to move the target by because of the program's standing (see
    /// <see cref="RosterDepthModel.ProgramAdjustment"/>). Applied in full only
    /// when the evidence is thin — a player with a draft slot and a stat line
    /// is rated on their own record, not their school's.
    /// </param>
    /// <param name="overallOverride">
    /// An exact overall to generate at, replacing the blend. Used by the
    /// roster-level passes, which can see the whole team and so know something
    /// about a player that scoring them one at a time cannot — see
    /// <see cref="RoleSpread"/>. Every ceiling below still applies.
    /// </param>
    public GeneratedRatings Generate(
        string cfb27Position,
        string? playerType,
        HistoricalPlayer player,
        RatingEvidence evidence,
        int? overallCeiling = null,
        int programAdjustment = 0,
        int? overallOverride = null)
    {
        var group = _model.ResolveGroup(cfb27Position);
        var positionModel = _model.GetModel(group);
        var formula = _formulas.Resolve(cfb27Position, playerType);
        var adjustments = new List<string>();

        // 1. Evidence -> target overall.
        var normalized = evidence with { Stats = TalentScorer.WithDerivedStats(evidence.Stats) };
        var talent = _scorer.Assess(group, normalized);
        var target = (int)Math.Round(talent.Score);

        // 1b. Program standing. Role, awards and stats say what a player did,
        //     never where — so an anonymous backup came out identical at a
        //     playoff program and at the worst team in the country. The
        //     adjustment fades as the evidence strengthens: a first-round pick
        //     is rated on their own record.
        if (programAdjustment != 0)
        {
            var share = talent.Confidence switch
            {
                RatingConfidence.Low => 1.0,
                RatingConfidence.Medium => 0.5,
                _ => 0.0,
            };

            var shift = (int)Math.Round(programAdjustment * share);
            if (shift != 0)
            {
                adjustments.Add(
                    $"Target overall moved {target} -> {target + shift}: the program rates " +
                    $"{Math.Abs(programAdjustment)} point(s) {(programAdjustment > 0 ? "above" : "below")} " +
                    $"a typical one, and this player's own record is {talent.Confidence} confidence.");
                target += shift;
            }
        }

        // 1c. Secondary production. The talent score asks one question per
        //     position — "how well did this back run?" — and a back who caught
        //     37 passes answered a second one it never asked. Credit it here,
        //     bounded, so the roster's shape still comes from the primary role.
        var roles = _emphasis.Score(group, normalized.Stats);
        var secondary = _emphasis.SecondaryOverallBonus(roles, out var secondaryNote);
        var secondaryPoints = (int)Math.Round(secondary);
        if (secondaryPoints > 0)
        {
            adjustments.Add(
                $"Target overall moved {target} -> {target + secondaryPoints}: {secondaryNote}");
            target += secondaryPoints;
        }

        // 1c-bis. A roster-level pass has already decided this player's
        //     overall from something only the whole team shows, and it decided
        //     it against the game's own measured curve — so it lands after the
        //     program and secondary adjustments rather than before them, or
        //     those would move it back off the curve. Every cap below still
        //     applies: they are about what the game can hold.
        if (overallOverride is int decided)
        {
            adjustments.Add(
                $"Target overall set to {decided} rather than the blended {target} by a roster-level rule.");
            target = decided;
        }

        // 1d. The drafted floor. Being drafted at all is the strongest single
        //     fact a college career leaves behind — a few hundred players out
        //     of ten thousand — and the weighted blend cannot express that,
        //     because draft is one signal of five. Applied before every
        //     ceiling below, so a cap always still wins.
        if (_model.DraftedOverallFloor > 0 && normalized.WasDrafted)
        {
            var floor = (int)Math.Round(_model.DraftedOverallFloor);
            if (target < floor)
            {
                adjustments.Add(
                    $"Target overall raised {target} -> {floor}: every drafted player is rated at least " +
                    $"{floor}. Being drafted is the strongest single fact a college career leaves behind, " +
                    "and it is one signal of five in the blend.");
                target = floor;
            }
        }

        // 1e. The undrafted ceiling, the other side of the same boundary. Only
        //     an explicit UDFA is capped — a blank draft column is a gap in the
        //     record, not a statement that the player went undrafted, and most
        //     all-time rosters carry no draft data whatever.
        if (_model.UndraftedOverallCeiling > 0 && normalized.UndraftedFreeAgent)
        {
            var undraftedCap = (int)Math.Round(_model.UndraftedOverallCeiling);
            if (target > undraftedCap)
            {
                adjustments.Add(
                    $"Target overall reduced {target} -> {undraftedCap}: an undrafted player tops out at " +
                    $"{undraftedCap}, where the drafted band begins.");
                target = undraftedCap;
            }
        }

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

        // 2b. Position ceiling. Award and draft scores share one scale across
        //     every position, but the game's positions do not share a range —
        //     its best punter is an 86 where its best receiver is a 99. Left
        //     alone, a nation-leading All-American punter generated at 91,
        //     better than any punter in the game.
        if (_model.PositionOverallCaps.TryGetValue(group, out var positionCap) && target > positionCap)
        {
            adjustments.Add(
                $"Target overall reduced {target} -> {positionCap}: the highest {group} the game itself " +
                $"carries is {positionCap}.");
            target = positionCap;
        }

        if (overallCeiling is int ceiling && target > ceiling)
        {
            adjustments.Add($"Target overall reduced {target} -> {ceiling} by the depth-chart consistency rule.");
            target = ceiling;
        }

        // 3. Attribute shape.
        var profile = _profiles?.Find(formula.PlayerType);
        var locked = new HashSet<string>(StringComparer.Ordinal);
        var attributes = BuildShape(
            positionModel, profile, group, target, talent.Score, player, normalized, roles, adjustments, locked);

        // 4. Calibrate against EA's own formula. The position's sanity caps are
        //    widened to admit the archetype's measured values first: those caps
        //    were written before the game's own players were measured, and a
        //    guess must never overrule a measurement.
        var caps = EffectiveCaps(positionModel, profile, target);
        void Clamp(Dictionary<string, double> values) => ApplyCaps(values, caps, classModel);
        Clamp(attributes);
        var achieved = OverallFormulaSet.Calibrate(formula, attributes, target, Clamp, locked,
            share: a => positionModel.TalentSensitivity.GetValueOrDefault(a, 0.15));
        if (achieved != target)
        {
            adjustments.Add(
                $"Overall settled at {achieved} rather than {target}: sanity caps for {group} " +
                "prevented the remaining adjustment.");
        }

        // 5. Freeze to integers. Rounding moves the raw total by up to half a
        //    point per attribute, which can drop the overall below the value
        //    the double-precision solve achieved, so nudge integer attributes
        //    until the overall computed from the values ACTUALLY WRITTEN hits
        //    the target. This keeps the written overall and the written
        //    attributes in agreement while still landing on the target.
        var final = attributes.ToDictionary(a => a.Key, a => (int)Math.Round(a.Value));
        var finalOverall = SettleIntegers(formula, final, target, caps, classModel, locked);
        if (finalOverall != achieved && finalOverall != target)
        {
            adjustments.Add($"Overall settled at {finalOverall} rather than {target} after rounding to integers.");
        }

        return new GeneratedRatings(final, finalOverall, target, group, formula.PlayerType, talent, adjustments);
    }

    /// <summary>
    /// Nudges whole-number attributes until EA's formula returns
    /// <paramref name="target"/>, respecting caps and locked measurements.
    /// Attributes with the largest coefficients move first, so the fewest
    /// possible points are changed.
    /// </summary>
    private int SettleIntegers(
        OverallFormula formula,
        Dictionary<string, int> values,
        int target,
        IReadOnlyDictionary<string, double[]> caps,
        ClassYearExperienceModel? classModel,
        IReadOnlySet<string> locked)
    {
        double Overall() => formula.Compute(values.ToDictionary(v => v.Key, v => (double)v.Value));

        var candidates = formula.Coefficients
            .Where(c => values.ContainsKey(c.Key) && !locked.Contains(c.Key))
            .OrderByDescending(c => c.Value)
            .Select(c => c.Key)
            .ToList();

        // Each pass may only move every attribute one point, so bound the
        // work by the largest plausible gap rather than looping freely.
        for (var pass = 0; pass < 40; pass++)
        {
            var current = Overall();
            if (current == target)
            {
                return target;
            }

            var direction = current < target ? 1 : -1;
            var moved = false;
            foreach (var attribute in candidates)
            {
                var proposed = values[attribute] + direction;
                var bounded = Bound(attribute, proposed, caps, classModel);
                if (bounded == values[attribute])
                {
                    continue;
                }

                values[attribute] = bounded;
                moved = true;
                if (Overall() == target)
                {
                    return target;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        return (int)Overall();
    }

    /// <summary>Applies position, class-year and global bounds to one integer value.</summary>
    private int Bound(
        string attribute, int value, IReadOnlyDictionary<string, double[]> caps,
        ClassYearExperienceModel? classModel)
    {
        if (caps.TryGetValue(attribute, out var bounds) && bounds.Length == 2)
        {
            value = (int)Math.Clamp(value, bounds[0], bounds[1]);
        }

        if (classModel is not null)
        {
            if (attribute == "AwarenessRating")
            {
                value = Math.Min(value, classModel.AwarenessCap);
            }
            else if (attribute == "PlayRecognitionRating")
            {
                value = Math.Min(value, classModel.PlayRecognitionCap);
            }
        }

        return Math.Clamp(value, _model.GlobalCaps.Min, _model.GlobalCaps.Max);
    }

    private Dictionary<string, double> BuildShape(
        PositionRatingModel positionModel,
        ArchetypeProfile? profile,
        string group,
        int target,
        double talent,
        HistoricalPlayer player,
        RatingEvidence evidence,
        IReadOnlyList<RoleProduction> roles,
        List<string> adjustments,
        HashSet<string> locked)
    {
        var attributes = new Dictionary<string, double>(_model.AttributeDefaults, StringComparer.Ordinal);

        // Where the archetype has been measured, that measurement IS the
        // shape: it is what the game itself gives this archetype at this
        // overall, across every player of it in a real export. The position
        // baseline is a hand-written approximation of the same thing and only
        // fills in what the export could not measure.
        var measured = new HashSet<string>(StringComparer.Ordinal);
        if (profile is not null)
        {
            foreach (var attribute in attributes.Keys.ToList())
            {
                if (profile.TryExpected(attribute, target, out var value))
                {
                    attributes[attribute] = value;
                    measured.Add(attribute);
                }
            }

            adjustments.Add(
                $"Attributes start from what the game gives {profile.SampleSize} real " +
                $"players of this archetype at overall {target}, not from a written-down baseline.");
        }

        foreach (var (attribute, value) in positionModel.Baseline)
        {
            if (!measured.Contains(attribute))
            {
                attributes[attribute] = value;
            }
        }

        // Talent moves each attribute by its own sensitivity, so an elite QB
        // gains far more accuracy than speed. A measured attribute already
        // carries its own dependence on overall and must not be moved twice.
        var talentDelta = talent - _model.ReferenceTalent;
        foreach (var (attribute, sensitivity) in positionModel.TalentSensitivity)
        {
            if (attributes.ContainsKey(attribute) && !measured.Contains(attribute))
            {
                attributes[attribute] += talentDelta * sensitivity;
            }
        }

        _emphasis.Apply(roles, profile, attributes, locked, adjustments);
        ApplyPhysique(attributes, group, player, adjustments);
        ApplyMeasurements(attributes, evidence, adjustments, locked);
        ApplyExperience(attributes, player);
        return attributes;
    }

    /// <summary>
    /// The position's sanity caps, widened wherever the archetype's measured
    /// value falls outside them.
    ///
    /// Those caps were written by hand before the game's own 16,000 players
    /// were measured, and several of them are simply wrong: they would hold a
    /// pass-protecting centre's acceleration below what every pass-protecting
    /// centre in the game has. A guess must not overrule a measurement, so the
    /// cap yields to the profile — and goes on bounding everything else,
    /// including how far calibration may drag the attribute afterwards.
    /// </summary>
    private static Dictionary<string, double[]> EffectiveCaps(
        PositionRatingModel positionModel, ArchetypeProfile? profile, int target)
    {
        var caps = positionModel.Caps.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
        if (profile is null)
        {
            return caps;
        }

        foreach (var (attribute, bounds) in caps.ToList())
        {
            if (bounds.Length == 2 && profile.TryExpected(attribute, target, out var value))
            {
                caps[attribute] = new[] { Math.Min(bounds[0], value), Math.Max(bounds[1], value) };
            }
        }

        return caps;
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
        Dictionary<string, double> attributes, IReadOnlyDictionary<string, double[]> caps,
        ClassYearExperienceModel? classModel)
    {
        foreach (var (attribute, bounds) in caps)
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
