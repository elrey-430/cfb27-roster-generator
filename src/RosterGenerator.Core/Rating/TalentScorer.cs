using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>
/// Weighs a player's historical evidence into a target overall rating.
///
/// Every signal is expressed directly on the overall scale — "what overall
/// does this fact imply?" — so the tables in <c>data/RatingModels.json</c>
/// can be read and argued with directly (a Heisman winner implies 98, a
/// first-round pick 89–97, a 1,300-yard rusher 90). The final score is the
/// weighted mean of whichever signals had data, and confidence follows from
/// how much of the total signal weight was covered.
/// </summary>
public sealed class TalentScorer
{
    private readonly RatingModelSet _model;

    /// <summary>Creates a scorer over a rating model.</summary>
    public TalentScorer(RatingModelSet model)
    {
        _model = model;
    }

    /// <summary>Weighs the evidence for one player.</summary>
    /// <param name="positionGroup">Rating position group (QB, HB, OL, ...).</param>
    /// <param name="evidence">The player's historical evidence.</param>
    public TalentAssessment Assess(string positionGroup, RatingEvidence evidence)
    {
        var signals = new List<TalentSignal>();
        var missing = new List<string>();

        AddDraftSignal(evidence, signals, missing);
        AddAwardsSignal(evidence, signals, missing);
        AddProductionSignal(positionGroup, evidence, signals, missing);
        AddRecruitingSignal(evidence, signals, missing);
        AddRoleSignal(evidence, signals, missing);

        var totalWeight = _model.SignalWeights.Values.Sum();
        var coveredWeight = signals.Sum(s => s.Weight);
        var coverage = totalWeight > 0 ? coveredWeight / totalWeight : 0;

        // With no evidence at all, fall back to the model's reference talent
        // (an average FBS starter) rather than inventing a number.
        var score = coveredWeight > 0
            ? signals.Sum(s => s.Score * s.Weight) / coveredWeight
            : _model.ReferenceTalent;

        // A strong retrospective signal sets a floor: a top-10 draft pick was
        // an elite college player regardless of how modest his award list or
        // recruiting rating looked, and averaging would understate him.
        string? floorNote = null;
        foreach (var signal in signals)
        {
            if (!_model.SignalFloors.TryGetValue(signal.Name, out var tolerance))
            {
                continue;
            }

            var floor = signal.Score - tolerance;
            if (floor > score)
            {
                floorNote =
                    $"Raised to {floor:0} (floor from {signal.Name}: {signal.Explanation}) — the weighted " +
                    $"blend of {score:0} understated a player with this record.";
                score = floor;
            }
        }

        var high = _model.ConfidenceThresholds.GetValueOrDefault("high", 0.60);
        var medium = _model.ConfidenceThresholds.GetValueOrDefault("medium", 0.30);
        var confidence = coverage >= high ? RatingConfidence.High
            : coverage >= medium ? RatingConfidence.Medium
            : RatingConfidence.Low;

        return new TalentAssessment(score, confidence, coverage, signals, missing, floorNote);
    }

    private void AddDraftSignal(RatingEvidence evidence, List<TalentSignal> signals, List<string> missing)
    {
        var weight = _model.SignalWeights.GetValueOrDefault("draft");
        var pick = evidence.DraftPickOverall
                   // A round with no pick number is treated as its midpoint.
                   ?? (evidence.DraftRound is int round ? (round - 1) * 32 + 16 : (int?)null);

        if (pick is int overallPick)
        {
            var score = RatingModelSet.Interpolate(_model.DraftScores, overallPick);
            signals.Add(new TalentSignal("draft", score, weight,
                $"Drafted #{overallPick} overall" +
                (evidence.DraftPickOverall is null ? $" (estimated from round {evidence.DraftRound})" : "")));
        }
        else if (evidence.UndraftedFreeAgent)
        {
            signals.Add(new TalentSignal("draft", _model.UndraftedFreeAgentScore, weight,
                "Undrafted free agent"));
        }
        else
        {
            missing.Add("draft position");
        }
    }

