using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Tests for the historical rating engine: EA's overall formulas, evidence
/// weighting, confidence, the sanity guardrails the milestone called out,
/// and believability checks on real historical players.
/// </summary>
public sealed class RatingEngineTests
{
    private static readonly RatingEngine Engine = TestFixtures.RatingEngine();

    private static readonly OverallFormulaSet Formulas = OverallFormulaSet.Load(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "OverallFormulas.json"));

    private static HistoricalPlayer Player(
        string position, int height, int weight, string classYear, string last = "Test") => new()
    {
        FirstName = "Player",
        LastName = last,
        Position = position,
        HeightInches = height,
        WeightPounds = weight,
        ClassYear = classYear,
    };

    private static Dictionary<string, double> Stats(params (string Key, double Value)[] stats) =>
        stats.ToDictionary(s => s.Key, s => s.Value);

    // -- EA overall formulas -------------------------------------------------

    [Fact]
    public void FormulaSetCoversEveryPositionAndArchetype()
    {
        Assert.Equal(79, Formulas.Formulas.Count);
        foreach (var position in Schema.PlayerSchema.Positions)
        {
            Assert.NotEmpty(Formulas.PlayerTypesFor(position));
        }
    }

    [Fact]
    public void OverallIsTheLinearFormulaWithHalfDownRounding()
    {
        var formula = Formulas.Resolve("QB", "QB_FieldGeneral");
        var attributes = formula.Coefficients.Keys.ToDictionary(a => a, _ => 75.0);

        var raw = formula.ComputeRaw(attributes);
        Assert.Equal(raw, formula.Intercept + attributes.Sum(a => a.Value * formula.Coefficients[a.Key]), 6);

        // An exact .5 must round DOWN, matching the game.
        var half = formula.Coefficients.Keys.ToDictionary(a => a, _ => 0.0);
        var single = formula.Coefficients.First().Key;
        half[single] = (Math.Floor(-formula.Intercept) + 0.5 - formula.Intercept - formula.Intercept * 0) /
                       formula.Coefficients[single];
        // Construct a raw value of exactly N + 0.5 and confirm it floors.
        var target = 40.5;
        half[single] = (target - formula.Intercept) / formula.Coefficients[single];
        Assert.Equal(40, formula.Compute(half));
    }

    [Fact]
    public void UnknownArchetypeFallsBackToThePositionRatherThanThrowing()
    {
        var formula = Formulas.Resolve("QB", "QB_NotARealArchetype");

        Assert.Equal("QB", formula.Position);
    }

    [Fact]
    public void GeneratedOverallAlwaysMatchesGeneratedAttributes()
    {
        // The written overall must be exactly what EA's formula produces for
        // the written attributes — the two can never disagree.
        foreach (var (position, archetype) in new[]
                 {
                     ("QB", "QB_FieldGeneral"), ("HB", "HB_ElusiveBack"), ("WR", "WR_DeepThreat"),
                     ("LT", "OT_Power"), ("DT", "DT_NoseTackle"), ("CB", "CB_MantoMan"), ("K", "KP_Accurate"),
                 })
        {
            var ratings = Engine.Generate(position, archetype, Player(position, 74, 220, "Junior"),
                new RatingEvidence { Role = "Starter", DraftPickOverall = 50 });

            var recomputed = Formulas.Resolve(position, archetype)
                .Compute(ratings.Attributes.ToDictionary(a => a.Key, a => (double)a.Value));
            Assert.Equal(ratings.Overall, recomputed);
        }
    }

    [Fact]
    public void FormulasReproduceTheRealGameOverallsInTheDonorFixture()
    {
        // The strongest available check: EA's formulas must reproduce the
        // OverallRating the game itself stored for real players. Measured at
        // 99.33% exact over a full 16,257-player export; the committed
        // 85-player fixture is asserted here.
        var roster = Model.PlayerRoster.Load(
            Path.Combine(AppContext.BaseDirectory, "Tests", "DonorDynasty", "0152_Player.csv"));

        var exact = 0;
        var total = 0;
        foreach (var player in roster.Players.Where(p => p.FirstName.Length > 0))
        {
            var formula = Formulas.Resolve(player.Position, player.GetRaw("PlayerType"));
            var attributes = formula.Coefficients.Keys
                .ToDictionary(a => a, a => (double)player.GetInt(a));
            total++;
            if (formula.Compute(attributes) == player.OverallRating)
            {
                exact++;
            }
        }

        Assert.True(total >= 80, $"Expected the donor fixture to hold a full roster, found {total}.");
        Assert.True(exact >= total * 0.97,
            $"EA overall formulas reproduced only {exact}/{total} stored overalls.");
    }

    // -- Evidence, confidence and transparency -------------------------------

    [Fact]
    public void StrongEvidenceProducesHighConfidenceWithReasons()
    {
        var ratings = Engine.Generate("QB", "QB_FieldGeneral",
            Player("QB", 76, 230, "Redshirt Freshman", "Winston"),
            new RatingEvidence
            {
                DraftPickOverall = 1,
                Role = "Starter",
                StarRating = 5,
                Awards = new[] { "Heisman", "Consensus All-American" },
                Stats = Stats(("PassYards", 4057), ("PassTD", 40), ("Completions", 257), ("Attempts", 384)),
            });

        Assert.Equal(RatingConfidence.High, ratings.Confidence);
        Assert.Contains(ratings.Talent.Reasons, r => r.Contains("#1 overall"));
        Assert.Contains(ratings.Talent.Reasons, r => r.Contains("Heisman"));
        Assert.InRange(ratings.Overall, 90, 99);
    }

    [Fact]
    public void NoEvidenceProducesLowConfidenceAndAModestPlayer()
    {
        var ratings = Engine.Generate("WR", "WR_ShiftyRouteRunner",
            Player("WR", 72, 180, "Freshman"), RatingEvidence.Empty);

        Assert.Equal(RatingConfidence.Low, ratings.Confidence);
        Assert.InRange(ratings.Overall, 40, 70);
    }

    [Fact]
    public void PartialEvidenceProducesMediumConfidence()
    {
        var ratings = Engine.Generate("HB", "HB_ElusiveBack",
            Player("HB", 71, 200, "Junior"),
            new RatingEvidence { Role = "Backup", StarRating = 4, Stats = Stats(("RushYards", 155)) });

        Assert.Equal(RatingConfidence.Medium, ratings.Confidence);
    }

    // -- Verified measurements ----------------------------------------------

    [Theory]
    [InlineData(4.30, 99)]
    [InlineData(4.40, 96)]
    [InlineData(4.50, 92)]
    public void VerifiedFortyTimeSetsSpeedExactlyAndSurvivesCalibration(double forty, int expectedSpeed)
    {
        var ratings = Engine.Generate("HB", "HB_ElusiveBack",
            Player("HB", 71, 200, "Junior"),
            new RatingEvidence { FortyYardDash = forty, Role = "Starter", DraftPickOverall = 41 });

        Assert.Equal(expectedSpeed, ratings.Attributes["SpeedRating"]);
        Assert.Contains(ratings.Adjustments, a => a.Contains("verified 40-yard dash"));
    }

    // -- Guardrails the milestone called out ---------------------------------

    [Fact]
    public void OffensiveLinemenNeverGetEliteSpeed()
    {
        var ratings = Engine.Generate("LT", "OT_Power",
            Player("LT", 78, 315, "Senior"),
            new RatingEvidence { DraftPickOverall = 1, Role = "Starter", Awards = new[] { "Heisman" } });

        Assert.InRange(ratings.Overall, 90, 99);
        Assert.True(ratings.Attributes["SpeedRating"] <= 72,
            $"OL speed was {ratings.Attributes["SpeedRating"]} — an elite tackle still must not be fast.");
    }

    [Fact]
    public void KickersNeverGetHighTackling()
    {
        var ratings = Engine.Generate("K", "KP_Accurate",
            Player("K", 72, 200, "Senior"),
            new RatingEvidence
            {
                DraftPickOverall = 59, Role = "Starter", Awards = new[] { "Consensus All-American" },
            });

        Assert.True(ratings.Attributes["TackleRating"] <= 45,
            $"Kicker tackling was {ratings.Attributes["TackleRating"]}.");
        Assert.True(ratings.Attributes["KickPowerRating"] >= 80, "An All-American kicker should kick well.");
    }

    [Fact]
    public void TrueFreshmenDoNotGetVeteranAwareness()
    {
        var freshman = Engine.Generate("QB", "QB_FieldGeneral",
            Player("QB", 76, 230, "Freshman"),
            new RatingEvidence { DraftPickOverall = 1, Role = "Starter", Awards = new[] { "Heisman" } });
        var senior = Engine.Generate("QB", "QB_FieldGeneral",
            Player("QB", 76, 230, "Senior"),
            new RatingEvidence { DraftPickOverall = 1, Role = "Starter", Awards = new[] { "Heisman" } });

        Assert.True(freshman.Attributes["AwarenessRating"] <= 78);
        Assert.True(senior.Attributes["AwarenessRating"] > freshman.Attributes["AwarenessRating"]);
    }

    [Fact]
    public void UnknownFreshmanIsCappedByLowConfidence()
    {
        var ratings = Engine.Generate("QB", "QB_FieldGeneral",
            Player("QB", 76, 220, "Freshman"), RatingEvidence.Empty);

        Assert.Equal(RatingConfidence.Low, ratings.Confidence);
        Assert.True(ratings.Overall <= 68, $"Unknown freshman generated at {ratings.Overall}.");
    }

    // -- Draft slot ----------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void FirstRoundPicksAreRatedAtLeast91(int overallPick)
    {
        // Real rosters put first-round picks at 91+, high picks higher still.
        // Only modest supporting evidence is supplied here, so this also
        // proves the draft floor survives dilution by weaker signals.
        foreach (var (position, archetype) in new[]
                 {
                     ("QB", "QB_FieldGeneral"), ("HB", "HB_ElusiveBack"),
                     ("CB", "CB_MantoMan"), ("LT", "OT_Power"), ("WR", "WR_DeepThreat"),
                 })
        {
            var ratings = Engine.Generate(position, archetype, Player(position, 73, 210, "Junior"),
                new RatingEvidence { DraftPickOverall = overallPick, Role = "Starter", StarRating = 3 });

            Assert.True(ratings.Overall >= 91,
                $"#{overallPick} overall pick at {position} generated {ratings.Overall}; " +
                "first-round picks must reach 91.");
        }
    }

    [Fact]
    public void HigherPicksOutrankLowerOnesAndLaterRoundsStepDown()
    {
        int Overall(int pick) => Engine.Generate("CB", "CB_MantoMan",
            Player("CB", 73, 195, "Junior"),
            new RatingEvidence { DraftPickOverall = pick, Role = "Starter", StarRating = 3 }).Overall;

        // Above the drafted floor the order is strict, and that is where the
        // draft slot is still doing work: 97, 94, 91 for picks 1, 10 and 32.
        Assert.True(Overall(1) > Overall(10));
        Assert.True(Overall(10) > Overall(32));
        Assert.True(Overall(32) > Overall(64), "A late first-rounder must outrank a second-round pick.");

        // Below it the order flattens, deliberately: every drafted player is
        // rated at least the floor, so a third-rounder and a seventh-rounder
        // meet there. Ordering is preserved as non-increasing, never inverted.
        foreach (var (better, worse) in new[] { (64, 100), (100, 200), (200, 256) })
        {
            Assert.True(Overall(better) >= Overall(worse),
                $"pick {better} came out below pick {worse}.");
        }
    }

    [Fact]
    public void EveryDraftedPlayerClearsTheFloorHoweverLateTheyWent()
    {
        // The point of the floor: a few hundred players out of ten thousand in
        // FBS are drafted at all, and the weighted blend cannot say so, because
        // draft is one signal of five. Before this a seventh-round pick landed
        // at 77 and a sixth-rounder at 80 — the same 80 as a player the roster
        // file says nothing whatever about.
        foreach (var pick in new[] { 64, 100, 160, 200, 256 })
        {
            var overall = Engine.Generate("CB", "CB_MantoMan",
                Player("CB", 73, 195, "Junior"),
                new RatingEvidence { DraftPickOverall = pick, Role = "Starter", StarRating = 3 }).Overall;

            Assert.True(overall >= 85, $"pick {pick} generated at {overall}, below the drafted floor.");
        }
    }

    [Fact]
    public void ARoundOnItsOwnIsStillBeingDrafted()
    {
        // Roster files often carry the round and not the pick number.
        var overall = Engine.Generate("CB", "CB_MantoMan", Player("CB", 73, 195, "Junior"),
            new RatingEvidence { DraftRound = 7, Role = "Starter", StarRating = 3 }).Overall;

        Assert.True(overall >= 85, $"a seventh-round pick generated at {overall}.");
    }

    [Fact]
    public void AnUndraftedPlayerIsNotLiftedByIt()
    {
        // The floor is a fact about being drafted, and both of these players
        // were not. "Undrafted" is a statement; a blank column is a gap; and
        // neither is a draft slot.
        var undrafted = Engine.Generate("CB", "CB_MantoMan", Player("CB", 73, 195, "Junior"),
            new RatingEvidence { UndraftedFreeAgent = true, Role = "Starter", StarRating = 3 }).Overall;
        var unknown = Engine.Generate("CB", "CB_MantoMan", Player("CB", 73, 195, "Junior"),
            new RatingEvidence { Role = "Starter", StarRating = 3 }).Overall;

        Assert.True(undrafted < 85, $"an undrafted free agent was raised to {undrafted}.");
        Assert.True(unknown < 85, $"a player with no draft column was raised to {unknown}.");
    }

    [Fact]
    public void TheFloorSaysSoInTheReasons()
    {
        var ratings = Engine.Generate("CB", "CB_MantoMan", Player("CB", 73, 195, "Junior"),
            new RatingEvidence { DraftPickOverall = 200, Role = "Starter", StarRating = 3 });

        // The draft curve now bottoms out at 85, so the per-pick floor is what
        // does the lifting and the flat 85 backstop underneath it never has to
        // fire. Either way the player is told which rule raised them.
        Assert.Contains(ratings.Talent.Reasons, r => r.Contains("floor from draft"));
    }

    [Fact]
    public void TheDraftCurveRunsFromTheFloorToNinetyNine()
    {
        // Asked for: pick 1 tops out at 99 and everyone else sits on a curve
        // down to the drafted floor. Checked on a receiver, whose position cap
        // is 99 — a halfback caps at 96, which is the game's own limit and not
        // this model's business to exceed.
        int Overall(int pick) => Engine.Generate("WR", "WR_PhysicalReceiver",
            Player("WR", 75, 200, "Senior"),
            new RatingEvidence { DraftPickOverall = pick, Role = "Starter", StarRating = 3 }).Overall;

        Assert.Equal(99, Overall(1));
        Assert.True(Overall(1) > Overall(32), "the first pick must clear the end of round one.");
        Assert.True(Overall(32) > Overall(100));
        Assert.True(Overall(100) > Overall(256));
        Assert.True(Overall(256) >= 85, $"the last pick came out at {Overall(256)}.");
    }

    [Fact]
    public void ADraftSlotIsAFloorAndNeverACeiling()
    {
        // Derrick Henry won the Heisman and went in the second round. His
        // season has to be able to outrun his draft slot, or the draft becomes
        // a verdict on the year rather than a fact about it.
        HistoricalPlayer Back() => Player("HB", 74, 215, "Senior");
        RatingEvidence Season(int pick, bool heisman) => new()
        {
            DraftPickOverall = pick,
            Role = "Starter",
            StarRating = 5,
            Awards = heisman ? new[] { "Heisman" } : Array.Empty<string>(),
            Stats = Stats(("RushYards", heisman ? 2219 : 900), ("RushTD", heisman ? 28 : 8),
                          ("RushAttempts", heisman ? 395 : 180)),
        };

        var heismanSecondRound = Engine.Generate("HB", "HB_PowerBack", Back(), Season(45, true)).Overall;
        var ordinarySecondRound = Engine.Generate("HB", "HB_PowerBack", Back(), Season(45, false)).Overall;
        var heismanSeventhRound = Engine.Generate("HB", "HB_PowerBack", Back(), Season(240, true)).Overall;
        var ordinarySeventhRound = Engine.Generate("HB", "HB_PowerBack", Back(), Season(240, false)).Overall;

        Assert.True(heismanSecondRound > ordinarySecondRound,
            "the Heisman winner did not clear an ordinary player taken at the same pick.");
        Assert.Equal(heismanSecondRound, heismanSeventhRound);
        Assert.True(heismanSeventhRound > ordinarySeventhRound + 5,
            $"a Heisman season was worth only {heismanSeventhRound - ordinarySeventhRound} points " +
            "over an ordinary one at the same late pick.");
    }

    [Fact]
    public void AnUndraftedPlayerTopsOutWhereTheDraftedBandBegins()
    {
        // The two rules meet at 85: drafted players from there up, undrafted
        // from there down, so where a player sits says what happened to them.
        var overall = Engine.Generate("HB", "HB_PowerBack", Player("HB", 74, 215, "Senior"),
            new RatingEvidence
            {
                UndraftedFreeAgent = true, Role = "Starter", StarRating = 5,
                Awards = new[] { "Heisman" },
                Stats = Stats(("RushYards", 2219), ("RushTD", 28), ("RushAttempts", 395)),
            }).Overall;

        Assert.Equal(85, overall);
    }

    [Fact]
    public void ABlankDraftColumnIsNotCappedLikeAnUndraftedOne()
    {
        // Most all-time rosters carry no draft data at all. Capping those would
        // be asserting the player went undrafted, which nobody said.
        var overall = Engine.Generate("HB", "HB_PowerBack", Player("HB", 74, 215, "Senior"),
            new RatingEvidence
            {
                Role = "Starter", StarRating = 5,
                Awards = new[] { "Heisman" },
                Stats = Stats(("RushYards", 2219), ("RushTD", 28), ("RushAttempts", 395)),
            }).Overall;

        Assert.True(overall > 85, $"a player with no draft column was capped at {overall}.");
    }

    [Fact]
    public void APositionCapStillWinsOverTheFloor()
    {
        // The floor raises; it does not lift anyone past what the game's own
        // best at their position carries. The punter cap is 86, which is above
        // the floor — so what is checked is that the cap is still respected
        // rather than bypassed.
        var punter = Engine.Generate("P", null, Player("P", 73, 195, "Junior"),
            new RatingEvidence { DraftPickOverall = 250, Role = "Starter", StarRating = 3 });

        Assert.InRange(punter.Overall, 85, 86);
    }

    [Fact]
    public void DraftFloorIsReportedWhenItRaisesTheBlend()
    {
        var ratings = Engine.Generate("CB", "CB_MantoMan", Player("CB", 73, 202, "Junior"),
            new RatingEvidence
            {
                DraftPickOverall = 5, Role = "Starter", StarRating = 3,
                Awards = new[] { "First-Team All-Conference" },
            });

        Assert.Contains(ratings.Talent.Reasons, r => r.Contains("floor from draft"));
    }

    // -- Depth-chart consistency ---------------------------------------------

    [Fact]
    public void BackupRatedAboveStarterIsFlagged()
    {
        var starter = Rated("Starter", new RatingEvidence { Role = "Starter", StarRating = 3 });
        var backup = Rated("Backup", new RatingEvidence
        {
            Role = "Backup", StarRating = 5, Stats = Stats(("RushYards", 1700), ("RushTD", 20)),
        });

        var violations = DepthConsistency.FindViolations(new[] { starter, backup });

        var violation = Assert.Single(violations);
        Assert.Equal("Backup", violation.Player.Player.LastName);
        Assert.True(violation.Ceiling < starter.Ratings.Overall + 1);
    }

    [Fact]
    public void DraftedBackupIsAllowedToExceedTheStarter()
    {
        var starter = Rated("Starter", new RatingEvidence { Role = "Starter", StarRating = 3 });
        var backup = Rated("Backup", new RatingEvidence
        {
            Role = "Backup", DraftPickOverall = 5, StarRating = 5,
            Awards = new[] { "Consensus All-American" },
            Stats = Stats(("RushYards", 1700), ("RushTD", 20), ("RushAttempts", 220)),
        });

        Assert.Empty(DepthConsistency.FindViolations(new[] { starter, backup }));
    }

    private static RatedPlayer Rated(string last, RatingEvidence evidence)
    {
        var player = Player("HB", 71, 205, "Junior", last) with { Evidence = evidence };
        return new RatedPlayer(player, "HB", "HB_ElusiveBack",
            Engine.Generate("HB", "HB_ElusiveBack", player, evidence));
    }

    // -- Believability on real historical players ----------------------------

    [Theory]
    // Player,           position, archetype,          height, weight, class,               pick, expected overall band
    [InlineData("QB", "QB_FieldGeneral", 76, 230, "Redshirt Freshman", 1, 90, 99)]
    [InlineData("HB", "HB_ElusiveBack", 72, 200, "Junior", 2, 88, 99)]
    [InlineData("FS", "S_Hybrid", 68, 190, "Senior", 41, 80, 92)]
    [InlineData("HB", "HB_ElusiveBack", 68, 203, "Junior", 103, 74, 88)]
    public void EliteHistoricalPlayersLandInBelievableBands(
        string position, string archetype, int height, int weight, string classYear,
        int draftPick, int low, int high)
    {
        var ratings = Engine.Generate(position, archetype, Player(position, height, weight, classYear),
            new RatingEvidence
            {
                DraftPickOverall = draftPick, Role = "Starter", StarRating = 5,
                Awards = new[] { "First-Team All-Conference" },
            });

        Assert.InRange(ratings.Overall, low, high);
    }
}
