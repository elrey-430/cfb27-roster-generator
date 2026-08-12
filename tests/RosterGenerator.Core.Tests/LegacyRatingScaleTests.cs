using RosterGenerator.Core.Legacy;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The measured 5-bit-to-0-99 scale, pinned against the readings it was built
/// from. If somebody regenerates the data file these are the numbers that came
/// off a television screen, and they do not move.
/// </summary>
public sealed class LegacyRatingScaleTests
{
    private static readonly LegacyRatingScale Scale =
        LegacyRatingScale.Load(TestFixtures.DataPath("LegacyRatingScale.json"));

    [Theory]
    // Read off the 2004 USC roster in-game, matched to what the file stores.
    [InlineData(27, 95)]  // Leinart, overall
    [InlineData(24, 92)]  // Leinart, awareness
    [InlineData(23, 90)]  // Bush, overall
    [InlineData(29, 97)]  // Bush, speed
    [InlineData(20, 88)]  // LenDale White, overall
    [InlineData(13, 78)]  // LenDale White, strength AND awareness
    [InlineData(16, 84)]  // Hance and Booty, overall
    [InlineData(8, 68)]   // three different players, two different columns
    [InlineData(6, 62)]   // Leinart speed and strength, Hinds strength
    [InlineData(1, 44)]   // Hance, strength
    public void TheMeasuredReadingsAreWhatTheScaleSays(int stored, int displayed) =>
        Assert.Equal(displayed, Scale.ToDisplayed(stored));

    [Fact]
    public void TheScaleRisesAllTheWayAndSpansTheGamesRange()
    {
        Assert.Equal(32, Scale.Steps);
        Assert.Equal(40, Scale.ToDisplayed(0));
        Assert.Equal(99, Scale.ToDisplayed(31));

        for (var stored = 1; stored < Scale.Steps; stored++)
        {
            Assert.True(Scale.ToDisplayed(stored) >= Scale.ToDisplayed(stored - 1),
                $"the scale must never fall: {stored - 1} -> {Scale.ToDisplayed(stored - 1)}, " +
                $"{stored} -> {Scale.ToDisplayed(stored)}");
        }
    }

    [Fact]
    public void GoingBackToStoredLandsOnTheValueItCameFrom()
    {
        for (var stored = 0; stored < Scale.Steps; stored++)
        {
            var displayed = Scale.ToDisplayed(stored);
            // 22 and 23 both display 90, so the round trip may land on either.
            Assert.Equal(displayed, Scale.ToDisplayed(Scale.ToStored(displayed)));
        }
    }

    [Fact]
    public void ARatingBetweenTwoStepsTakesTheNearer()
    {
        // The bottom of the scale is four display points to a step; truncating
        // there would cost a player most of a step for nothing.
        Assert.Equal(1, Scale.ToStored(45));
        Assert.Equal(2, Scale.ToStored(47));
        Assert.Equal(31, Scale.ToStored(99));
        Assert.Equal(0, Scale.ToStored(0));
    }
}
