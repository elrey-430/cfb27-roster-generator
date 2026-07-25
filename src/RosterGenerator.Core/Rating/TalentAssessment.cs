using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>How much evidence stood behind a generated rating.</summary>
public enum RatingConfidence
{
    /// <summary>Little or no evidence — mostly position/class defaults.</summary>
    Low,

    /// <summary>Some evidence (production or recruiting profile).</summary>
    Medium,

    /// <summary>Strong retrospective evidence (draft slot and/or major awards).</summary>
    High,
}

/// <summary>One talent signal that contributed to a player's score.</summary>
/// <param name="Name">Signal name: draft, awards, production, recruiting, role.</param>
/// <param name="Score">The 0–100 talent score this signal implies.</param>
/// <param name="Weight">The signal's weight in the blend.</param>
/// <param name="Explanation">Human-readable justification.</param>
public sealed record TalentSignal(string Name, double Score, double Weight, string Explanation);

/// <summary>
/// The result of weighing a player's historical evidence: a 0–100 talent
/// score, the confidence in it, and the signals that produced it. This is
/// the transparency contract — every generated player can explain itself.
/// </summary>
public sealed class TalentAssessment
{
    /// <summary>Creates an assessment.</summary>
    public TalentAssessment(double score, RatingConfidence confidence, double coverage,
        IReadOnlyList<TalentSignal> signals, IReadOnlyList<string> missingSignals, string? floorNote = null,
        string? demotionNote = null)
    {
        Score = score;
        Confidence = confidence;
        Coverage = coverage;
        Signals = signals;
        MissingSignals = missingSignals;
        FloorNote = floorNote;
        DemotionNote = demotionNote;
    }

    /// <summary>Blended talent score, 0–100 (75 = average FBS starter).</summary>
    public double Score { get; }

    /// <summary>Confidence derived from how much of the signal weight was available.</summary>
    public RatingConfidence Confidence { get; }

    /// <summary>Fraction of total signal weight that had data (0–1).</summary>
    public double Coverage { get; }

    /// <summary>The signals that fired, most influential first.</summary>
    public IReadOnlyList<TalentSignal> Signals { get; }

    /// <summary>Names of signals with no supporting data.</summary>
    public IReadOnlyList<string> MissingSignals { get; }

    /// <summary>
    /// Set when a signal floor raised the score above the weighted blend
    /// (e.g. a first-round pick whose other signals were ordinary).
    /// </summary>
    public string? FloorNote { get; }

    /// <summary>
    /// Set when a signal's weight was cut because it contradicted the evidence
    /// measuring the season being recreated — currently only the draft slot.
    /// </summary>
    public string? DemotionNote { get; }

    /// <summary>Human-readable reasons, suitable for a report.</summary>
    public IReadOnlyList<string> Reasons
    {
        get
        {
            var reasons = Signals.OrderByDescending(s => s.Weight).Select(s => s.Explanation).ToList();
            if (DemotionNote is not null)
            {
                reasons.Add(DemotionNote);
            }

            if (FloorNote is not null)
            {
                reasons.Add(FloorNote);
            }

            return reasons;
        }
    }
}
