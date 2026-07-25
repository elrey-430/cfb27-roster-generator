using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>
/// A generated player's complete rating set, together with the reasoning
/// that produced it.
/// </summary>
public sealed class GeneratedRatings
{
    /// <summary>Creates a rating result.</summary>
    public GeneratedRatings(
        IReadOnlyDictionary<string, int> attributes,
        int overall,
        int targetOverall,
        string positionGroup,
        string playerType,
        TalentAssessment talent,
        IReadOnlyList<string> adjustments)
    {
        Attributes = attributes;
        Overall = overall;
        TargetOverall = targetOverall;
        PositionGroup = positionGroup;
        PlayerType = playerType;
        Talent = talent;
        Adjustments = adjustments;
    }

    /// <summary>Every generated attribute, keyed by CSV column name.</summary>
    public IReadOnlyDictionary<string, int> Attributes { get; }

    /// <summary>The overall rating EA's formula produces for these attributes.</summary>
    public int Overall { get; }

    /// <summary>The overall the evidence called for, before caps and clamping.</summary>
    public int TargetOverall { get; }

    /// <summary>Rating position group used (QB, HB, OL, ...).</summary>
    public string PositionGroup { get; }

    /// <summary>Archetype whose EA formula was used.</summary>
    public string PlayerType { get; }

    /// <summary>Evidence assessment behind the target overall.</summary>
    public TalentAssessment Talent { get; }

    /// <summary>Caps and corrections that moved the result, in order.</summary>
    public IReadOnlyList<string> Adjustments { get; }

    /// <summary>Confidence in this rating set.</summary>
    public RatingConfidence Confidence => Talent.Confidence;
}
