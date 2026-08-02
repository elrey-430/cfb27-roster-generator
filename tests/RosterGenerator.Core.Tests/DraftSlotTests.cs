using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// <c>DraftRound</c> and <c>DraftPick</c> are read together.
///
/// <para><b>Reported.</b> The template offers both columns and the tool read
/// only the pick, as an overall number. So a user writing a second-round
/// selection the way the draft is actually announced — <em>round 2, pick 1</em>
/// — got the first pick of the entire draft, and a 33rd-overall player came out
/// in the high nineties instead of the low nineties.</para>
///
/// <para>Both spellings now work, and which one was meant is decided by
/// arithmetic: a pick larger than a round holds must be an overall number, and
/// otherwise a round makes the pick a position inside it.</para>
/// </summary>
public sealed class DraftSlotTests
{
    private static int? Overall(int? round, int? pick) => DraftSlot.Resolve(round, pick).OverallPick;

    // ---- The bug, stated directly -------------------------------------------

    [Fact]
    public void RoundTwoPickOneIsTheThirtyThirdSelection()
    {
        Assert.Equal(33, Overall(2, 1));
        Assert.Equal(DraftSlot.Reading.WithinRound, DraftSlot.Resolve(2, 1).How);
    }

    [Fact]
    public void BothWaysOfWritingTheSameSlotAgree()
    {
        // The whole point: a user may write it either way and get one answer.
        Assert.Equal(Overall(null, 33), Overall(2, 1));
        Assert.Equal(Overall(null, 64), Overall(2, 32));
        Assert.Equal(Overall(null, 65), Overall(3, 1));
        Assert.Equal(Overall(null, 212), Overall(7, 20));
    }

    [Fact]
    public void ARoundRelativePickAndAnOverallPickAreToldApart()
    {
        // Asked for by name: "2nd round, 45th pick" is the 45th selection,
        // which is the 13th pick of round two — 45 is too large to be a
        // position inside a round.
        Assert.Equal(45, Overall(2, 45));
        Assert.Equal(DraftSlot.Reading.Overall, DraftSlot.Resolve(2, 45).How);

        // And 45 really is the 13th pick of the second round.
        Assert.Equal(45, Overall(2, 13));
    }

    // ---- The readings that were already right -------------------------------

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 5, 5)]
    [InlineData(1, 32, 32)]
    public void RoundOneNeedsNoDecision(int round, int pick, int expected)
    {
        // The two readings cannot differ inside the first round.
        Assert.Equal(expected, Overall(round, pick));
    }

    [Fact]
    public void APickWithNoRoundIsStillAnOverallNumber()
    {
        Assert.Equal(150, Overall(null, 150));
        Assert.Equal(DraftSlot.Reading.Overall, DraftSlot.Resolve(null, 150).How);
    }

    [Fact]
    public void ARoundWithNoPickIsStillItsMidpoint()
    {
        Assert.Equal(16, Overall(1, null));
        Assert.Equal(48, Overall(2, null));
        Assert.Equal(DraftSlot.Reading.RoundMidpoint, DraftSlot.Resolve(2, null).How);
    }

    [Fact]
    public void NothingGivenIsNothingResolved()
    {
        Assert.Null(Overall(null, null));
        Assert.Equal(DraftSlot.Reading.None, DraftSlot.Resolve(null, null).How);
    }

    // ---- Disagreement between the two ---------------------------------------

    [Fact]
    public void ACompensatoryPickPastItsRoundIsNotComplainedAbout()
    {
        // Rounds run past 32 selections when compensatory picks are awarded, so
        // a pick one round beyond where the arithmetic puts it is ordinary.
        Assert.Equal(240, Overall(7, 240));
        Assert.Null(DraftSlot.Resolve(7, 240).Note);
    }

    [Fact]
    public void AFlatContradictionIsReportedAndThePickBelieved()
    {
        var resolved = DraftSlot.Resolve(2, 200);

        Assert.Equal(200, resolved.OverallPick);
        Assert.NotNull(resolved.Note);
        Assert.Contains("round 7", resolved.Note!, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Through the rating engine ------------------------------------------

    [Fact]
    public void TheRoundRelativeReadingReachesTheRating()
    {
        int Rate(int? round, int? pick) => TestFixtures.RatingEngine().Generate(
            "WR", "WR_PhysicalReceiver",
            new HistoricalPlayer
            {
                FirstName = "A", LastName = "B", Position = "WR",
                HeightInches = 75, WeightPounds = 200, ClassYear = "Senior",
            },
            new RatingEvidence { DraftRound = round, DraftPickOverall = pick, Role = "Starter" }).Overall;

        // The reported case: these must now match, and must not be a #1 pick.
        Assert.Equal(Rate(null, 33), Rate(2, 1));
        Assert.True(Rate(2, 1) < Rate(1, 1),
            "a second-round pick still rates like the first pick of the draft.");
    }

    [Fact]
    public void ThePlayerIsToldHowTheirSlotWasRead()
    {
        var ratings = TestFixtures.RatingEngine().Generate("WR", "WR_PhysicalReceiver",
            new HistoricalPlayer
            {
                FirstName = "A", LastName = "B", Position = "WR",
                HeightInches = 75, WeightPounds = 200, ClassYear = "Senior",
            },
            new RatingEvidence { DraftRound = 2, DraftPickOverall = 1, Role = "Starter" });

        // Silently reinterpreting a user's number would be worse than the bug.
        Assert.Contains(ratings.Talent.Reasons,
            r => r.Contains("#33 overall (round 2, pick 1)", StringComparison.Ordinal));
    }
}
