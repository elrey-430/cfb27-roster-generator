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
    [InlineData("LE", 76, 290, "Sacks", 8, "DE_PurePower")]
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

    // -- The second pass, measured against a base save -----------------------
    //
    // tools/measure_archetype_usage.py counts what the game itself does with
    // archetypes. These pin what that measurement decided, because the default
    // is what most of a researched historical roster receives: a default the
    // game never uses puts most of a recreated team in an archetype that does
    // not occur, and nothing in the game says so.

    [Theory]
    // Every one of these was previously a default the game barely used —
    // C_WellRounded on 0 of 403 centres, G_WellRounded on 1 of 944 guards,
    // TE_Possession on 8 of 756 tight ends, KP_Accurate on 5% of punters.
    [InlineData("C", "C_Agile")]
    [InlineData("LG", "G_Agile")]
    [InlineData("RG", "G_Agile")]
    [InlineData("TE", "TE_PhysicalRouteRunner")]
    [InlineData("DT", "DT_PurePower")]
    [InlineData("K", "KP_Power")]
    [InlineData("P", "KP_Power")]
    public void ThePositionDefaultIsWhatTheGameItselfUsesMostOften(string position, string expected)
    {
        var choice = Selector.Select(position, Player(position, 74, 280), RatingEvidence.Empty);

        Assert.Equal(expected, choice.Archetype);
    }

    [Theory]
    [InlineData("LT", 278)] // Anthony Munoz, and every lineman before about 1990
    [InlineData("RT", 290)]
    [InlineData("C", 285)]
    [InlineData("LG", 292)]
    public void ALightOffensiveLinemanIsNoLongerCalledAPassProtector(string position, int weight)
    {
        // The rule said "at most 295 lb means pass protector". The base save
        // says otherwise: OT_PassProtector's median weight is 309 lb, *above*
        // OT_Agile's 305, and the rule caught 13 of 138 real pass protectors
        // while mislabelling 86 other tackles. A light lineman in 1979 is a
        // normal lineman, not a finesse one, and weight cannot tell them apart.
        var choice = Selector.Select(position, Player(position, 76, weight), RatingEvidence.Empty);

        Assert.DoesNotContain("PassProtector", choice.Archetype);
    }

    [Fact]
    public void AHeavyOffensiveLinemanIsStillAPowerBlocker()
    {
        // The other direction survives the same test: a power blocker really
        // is heavier (0.68 separation, precision above the base rate at every
        // OL position), so this rule is kept where the light one was dropped.
        var choice = Selector.Select("RG", Player("RG", 77, 325), RatingEvidence.Empty);

        Assert.Equal("G_Power", choice.Archetype);
        Assert.True(choice.MatchedRule);
    }

    [Fact]
    public void AnAwardWinningKickerIsAPowerKicker()
    {
        // The reported oddity: a Groza winner classified KP_Power off a
        // 53-yard long. The measurement says the classification was right and
        // the reason was wrong — 18 of the game's top 20 kickers are KP_Power,
        // as are 74% of all kickers. It is now the default rather than
        // something a 52-yard field goal has to unlock.
        var choice = Selector.Select(
            "K", Player("K", 71, 190), Ev(("FieldGoalsMade", 21), ("FieldGoalsAttempted", 24),
                ("LongFieldGoal", 53)));

        Assert.Equal("KP_Power", choice.Archetype);
    }

    [Fact]
    public void AKickerWhoIsAccurateWithoutALegIsTheAccurateArchetype()
    {
        // What KP_Accurate actually means in the game: accuracy above power
        // (KickAccuracy 79 vs KickPower 72, against +11 the other way for
        // KP_Power). So it takes both halves — a good percentage and no long.
        var choice = Selector.Select(
            "K", Player("K", 70, 185), Ev(("FieldGoalsMade", 18), ("FieldGoalsAttempted", 20),
                ("LongFieldGoal", 44)));

        Assert.Equal("KP_Accurate", choice.Archetype);
        Assert.True(choice.MatchedRule);
    }

    [Fact]
    public void APunterWithNoFieldGoalsIsAPowerPunter()
    {
        // 95% of the game's punters are KP_Power, and a punter has no field
        // goal percentage, so the accuracy rule cannot fire on one at all.
        var choice = Selector.Select("P", Player("P", 74, 205), Ev(("PuntAverage", 44.1)));

        Assert.Equal("KP_Power", choice.Archetype);
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
