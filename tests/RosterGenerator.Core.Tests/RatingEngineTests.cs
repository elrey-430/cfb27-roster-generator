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

        Assert.True(Overall(1) >= Overall(10));
        Assert.True(Overall(10) >= Overall(32));
        Assert.True(Overall(32) > Overall(64), "A late first-rounder must outrank a second-round pick.");
        Assert.True(Overall(64) > Overall(200));
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