    private void AddAwardsSignal(RatingEvidence evidence, List<TalentSignal> signals, List<string> missing)
    {
        var weight = _model.SignalWeights.GetValueOrDefault("awards");
        double best = 0;
        string? bestAward = null;
        foreach (var award in evidence.Awards)
        {
            var key = award.Trim().ToLowerInvariant();
            if (_model.AwardScores.TryGetValue(key, out var score) && score > best)
            {
                best = score;
                bestAward = award.Trim();
            }
        }

        if (bestAward is not null)
        {
            // The best award drives the signal; extras are noted, not stacked,
            // so a long honours list cannot inflate a player past its ceiling.
            var others = evidence.Awards.Count - 1;
            signals.Add(new TalentSignal("awards", best, weight,
                $"{bestAward}{(others > 0 ? $" (+{others} other award{(others > 1 ? "s" : "")})" : "")}"));
        }
        else if (evidence.Awards.Count > 0)
        {
            missing.Add($"recognized awards (none of '{string.Join(", ", evidence.Awards)}' are in the award table)");
        }
        else
        {
            missing.Add("awards");
        }
    }

    private void AddProductionSignal(
        string positionGroup, RatingEvidence evidence, List<TalentSignal> signals, List<string> missing)
    {
        var weight = _model.SignalWeights.GetValueOrDefault("production");
        if (!_model.Production.TryGetValue(positionGroup, out var stats) || stats.Count == 0)
        {
            missing.Add($"production stats (the {positionGroup} model has no statistical signal)");
            return;
        }

        double weighted = 0;
        double used = 0;
        var described = new List<string>();
        foreach (var stat in stats)
        {
            if (!evidence.Stats.TryGetValue(stat.Stat, out var value))
            {
                continue;
            }

            weighted += RatingModelSet.Interpolate(stat.Curve, value) * stat.Weight;
            used += stat.Weight;
            described.Add($"{FormatStat(value)} {stat.Stat}");
        }

        if (used <= 0)
        {
            missing.Add("production stats");
            return;
        }

        // Partial stat coverage scales the signal's weight down, so one stat
        // out of three counts for less than a complete stat line.
        var coverageOfStats = used / stats.Sum(s => s.Weight);
        signals.Add(new TalentSignal("production", weighted / used, weight * coverageOfStats,
            $"Season production: {string.Join(", ", described)}"));
    }

    private void AddRecruitingSignal(RatingEvidence evidence, List<TalentSignal> signals, List<string> missing)
    {
        var weight = _model.SignalWeights.GetValueOrDefault("recruiting");
        if (evidence.StarRating is int stars &&
            _model.RecruitingStarScores.TryGetValue(stars.ToString(), out var score))
        {
            signals.Add(new TalentSignal("recruiting", score, weight, $"{stars}-star recruit"));
        }
        else
        {
            missing.Add("recruiting rating");
        }
    }

    private void AddRoleSignal(RatingEvidence evidence, List<TalentSignal> signals, List<string> missing)
    {
        var weight = _model.SignalWeights.GetValueOrDefault("role");
        if (evidence.Role is not null &&
            _model.RoleScores.TryGetValue(evidence.Role.Trim().ToLowerInvariant(), out var score))
        {
            signals.Add(new TalentSignal("role", score, weight, $"Depth chart role: {evidence.Role.Trim()}"));
        }
        else
        {
            missing.Add("depth chart role");
        }
    }

    private static string FormatStat(double value) =>
        Math.Abs(value - Math.Round(value)) < 1e-9 ? ((long)value).ToString() : value.ToString("0.0");

    /// <summary>
    /// Fills in statistics that can be derived from others (completion
    /// percentage, yards per carry, field-goal percentage) so users only
    /// have to supply the raw counting stats they have.
    /// </summary>
    public static IReadOnlyDictionary<string, double> WithDerivedStats(IReadOnlyDictionary<string, double> stats)
    {
        var derived = new Dictionary<string, double>(stats, StringComparer.OrdinalIgnoreCase);

        void Ratio(string result, string numerator, string denominator, double multiplier)
        {
            if (!derived.ContainsKey(result) &&
                derived.TryGetValue(numerator, out var n) &&
                derived.TryGetValue(denominator, out var d) && d > 0)
            {
                derived[result] = n / d * multiplier;
            }
        }

        Ratio(StatKeys.CompletionPct, StatKeys.Completions, StatKeys.Attempts, 100);
        Ratio(StatKeys.YardsPerCarry, StatKeys.RushYards, StatKeys.RushAttempts, 1);
        Ratio(StatKeys.FieldGoalPct, StatKeys.FieldGoalsMade, StatKeys.FieldGoalsAttempted, 100);
        return derived;
    }
}
