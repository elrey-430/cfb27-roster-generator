using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// What a player the roster file says little about is allowed to reach.
///
/// <para>The cap used to read class year alone, which conflated "young" with
/// "unknown". Measured across 11,730 players on 138 teams, the game says role
/// dominates and class barely registers below the starting eleven — its
/// backups reach 78, 77, 77, 77 by class and its reserves 73, 73, 73, 73,
/// while its starters run 82, 84, 87, 87.</para>
///
/// <para>So one number per class was wrong in both directions at once: it held
/// a freshman backup ten points under where the game puts one, and let a senior
/// reserve nine points over.</para>
/// </summary>
public sealed class ClassCapTests
{
    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static RatingModelSet Model =>
        RatingModelSet.Load(Path.Combine(DataDirectory, "RatingModels.json"));

    private static int Overall(string role, string classYear) =>
        TestFixtures.RatingEngine().Generate("WR", "WR_PhysicalReceiver",
            new HistoricalPlayer
            {
                FirstName = "A", LastName = "B", Position = "WR",
                HeightInches = 73, WeightPounds = 195, ClassYear = classYear,
            },
            new RatingEvidence { Role = role }).Overall;

    [Fact]
    public void AFreshmanStarterIsNotHeldToAFreshmanReserveNumber()
    {
        // The case that kept the low 80s empty: the old cap stopped every
        // freshman at 68 whatever the file said they did, where the game's own
        // freshman starters reach 82.
        Assert.True(Overall("Starter", "Freshman") > 68,
            "a freshman starter is still capped where a freshman walk-on is.");
    }

    [Fact]
    public void ASeniorReserveIsNotAllowedAStartersNumber()
    {
        // The other direction, which the old cap got wrong just as badly: it
        // let a senior reserve reach 82 where the game's reserves top out at 73.
        Assert.True(Overall("Reserve", "Senior") <= 75,
            $"a senior reserve reached {Overall("Reserve", "Senior")}.");
    }

    [Fact]
    public void RoleSeparatesMoreThanClassDoes()
    {
        var freshmanStarter = Overall("Starter", "Freshman");
        var seniorReserve = Overall("Reserve", "Senior");

        Assert.True(freshmanStarter > seniorReserve,
            "a freshman who starts should out-rate a senior who does not play.");
    }

    [Theory]
    [InlineData("starter")]
    [InlineData("backup")]
    [InlineData("reserve")]
    [InlineData("walk-on")]
    public void EveryRoleCarriesACapForEveryClass(string role)
    {
        var model = Model;
        foreach (var classYear in new[] { "Freshman", "Sophomore", "Junior", "Senior" })
        {
            Assert.NotNull(model.LowConfidenceCap(role, classYear, null));
        }
    }

    [Fact]
    public void AFileThatNamesNoRoleFallsBackToTheClassCap()
    {
        // A roster file need not carry a Role column at all, and the old
        // per-class value is still the right answer when it does not.
        var model = Model;
        Assert.Null(model.LowConfidenceCap(null, "Freshman", null));
        Assert.Null(model.LowConfidenceCap("captain", "Freshman", null));
    }
}
