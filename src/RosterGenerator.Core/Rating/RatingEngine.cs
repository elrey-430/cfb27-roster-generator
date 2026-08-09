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

        // 1a. Unless a source roster already answered the question. A later
        //     game in the same series stores its overall plainly and on this
        //     game's scale, so an 84 there is an 84 here — a fact about the
        //     player, where everything the blend does is an inference about
        //     him. It also switches off the steps below that exist to cover
        //     for not knowing: the program nudge, the secondary-production
        //     bonus, the drafted floor, the undrafted ceiling and the
        //     low-confidence class cap are all ways of guessing better, and
        //     there is nothing left to guess.
        var sourceDecided = normalized.SourceOverall is double;
        if (normalized.SourceOverall is double sourceOverall)
        {
            var stated = (int)Math.Round(Math.Clamp(sourceOverall, _model.GlobalCaps.Min, _model.GlobalCaps.Max));
            adjustments.Add(
                $"Target overall set to {stated} from the source roster's own rating rather than the " +
                $"blended {target}: the source records it on this game's scale, so it is read as a number " +
                "rather than as a place in an order.");
            target = stated;
        }

        // 1b. Program standing. Role, awards and stats say what a player did,
        //     never where — so an anonymous backup came out identical at a
        //     playoff program and at the worst team in the country. The
        //     adjustment fades as the evidence strengthens: a first-round pick
        //     is rated on their own record.
        if (programAdjustment != 0 && !sourceDecided)
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
        if (secondaryPoints > 0 && !sourceDecided)
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
        if (_model.DraftedOverallFloor > 0 && normalized.WasDrafted && !sourceDecided)
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
        if (_model.UndraftedOverallCeiling > 0 && normalized.UndraftedFreeAgent && !sourceDecided)
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
        // The cap reads role first and class second, because that is the order
        // the game's own rosters put them in: its backups reach 78/77/77/77 by
        // class and its reserves 73/73/73/73, while its starters run
        // 82/84/87/87. A cap on class alone was wrong in both directions — a
        // freshman backup held ten points under where the game puts one, a
        // senior reserve let nine points over.
        var lowConfidenceCap = _model.LowConfidenceCap(normalized.Role, classYear, classModel);
        if (lowConfidenceCap is double capValue && talent.Confidence == RatingConfidence.Low &&
            target > capValue && !sourceDecided)
        {
            var capped = (int)Math.Round(capValue);
            adjustments.Add(
                $"Target overall reduced {target} -> {capped}: " +
                $"{normalized.Role ?? "a player"} in the {classYear} class with Low confidence evidence " +
                $"tops out there in the game's own rosters.");
            target = capped;
        }

        // 2b. Position ceiling. Award and draft scores share one scale across
        //     every position, but the game's positions do not share a range —
        //     its best punter is an 86 where its best receiver is a 99. Left
        //     alone, a nation-leading All-American punter generated at 91,
        //     better than any punter in the game.
        //
        //     This too is about inference and so is off when a source decided
        //     the number. The ceiling is the highest the game's own shipped
        //     rosters go, not the highest it can hold, and holding an imported
        //     96 quarterback down to 95 would leave him carrying the ratings
        //     of a 96 with the overall of a 95 — the ratings are locked, so
        //     the only way to reach the lower number is to pull the handful of
        //     attributes that are not locked far out of shape.
        if (_model.PositionOverallCaps.TryGetValue(group, out var positionCap) && target > positionCap &&
            !sourceDecided)
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
        var carried = new HashSet<string>(StringComparer.Ordinal);
        var attributes = BuildShape(
            positionModel, profile, group, target, talent.Score, player, normalized, roles, adjustments,
            locked, carried);

        // 4. Calibrate against EA's own formula. The position's sanity caps are
        //    widened to admit the archetype's measured values first: those caps
        //    were written before the game's own players were measured, and a
        //    guess must never overrule a measurement.
        var caps = EffectiveCaps(positionModel, profile, target);
        void Clamp(Dictionary<string, double> values) => ApplyCaps(values, caps, classModel, locked);
        Clamp(attributes);

        // 4b. A player who arrived with real ratings is rescaled onto this
        //     game's scale rather than calibrated. See <see cref="Rescale"/>.
        int achieved;
        if (carried.Count > 0)
        {
            achieved = Rescale(formula, attributes, carried, target, group, adjustments);
        }
        else
        {
            achieved = OverallFormulaSet.Calibrate(formula, attributes, target, Clamp, locked,
                share: a => positionModel.TalentSensitivity.GetValueOrDefault(a, 0.15));
            if (achieved != target)
            {
                adjustments.Add(
                    $"Overall settled at {achieved} rather than {target}: sanity caps for {group} " +
                    "prevented the remaining adjustment.");
            }
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
                var bounded = Bound(attribute, proposed, caps, classModel, locked);
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
        ClassYearExperienceModel? classModel, IReadOnlySet<string>? locked = null)
    {
        // Same rule as ApplyCaps: what the game's own rosters do never
        // overrules a number that was measured or recorded.
        if (locked is not null && locked.Contains(attribute))
        {
            return Math.Clamp(value, _model.GlobalCaps.Min, _model.GlobalCaps.Max);
        }

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
        HashSet<string> locked,
        HashSet<string> carried)
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
        ApplyLegacyShape(attributes, evidence, adjustments, locked);
        ApplySourceRatings(attributes, evidence, profile, target, adjustments, locked, carried);
        ApplyMeasurements(attributes, evidence, adjustments, locked, carried);

        // An imported player's class year is already in his ratings — whoever
        // built that roster rated a senior as a senior — so the experience
        // shift would count it twice. It also has to be skipped rather than
        // merely blocked on the carried attributes: lifting only the ones the
        // source never recorded raises the overall, and the rescale below then
        // pulls the carried ratings down to compensate, which is the class
        // year moving them by the back door.
        if (carried.Count == 0)
        {
            ApplyExperience(attributes, player, locked);
        }
        return attributes;
    }

    /// <summary>
    /// Writes back the ratings a source roster actually recorded, and leaves
    /// the archetype to fill the gaps CFB27 leaves around them.
    ///
    /// <para>NCAA 14 answers forty-two of CFB27's fifty-seven rating columns on
    /// the same 0-99 scale. Those forty-two are not estimated here at all: the
    /// number is copied and the attribute is locked, so calibration, the caps,
    /// the rounding pass and the experience shift all leave it where whoever
    /// built that roster put it.</para>
    ///
    /// <para>The remaining fifteen are the ones the older game never had a
    /// column for — throw under pressure, break sack, play action, the deep
    /// route runs — and they come from the archetype's measured profile at this
    /// player's overall, which <see cref="BuildShape"/> has already seeded.
    /// That is the whole trade: real numbers where they exist, and where they
    /// do not, what the game itself gives this kind of player at this level
    /// rather than an invention.</para>
    ///
    /// <para>Two of the forty-two are one number where CFB27 wants three. See
    /// <see cref="Split"/>.</para>
    /// </summary>
    private void ApplySourceRatings(
        Dictionary<string, double> attributes, RatingEvidence evidence, ArchetypeProfile? profile,
        int target, List<string> adjustments, HashSet<string> locked, HashSet<string> carried)
    {
        if (evidence.SourceRatings.Count == 0)
        {
            return;
        }

        var kept = 0;
        foreach (var (attribute, value) in evidence.SourceRatings)
        {
            if (_model.SourceRatingSplits.TryGetValue(attribute, out var across))
            {
                Split(attributes, attribute, value, across, profile, target, adjustments, locked, carried);

                // The number goes into the split and nowhere else, even where
                // a column of the same name survives in CFB27. The game still
                // has a general ThrowAccuracyRating, but no overall formula
                // reads it and its own players carry values that make no sense
                // against the three it does read — a 33 on an improviser whose
                // short, mid and deep are all in the eighties. Copying the
                // source's 95 into that column would make an imported
                // quarterback the one player in the game whose vestigial
                // column means something.
                continue;
            }

            if (!attributes.ContainsKey(attribute))
            {
                continue;
            }

            attributes[attribute] = Math.Clamp(value, _model.GlobalCaps.Min, _model.GlobalCaps.Max);
            locked.Add(attribute);
            carried.Add(attribute);
            kept++;
        }

        var filled = attributes.Count - locked.Count;
        adjustments.Add(
            $"{kept} rating(s) came from the source roster. The remaining {filled} " +
            (profile is not null
                ? $"came from what the game gives this archetype at overall {target} — the older game had " +
                  "no column for them."
                : "came from the position baseline — the older game had no column for them."));
    }

    /// <summary>
    /// Spreads one source rating across the several CFB27 asks for.
    ///
    /// <para>The archetype's measured profile decides the <em>shape</em>: at
    /// overall 85 the game's own field generals throw 91 short, 89 mid and 87
    /// deep, and its pure scramblers 85/82/77. The source's single number
    /// decides the <em>level</em>: every one of the three moves by the same
    /// amount until their plain mean is what the source said. So a 95 accuracy
    /// on a field general comes out 97/95/93, and the same 95 on a pure
    /// scrambler comes out steeper.</para>
    ///
    /// <para>Clamping at the caps would otherwise quietly lower the mean, so
    /// whatever a clamped value cannot take is handed back to the ones with
    /// room. When none has room the mean falls short, which is honest: the
    /// game cannot hold what the source asked for.</para>
    /// </summary>
    private void Split(
        Dictionary<string, double> attributes, string source, double value, IReadOnlyList<string> across,
        ArchetypeProfile? profile, int target, List<string> adjustments, HashSet<string> locked,
        HashSet<string> carried)
    {
        var parts = across.Where(attributes.ContainsKey).ToList();
        if (parts.Count == 0)
        {
            return;
        }

        var shape = parts.ToDictionary(
            part => part,
            part => profile is not null && profile.TryExpected(part, target, out var expected)
                ? expected
                : attributes[part],
            StringComparer.Ordinal);

        var min = _model.GlobalCaps.Min;
        var max = _model.GlobalCaps.Max;
        var free = new HashSet<string>(parts, StringComparer.Ordinal);
        var result = new Dictionary<string, double>(shape, StringComparer.Ordinal);
        var wanted = value * parts.Count;

        // Each pass moves everything still free by the same amount, then
        // freezes whatever hit a cap. Bounded by the number of parts, because
        // every pass either lands the mean or freezes at least one of them.
        for (var pass = 0; pass <= parts.Count && free.Count > 0; pass++)
        {
            var fixedTotal = parts.Where(p => !free.Contains(p)).Sum(p => result[p]);
            var shift = (wanted - fixedTotal - free.Sum(p => shape[p])) / free.Count;
            var clamped = new List<string>();
            foreach (var part in free)
            {
                var moved = shape[part] + shift;
                result[part] = Math.Clamp(moved, min, max);
                if (Math.Abs(result[part] - moved) > 1e-9)
                {
                    clamped.Add(part);
                }
            }

            if (clamped.Count == 0)
            {
                break;
            }

            free.ExceptWith(clamped);
        }

        foreach (var part in parts)
        {
            attributes[part] = result[part];
            locked.Add(part);
            carried.Add(part);
        }

        adjustments.Add(
            $"The source's one {Describe(source)} of {value:0} became " +
            string.Join(", ", parts.Select(p => $"{Describe(p)} {result[p]:0}")) +
            $" — shaped by what the game gives this archetype at overall {target}, and moved together " +
            $"until they average {parts.Average(p => result[p]):0.#}.");
    }

    private static string Describe(string attribute) =>
        attribute.EndsWith("Rating", StringComparison.Ordinal)
            ? attribute[..^"Rating".Length]
            : attribute;

    /// <summary>
    /// Puts a source roster's ratings on this game's scale, without changing
    /// the player's shape.
    ///
    /// <para><b>The defect this fixes.</b> NCAA 14 and CFB27 both compute an
    /// overall from attributes, but neither the formulas nor the attribute
    /// distributions agree, so carrying the numbers across verbatim leaves the
    /// overall somewhere else. Measured over a real 2013 roster — 8,631
    /// players — CFB27's formula returns a mean of 6.8 points below what NCAA
    /// 14 stated at outside linebacker and 2.5 points <em>above</em> it at
    /// corner. That is a 9.6-point spread, and it is not noise: it tracks how
    /// much of each position's formula weight the carried attributes happen to
    /// cover. Left alone it would make corners the best players on every
    /// imported team and linebackers the worst, for no football reason
    /// whatever.</para>
    ///
    /// <para><b>The correction.</b> Every carried attribute moves by the
    /// <em>same</em> amount, solved so that EA's formula returns the overall
    /// the source stated. Because the formula is linear the amount is exact in
    /// closed form: the gap in overall, divided by the coefficient weight the
    /// carried attributes hold. Nothing measured has to be shipped and no
    /// per-position table can go stale — the position-dependence falls out of
    /// the coefficient sums by itself.</para>
    ///
    /// <para><b>Why one shift and not one per attribute.</b> Moving them
    /// together leaves every difference between them untouched, so the player
    /// keeps exactly the shape somebody gave him: who was fast, who was
    /// strong, which quarterback threw better than he ran. Shifting each
    /// attribute by its own amount would pull his shape toward the archetype
    /// average, which is the one thing carrying real ratings was for.</para>
    ///
    /// <para>The attributes the source never recorded do not move at all.
    /// They came from the archetype's measured profile and are already on this
    /// game's scale — moving them would be correcting a number that was never
    /// wrong.</para>
    /// </summary>
    private int Rescale(
        OverallFormula formula, Dictionary<string, double> attributes, IReadOnlySet<string> carried,
        int target, string group, List<string> adjustments)
    {
        var movable = carried
            .Where(a => attributes.ContainsKey(a) && formula.Coefficients.ContainsKey(a))
            .ToList();
        var weight = movable.Sum(a => formula.Coefficients[a]);
        if (movable.Count == 0 || weight <= 0)
        {
            return formula.Compute(attributes);
        }

        var before = formula.Compute(attributes);
        var min = (double)_model.GlobalCaps.Min;
        var max = (double)_model.GlobalCaps.Max;
        var start = movable.ToDictionary(a => a, a => attributes[a], StringComparer.Ordinal);
        var free = new HashSet<string>(movable, StringComparer.Ordinal);

        // Aim a hair below the .5 boundary, because EA rounds an exact .5 down.
        // Each pass moves everything still free by one shift and freezes
        // whatever hit a cap, handing what it could not take to the rest — so a
        // player with one attribute already at 99 still reaches his overall
        // through the others instead of falling short.
        for (var pass = 0; pass <= movable.Count && free.Count > 0; pass++)
        {
            var frozen = movable.Where(a => !free.Contains(a))
                .Sum(a => attributes[a] * formula.Coefficients[a]);
            var others = formula.Coefficients
                .Where(c => attributes.ContainsKey(c.Key) && !carried.Contains(c.Key))
                .Sum(c => attributes[c.Key] * c.Value);
            var freeBase = free.Sum(a => start[a] * formula.Coefficients[a]);
            var freeWeight = free.Sum(a => formula.Coefficients[a]);
            if (freeWeight <= 0)
            {
                break;
            }

            var shift = (target - 0.25 - formula.Intercept - others - frozen - freeBase) / freeWeight;
            var clamped = new List<string>();
            foreach (var attribute in free)
            {
                var moved = start[attribute] + shift;
                attributes[attribute] = Math.Clamp(moved, min, max);
                if (Math.Abs(attributes[attribute] - moved) > 1e-9)
                {
                    clamped.Add(attribute);
                }
            }

            if (clamped.Count == 0)
            {
                break;
            }

            free.ExceptWith(clamped);
        }

        var achieved = formula.Compute(attributes);
        var applied = movable.Average(a => attributes[a] - start[a]);
        adjustments.Add(
            $"The source's {movable.Count} rating(s) that this game's {group} formula reads were moved " +
            $"together by {applied:+0.0;-0.0} point(s) so the overall comes to the {target} the source " +
            $"stated rather than the {before} the same numbers mean here. The two games score the same " +
            "attributes differently; moving them together leaves every difference between them — and so " +
            "the player's shape — exactly as it was.");
        if (achieved != target)
        {
            adjustments.Add(
                $"Overall settled at {achieved} rather than {target}: the ratings ran into the game's " +
                "10-99 bounds before the rest of the gap could be closed.");
        }

        return achieved;
    }

    /// <summary>
    /// Restores the shape an imported player had in the roster he came from.
    ///
    /// <para>Everything above this point gives two players of the same
    /// archetype at the same overall the same attributes. That is right when
    /// nothing distinguishes them and wrong when something does: an older
    /// roster records who was fast and who was strong, and a corner who was
    /// the quickest man in the file should not come out identical to one who
    /// was the most physical.</para>
    ///
    /// <para>What crosses over is the player's rank among others at his own
    /// position, never the old number. Being the fastest corner in a file
    /// means something in any game; a speed of 28 out of 31 means nothing
    /// outside the one it was written in. The shift is bounded by
    /// <see cref="RatingModelSet.LegacyShapeMaxShift"/> so the ranking colours
    /// a player in without deciding what he is.</para>
    ///
    /// <para>Anything a verified measurement has already fixed is left alone —
    /// a stopwatch outranks somebody's recollection.</para>
    /// </summary>
    private void ApplyLegacyShape(
        Dictionary<string, double> attributes, RatingEvidence evidence, List<string> adjustments,
        HashSet<string> locked)
    {
        var reach = _model.LegacyShapeMaxShift;
        if (reach <= 0 || evidence.LegacyRatingPercentiles.Count == 0)
        {
            return;
        }

        var moved = 0;
        foreach (var (attribute, percentile) in evidence.LegacyRatingPercentiles)
        {
            if (!attributes.ContainsKey(attribute) || locked.Contains(attribute))
            {
                continue;
            }

            // 0 is the best at the position and 100 the worst, so a player in
            // the middle of the order leaves the attribute where it was.
            attributes[attribute] += (50 - Math.Clamp(percentile, 0, 100)) / 50.0 * reach;
            moved++;
        }

        if (moved > 0)
        {
            adjustments.Add(
                $"{moved} attribute(s) moved by up to {reach:0.#} point(s) to keep the shape this player " +
                "had among others at his position in the roster he was imported from.");
        }
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
        HashSet<string> locked, HashSet<string> carried)
    {
        // A verified measurement REPLACES the estimate — it is the best
        // evidence available for that attribute. It also takes the attribute
        // out of the carried set: a stopwatch is a statement on this game's
        // scale already, so the rescale below must not move it.
        void FromCurve(double? measurement, double[][] curve, string attribute, string label, string unit)
        {
            if (measurement is not double value || curve.Length == 0)
            {
                return;
            }

            var rating = RatingModelSet.Interpolate(curve, value);
            attributes[attribute] = rating;
            locked.Add(attribute);
            carried.Remove(attribute);
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
            carried.Remove("AccelerationRating");
        }

        FromCurve(evidence.BenchPressReps, _model.BenchRepsToStrength, "StrengthRating", "bench press", " reps");
        FromCurve(evidence.VerticalJumpInches, _model.VerticalToJumping, "JumpingRating", "vertical jump", "\"");
        FromCurve(evidence.ShuttleSeconds, _model.ShuttleToAgility, "AgilityRating", "20-yard shuttle", "s");
        FromCurve(evidence.ThreeConeSeconds, _model.ThreeConeToChangeOfDirection,
            "ChangeOfDirectionRating", "three-cone drill", "s");
    }

    private void ApplyExperience(
        Dictionary<string, double> attributes, HistoricalPlayer player, IReadOnlySet<string> locked)
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
            // A senior's awareness is normally lifted because the roster file
            // says nothing about it. When something does — a stopwatch, or a
            // source roster that recorded the number — moving it would be
            // overwriting evidence with the reason the estimate existed.
            if (attributes.ContainsKey(attribute) && !locked.Contains(attribute))
            {
                attributes[attribute] += shift;
            }
        }
    }

    /// <summary>
    /// Holds every attribute inside the position, class-year and global
    /// bounds.
    ///
    /// <para>Attributes a measurement or a source roster has fixed are left
    /// alone. Every cap here describes what the game's own rosters do — a
    /// freshman's awareness tops out at 78 in them — which is exactly the kind
    /// of statement that has to yield to a number somebody actually recorded.
    /// The global 10-99 bounds still apply to everything, because those are
    /// what the format itself holds.</para>
    /// </summary>
    private void ApplyCaps(
        Dictionary<string, double> attributes, IReadOnlyDictionary<string, double[]> caps,
        ClassYearExperienceModel? classModel, IReadOnlySet<string>? locked = null)
    {
        bool Free(string attribute) => locked is null || !locked.Contains(attribute);

        foreach (var (attribute, bounds) in caps)
        {
            if (attributes.TryGetValue(attribute, out var value) && bounds.Length == 2 && Free(attribute))
            {
                attributes[attribute] = Math.Clamp(value, bounds[0], bounds[1]);
            }
        }

        if (classModel is not null)
        {
            if (attributes.TryGetValue("AwarenessRating", out var awareness) && Free("AwarenessRating"))
            {
                attributes["AwarenessRating"] = Math.Min(awareness, classModel.AwarenessCap);
            }

            if (attributes.TryGetValue("PlayRecognitionRating", out var recognition) &&
                Free("PlayRecognitionRating"))
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
