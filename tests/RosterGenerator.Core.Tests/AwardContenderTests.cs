using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Two related pieces of Milestone 8, both about the model measuring the
/// season being recreated rather than something adjacent to it.
///
/// <list type="bullet">
/// <item><b>Award contention</b> — being in the conversation for a major
///       award is evidence about a season, and is often the only evidence
///       that survives when an injury or a positional market distorts
///       everything else.</item>
/// <item><b>Draft disagreement</b> — a draft slot records where the NFL took
///       a player months later. When it contradicts what they actually did,
///       the contemporaneous record is trusted more.</item>
/// </list>
/// </summary>
public sealed class AwardContenderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static TalentScorer Scorer() =>
        new(RatingModelSet.Load(FixturePath("RatingModels.json")));

    private static RatingModelSet Model() => RatingModelSet.Load(FixturePath("RatingModels.json"));

    [Fact]
    public void ContendingForAnAwardScoresBelowWinningIt()
    {
        var scorer = Scorer();
        var won = scorer.Assess("QB", new RatingEvidence
        {
            Awards = new List<string> { "conference player of the year" },
        });
        var contended = scorer.Assess("QB", new RatingEvidence
        {
            AwardContender = new List<string> { "conference player of the year" },
        });

        Assert.True(contended.Score < won.Score,
            $"contending ({contended.Score:0.0}) should score below winning ({won.Score:0.0})");
        Assert.Equal(Model().AwardContenderDiscount, won.Score - contended.Score, 3);
    }

    [Fact]
    public void ContendingForAnAwardStillBeatsHavingNoneAtAll()
    {
        var scorer = Scorer();
        var nothing = scorer.Assess("QB", new RatingEvidence { Role = "Starter" });
        var contended = scorer.Assess("QB", new RatingEvidence
        {
            Role = "Starter",
            AwardContender = new List<string> { "heisman" },
        });

        Assert.True(contended.Score > nothing.Score,
            "a Heisman contender should out-rate a player with no honours at all");
    }

    [Fact]
    public void ContendingForABigAwardBeatsWinningASmallOne()
    {
        // The ordering that makes the discount worth having: a Heisman
        // finalist had a better season than a first-team all-conference
        // winner, and taking whichever happens to be a "win" would say
        // otherwise.
        var scorer = Scorer();
        var smallWin = scorer.Assess("QB", new RatingEvidence
        {
            Awards = new List<string> { "first-team all-conference" },
        });
        var bigContender = scorer.Assess("QB", new RatingEvidence
        {
            AwardContender = new List<string> { "heisman" },
        });

        Assert.True(bigContender.Score > smallWin.Score,
            $"Heisman contention ({bigContender.Score:0.0}) should beat all-conference " +
            $"({smallWin.Score:0.0})");
    }

    [Fact]
    public void AnEmptyContenderListChangesNothing()
    {
        // The column is optional and must stay a no-op, like every other
        // optional column.
        var scorer = Scorer();
        var evidence = new RatingEvidence
        {
            Role = "Starter",
            Awards = new List<string> { "first-team all-conference" },
            DraftPickOverall = 40,
        };

        var without = scorer.Assess("QB", evidence);
        var withEmpty = scorer.Assess("QB", evidence with { AwardContender = new List<string>() });

        Assert.Equal(without.Score, withEmpty.Score, 6);
    }

    [Fact]
    public void AnUnrecognisedContenderIsReportedNotSilentlyDropped()
    {
        var assessment = Scorer().Assess("QB", new RatingEvidence
        {
            AwardContender = new List<string> { "Best Hair In The ACC" },
        });

        Assert.Contains(assessment.MissingSignals, m => m.Contains("Best Hair In The ACC"));
    }

    [Fact]
    public void ALateDraftSlotStopsDominatingAnEliteSeason()
    {
        // Jordan Travis: ACC Player of the Year, then a fifth-round pick
        // because he broke his leg in November. The draft signal is the
        // heaviest in the model and was pulling him below his own season.
        var scorer = Scorer();
        var travis = new RatingEvidence
        {
            Role = "Starter",
            DraftPickOverall = 171,
            Awards = new List<string> { "conference player of the year" },
            AwardContender = new List<string> { "heisman" },
        };

        var assessment = scorer.Assess("QB", travis);

        Assert.NotNull(assessment.DemotionNote);
        Assert.Contains("Draft position counted for less", assessment.DemotionNote!);

        // The award is worth 94; he should land nearer that than the ~80 his
        // draft slot implies, without reaching it outright.
        Assert.InRange(assessment.Score, 85, 94);
    }

    [Fact]
    public void ADraftSlotThatAgreesWithTheSeasonIsLeftAlone()
    {
        // The rule must be narrow. A first-round pick who also won awards has
        // no disagreement to resolve, and demoting the strongest signal there
        // would be wrong.
        var assessment = Scorer().Assess("LE", new RatingEvidence
        {
            Role = "Starter",
            DraftPickOverall = 19,
            Awards = new List<string> { "second-team all-american" },
        });

        Assert.Null(assessment.DemotionNote);
    }

    [Fact]
    public void RecruitingStarsCannotTriggerTheDemotion()
    {
        // Recruiting stars predate the season, so they are no better a witness
        // to it than the draft is. Only awards and production count as
        // contradicting a draft slot.
        var assessment = Scorer().Assess("QB", new RatingEvidence
        {
            DraftPickOverall = 200,
            StarRating = 5,
        });

        Assert.Null(assessment.DemotionNote);
    }
}
