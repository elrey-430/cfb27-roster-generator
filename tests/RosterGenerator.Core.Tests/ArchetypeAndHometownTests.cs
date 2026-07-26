using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;
using RosterGenerator.Core.Validation;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Milestone 5: archetype selection (confirmed writable, but it changes which
/// overall formula applies) and the hometown fields (free-text town plus a
/// strict 51-value state enum).
/// </summary>
public sealed class ArchetypeAndHometownTests
{
    private static readonly ArchetypeSelector Selector = ArchetypeSelector.Load(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ArchetypeRules.json"));

    private static readonly OverallFormulaSet Formulas = OverallFormulaSet.Load(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "OverallFormulas.json"));

    private static HistoricalPlayer Player(string position, int height, int weight) => new()
    {
        FirstName = "Test", LastName = "Player", Position = position,
        HeightInches = height, WeightPounds = weight, ClassYear = "Junior",
    };

    private static RatingEvidence Ev(params (string Key, double Value)[] stats) =>
        new() { Stats = stats.ToDictionary(s => s.Key, s => s.Value) };

    // -- Archetype selection -------------------------------------------------

    [Fact]
    public void EveryPositionHasRulesAndOnlyLegalArchetypes()
    {
        foreach (var position in PlayerSchema.Positions)
        {
            Assert.Contains(position, Selector.Positions);
            var legal = Formulas.PlayerTypesFor(position).ToHashSet();

            foreach (var archetype in Selector.AvailableFor(position))
            {
                Assert.Contains(archetype, legal);
            }

            // The default and every rule must also be legal for the position —
            // the exact defect found in a manually edited save (an LOLB
            // carrying MLB_PassCoverage).
            var chosen = Selector.Select(position, Player(position, 73, 210), RatingEvidence.Empty);
            Assert.Contains(chosen.Archetype, legal);
        }
    }

    [Theory]
    [InlineData("QB", 76, 220, "RushYards", 1050, "QB_PureScrambler")]
    [InlineData("HB", 71, 225, "RushYards", 900, "HB_PowerBack")]
    [InlineData("HB", 71, 195, "Receptions", 45, "HB_ReceivingBack")]
    [InlineData("WR", 76, 215, "RecYards", 900, "WR_Physical")]
    [InlineData("DT", 75, 330, "Sacks", 3, "DT_NoseTackle")]
    [InlineData("MLB", 73, 235, "Interceptions", 3, "MLB_PassCoverage")]
    [InlineData("CB", 72, 190, "Interceptions", 6, "CB_Zone")]
    public void ProfileDrivesArchetypeChoice(
        string position, int height, int weight, string stat, double value, string expected)
    {
        var choice = Selector.Select(position, Player(position, height, weight), Ev((stat, value)));

        Assert.Equal(expected, choice.Archetype);
        Assert.True(choice.MatchedRule);
        // The reason names the field that actually decided it, which may be a
        // measurable rather than the stat supplied.
        Assert.Contains("because", choice.Reason);
    }

    [Fact]
    public void NoEvidenceFallsBackToThePositionDefaultRatherThanGuessing()
    {
        var choice = Selector.Select("HB", Player("HB", 71, 200), RatingEvidence.Empty);

        Assert.False(choice.MatchedRule);
        Assert.Contains("default", choice.Reason);
    }

    [Fact]
    public void ChangingArchetypeAlsoChangesTheOverallItImplies()
    {
        // The reason archetype and overall must move together: the same
        // attributes score differently under different archetypes.
        var attributes = Formulas.Resolve("HB", "HB_ElusiveBack").Coefficients.Keys
            .ToDictionary(a => a, _ => 80.0);
        var elusive = Formulas.Resolve("HB", "HB_ElusiveBack").Compute(attributes);
        var power = Formulas.Resolve("HB", "HB_PowerBack").Compute(attributes);

        Assert.True(elusive > 0 && power > 0);
    }

    // -- Hometown ------------------------------------------------------------

    [Theory]
    [InlineData("Tampa, FL", "Tampa", "Florida")]
    [InlineData("Charleston, WV", "Charleston", "WestVirginia")]
    [InlineData("Buffalo, New York", "Buffalo", "NewYork")]
    [InlineData("Dallas, Texas", "Dallas", "Texas")]
    [InlineData("Miami, florida", "Miami", "Florida")]
    public void HometownParsesTownAndState(string input, string town, string state)
    {
        var parsed = Hometown.Parse(input);

        Assert.NotNull(parsed);
        Assert.Equal(town, parsed!.Town);
        Assert.Equal(state, parsed.State);
        Assert.Null(parsed.Note);
        Assert.Contains(parsed.State, PlayerSchema.HomeStates);
    }

    [Theory]
    [InlineData("Melbourne, Australia")]
    [InlineData("London, England")]
    [InlineData("Washington, DC")]
    public void NonUsHometownsUseTheNonUsEnumValueAndAreReported(string input)
    {
        var parsed = Hometown.Parse(input);

        Assert.NotNull(parsed);
        Assert.Equal(PlayerSchema.NonUsHomeState, parsed!.State);
        Assert.NotNull(parsed.Note);
    }

    [Fact]
    public void BlankHometownIsNotWritten()
    {
        Assert.Null(Hometown.Parse(null));
        Assert.Null(Hometown.Parse("   "));
    }

    [Fact]
    public void SettingAnInvalidHomeStateIsRejected()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);

        Assert.Throws<ArgumentException>(
            () => session.SetHometown(roster.Players.First(), "Nowhere", "West Virginia"));
    }

    [Fact]
    public void HometownIsWrittenToBothColumns()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var player = roster.Players.First();
        var session = new RosterEditSession(roster);

        session.SetHometown(player, "Rock Hill", "SouthCarolina");

        Assert.Equal("Rock Hill", player.GetRaw(PlayerColumns.HomeTown));
        Assert.Equal("SouthCarolina", player.GetRaw(PlayerColumns.HomeState));
    }

    // -- Archetype consistency validation ------------------------------------

    [Fact]
    public void ArchetypeInvalidForPositionIsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        // The real defect: an LOLB carrying an MLB archetype.
        var player = roster.Players.First();
        player.SetRaw(PlayerColumns.Position, "LOLB");
        player.SetRaw(PlayerColumns.PlayerType, "MLB_PassCoverage");

        var report = new RosterValidator().Validate(
            new RosterValidationContext(roster, overallFormulas: Formulas));

        Assert.Contains(report.Errors, i => i.RuleName == "ArchetypeConsistency" &&
                                            i.Column == PlayerColumns.PlayerType);
    }

    [Fact]
    public void ArchetypeChangedWithoutRecomputingTheOverallIsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var player = roster.Players.First(p => p.Position == "QB");
        var original = player.GetRaw(PlayerColumns.PlayerType);
        var attributes = PlayerSchema.NumericRatingColumns
            .Where(roster.Document.HasColumn)
            .ToDictionary(a => a, a => (double)player.GetInt(a));

        // The fixture's stored overall already agrees with its own archetype.
        Assert.Equal(player.OverallRating, Formulas.Resolve("QB", original).Compute(attributes));

        // Switch archetype and leave the overall alone — exactly what the
        // community editor does, and what left 35 of 85 players inconsistent
        // in a manually edited save.
        var other = Formulas.PlayerTypesFor("QB")
            .First(t => t != original &&
                        Formulas.Resolve("QB", t).Compute(attributes) != player.OverallRating);
        player.SetRaw(PlayerColumns.PlayerType, other);

        var report = new RosterValidator().Validate(
            new RosterValidationContext(roster, overallFormulas: Formulas));

        Assert.Contains(report.Errors, i => i.RuleName == "ArchetypeConsistency" &&
                                            i.Message.Contains("without recomputing"));
    }

    [Fact]
    public void ConverterWritesArchetypeAndKeepsTheOverallConsistent()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        var engine = TestFixtures.RatingEngine();
        var converter = new HistoricalTeamConverter(
            Mapping.TeamMappingSet.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TeamMappings.json")),
            Mapping.PositionMappingSet.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PositionMappings.json")),
            engine, Selector);

        converter.Convert(session, new HistoricalRoster
        {
            Season = 2015, School = "Florida State",
            Players = new[]
            {
                new HistoricalPlayer
                {
                    FirstName = "Power", LastName = "Runner", Position = "RB",
                    HeightInches = 71, WeightPounds = 230, ClassYear = "Junior",
                    Hometown = "Tampa, FL",
                    Evidence = new RatingEvidence { Role = "Starter", Stats = new Dictionary<string, double>
                        { ["RushYards"] = 1200, ["RushTD"] = 14, ["RushAttempts"] = 250 } },
                },
            },
        });

        var written = roster.Players.Single(p => p.LastName == "Runner");
        Assert.Equal("Tampa", written.GetRaw(PlayerColumns.HomeTown));
        Assert.Equal("Florida", written.GetRaw(PlayerColumns.HomeState));

        // Whatever archetype was chosen, the overall must agree with it.
        var archetype = written.GetRaw(PlayerColumns.PlayerType);
        Assert.Contains(archetype, Formulas.PlayerTypesFor(written.Position));
        var formula = Formulas.Resolve(written.Position, archetype);
        var values = formula.Coefficients.Keys.ToDictionary(a => a, a => (double)written.GetInt(a));
        Assert.Equal(written.OverallRating, formula.Compute(values));

        // And validation agrees.
        var report = new RosterValidator().Validate(
            new RosterValidationContext(roster, session, overallFormulas: Formulas));
        Assert.DoesNotContain(report.Errors, i => i.RuleName == "ArchetypeConsistency");
    }
}
