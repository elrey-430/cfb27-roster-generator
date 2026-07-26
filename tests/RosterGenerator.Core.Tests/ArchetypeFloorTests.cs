using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Holds every generated player inside the range the game itself uses for
/// their archetype, on the attributes that archetype is actually judged by.
///
/// The defect this guards is general, and both reports of it were the same
/// bug seen from opposite sides. A user's Marcus Allen — a back who caught 34
/// passes, correctly classified as a receiving back — came out with 30 in all
/// three route-running attributes, because the shape was assembled from a
/// hand-written position baseline that never mentioned them and a global
/// default that said 30. Another user's Marqise Lee, a receiver, came out with
/// 34 juke and 30 trucking for the same reason. In both cases the archetype
/// was chosen correctly and then ignored.
///
/// So the rule is not "Marcus Allen should run routes". It is: <b>no generated
/// player may sit below the floor the game's own players of that archetype
/// occupy, in an attribute that archetype's overall formula weights heavily.</b>
/// That catches the next instance of this class rather than these two.
/// </summary>
public sealed class ArchetypeFloorTests
{
    /// <summary>
    /// A coefficient this fraction of the formula's largest one counts as
    /// "what this archetype is judged by". EA's formulas separate the
    /// attributes that matter from the ones carried along by two orders of
    /// magnitude, so the exact cut is not delicate.
    /// </summary>
    private const double HeavyWeightShare = 0.25;

    /// <summary>
    /// How far below the archetype's measured line a player may sit, in
    /// standard deviations of the spread the game itself shows there. Three
    /// sigma is deliberately generous: this is a floor against nonsense, not a
    /// demand that everyone be average.
    /// </summary>
    private const double FloorSigmas = 3.0;

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static RatingEngine Engine() => RatingEngine.Load(
        FixturePath("RatingModels.json"), FixturePath("OverallFormulas.json"),
        FixturePath("ArchetypeProfiles.json"));

    private static ArchetypeSelector Selector() => ArchetypeSelector.Load(FixturePath("ArchetypeRules.json"));

    /// <summary>
    /// The attributes an archetype's overall formula leans on, paired with the
    /// lowest value the game's own players of that archetype reach there.
    /// </summary>
    private static IEnumerable<(string Attribute, double Floor)> Floors(
        OverallFormula formula, ArchetypeProfile profile, int overall)
    {
        var heaviest = formula.Coefficients.Values.Max();
        foreach (var (attribute, coefficient) in formula.Coefficients)
        {
            if (coefficient < heaviest * HeavyWeightShare ||
                !profile.TryExpected(attribute, overall, out var expected))
            {
                continue;
            }

            yield return (attribute, expected - FloorSigmas * profile.Spread(attribute));
        }
    }

    [Fact]
    public void NoGeneratedPlayerSitsBelowTheirArchetypesMeasuredFloor()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var engine = Engine();
        var depth = RosterDepthModel.Load(FixturePath("RosterDepth.json"));

        var report = new HistoricalTeamConverter(
                export.BuildTeamMappings(),
                PositionMappingSet.Load(FixturePath("PositionMappings.json")),
                engine,
                Selector(),
                new RosterFiller(depth, engine),
                depth)
            .Convert(session, csv.Roster);

        Assert.NotEmpty(report.Converted);
        var profiles = engine.Profiles;
        Assert.NotNull(profiles);

        var breaches = new List<string>();
        var checkedAttributes = 0;
        foreach (var entry in report.Converted)
        {
            var ratings = entry.Ratings;
            if (ratings is null || entry.AssignedPosition is not string position)
            {
                continue;
            }

            var profile = profiles!.Find(ratings.PlayerType);
            if (profile is null)
            {
                continue;
            }

            var formula = engine.Formulas.Resolve(position, ratings.PlayerType);
            foreach (var (attribute, floor) in Floors(formula, profile, ratings.Overall))
            {
                if (!ratings.Attributes.TryGetValue(attribute, out var value))
                {
                    continue;
                }

                checkedAttributes++;
                if (value < floor)
                {
                    breaches.Add(
                        $"{entry.Player.FirstName} {entry.Player.LastName} ({position} " +
                        $"{ratings.PlayerType}, {ratings.Overall} OVR): " +
                        $"{attribute} {value}, below the {floor:0} the game's own {ratings.PlayerType} " +
                        "players reach");
                }
            }
        }

        Assert.True(checkedAttributes > 200, $"only {checkedAttributes} attributes were checked — the guard is idle");
        Assert.True(breaches.Count == 0,
            $"{breaches.Count} generated attribute(s) fell below what the game itself gives that " +
            $"archetype:{Environment.NewLine}{string.Join(Environment.NewLine, breaches.Take(25))}");
    }

    [Fact]
    public void ABackWhoCaughtPassesCanRunRoutes()
    {
        var engine = Engine();
        var player = new HistoricalPlayer
        {
            FirstName = "Receiving", LastName = "Back", Position = "HB",
            ClassYear = "Junior", HeightInches = 72, WeightPounds = 210,
        };
        var evidence = new RatingEvidence
        {
            Role = "Starter",
            DraftRound = 1,
            DraftPickOverall = 10,
            Awards = new List<string> { "heisman" },
            Stats = new Dictionary<string, double>
            {
                ["rushYards"] = 2342, ["rushTD"] = 22, ["rushAttempts"] = 403,
                ["recYards"] = 217, ["recTD"] = 1, ["receptions"] = 34,
            },
        };

        var choice = Selector().Select("HB", player, evidence);
        var ratings = engine.Generate("HB", choice.Archetype, player, evidence);

        // The reported case: 30 in all three, for a player the selector had
        // already identified as catching passes for a living.
        foreach (var attribute in new[]
                 {
                     "ShortRouteRunningRating", "MediumRouteRunningRating", "DeepRouteRunningRating",
                 })
        {
            Assert.True(ratings.Attributes[attribute] > 40,
                $"{attribute} generated at {ratings.Attributes[attribute]} for a back with 34 catches");
        }

        // And it is still a running back: the archetype, not a blanket raise.
        Assert.True(ratings.Attributes["CarryingRating"] >= 80);
        Assert.True(ratings.Attributes["BCVisionRating"] >= 80);
    }

    [Fact]
    public void AQuarterbackWhoRanCanCarryTheBall()
    {
        var engine = Engine();
        var player = new HistoricalPlayer
        {
            FirstName = "Scrambling", LastName = "Quarterback", Position = "QB",
            ClassYear = "Senior", HeightInches = 73, WeightPounds = 212,
        };
        var evidence = new RatingEvidence
        {
            Role = "Starter",
            DraftRound = 5,
            Awards = new List<string> { "first-team all-conference" },
            Stats = new Dictionary<string, double>
            {
                ["passYards"] = 2790, ["passTD"] = 20, ["completions"] = 178, ["attempts"] = 281,
                ["rushYards"] = 485, ["rushTD"] = 7, ["rushAttempts"] = 96,
            },
        };

        var choice = Selector().Select("QB", player, evidence);
        var ratings = engine.Generate("QB", choice.Archetype, player, evidence);

        // The reported case: a quarterback whose running was most of his value,
        // generated at 35 break tackle.
        Assert.True(ratings.Attributes["BreakTackleRating"] >= 60,
            $"BreakTackleRating {ratings.Attributes["BreakTackleRating"]} for a 485-yard rushing quarterback");
        Assert.True(ratings.Attributes["CarryingRating"] >= 70);
        Assert.True(ratings.Attributes["BCVisionRating"] >= 75);
    }

    [Fact]
    public void ProductionRaisesTheAttributesItWasEarnedWith()
    {
        var engine = Engine();
        var selector = Selector();
        var player = new HistoricalPlayer
        {
            FirstName = "Dual", LastName = "Threat", Position = "QB",
            ClassYear = "Junior", HeightInches = 73, WeightPounds = 215,
        };

        var passingOnly = new RatingEvidence
        {
            Role = "Starter",
            Stats = new Dictionary<string, double>
            {
                ["passYards"] = 3200, ["passTD"] = 26, ["completions"] = 230, ["attempts"] = 350,
            },
        };
        var alsoRan = new RatingEvidence
        {
            Role = "Starter",
            Stats = new Dictionary<string, double>(passingOnly.Stats)
            {
                ["rushYards"] = 1100, ["rushTD"] = 12, ["rushAttempts"] = 170,
            },
        };

        var pocket = engine.Generate(
            "QB", selector.Select("QB", player, passingOnly).Archetype, player, passingOnly);
        var runner = engine.Generate(
            "QB", selector.Select("QB", player, alsoRan).Archetype, player, alsoRan);

        // Before this, the two tied on overall AND on ball carrying: the stats
        // were collapsed into one number and the second role vanished.
        Assert.True(runner.Overall > pocket.Overall,
            $"1,100 rushing yards changed nothing: both quarterbacks came out {runner.Overall}");
        Assert.True(runner.Attributes["BreakTackleRating"] > pocket.Attributes["BreakTackleRating"]);
        Assert.True(runner.Attributes["CarryingRating"] > pocket.Attributes["CarryingRating"]);
    }

    [Fact]
    public void TheEmphasisPassOnlyEverRaisesAnAttribute()
    {
        // Historical rosters arrive with whatever box scores survived. A 1968
        // receiver whose numbers nobody kept must not be marked down for the
        // gap, so this pass is one-directional by construction: shaping a
        // player DOWNWARD is the archetype's job, not production's.
        //
        // (The talent score is a different question and still reads a thin
        // stat line as thin — three catches for 41 yards is evidence of a
        // marginal receiver, and it lowers the overall accordingly.)
        var model = RatingModelSet.Load(FixturePath("RatingModels.json"));
        var emphasis = new ProductionEmphasis(model);
        var profiles = ArchetypeProfileSet.Load(FixturePath("ArchetypeProfiles.json"));
        Assert.False(emphasis.IsEmpty);

        var statLines = new[]
        {
            new Dictionary<string, double>(),
            new Dictionary<string, double> { ["receptions"] = 3, ["recYards"] = 41 },
            new Dictionary<string, double> { ["rushYards"] = 12, ["rushAttempts"] = 9, ["rushTD"] = 0 },
            new Dictionary<string, double> { ["tackles"] = 4, ["sacks"] = 0, ["interceptions"] = 0 },
            new Dictionary<string, double> { ["recYards"] = 1721, ["receptions"] = 118, ["recTD"] = 14 },
        };

        var checkedAttributes = 0;
        foreach (var group in model.ProductionEmphasis.Groups.Keys)
        {
            foreach (var archetype in profiles.Archetypes)
            {
                var profile = profiles.Find(archetype)!;
                foreach (var stats in statLines)
                {
                    var before = model.AttributeDefaults.ToDictionary(
                        a => a.Key, a => a.Value, StringComparer.Ordinal);
                    var after = new Dictionary<string, double>(before, StringComparer.Ordinal);
                    emphasis.Apply(
                        emphasis.Score(group, stats), profile, after,
                        new HashSet<string>(StringComparer.Ordinal), new List<string>());

                    foreach (var (attribute, value) in after)
                    {
                        checkedAttributes++;
                        Assert.True(value >= before[attribute],
                            $"{group}/{archetype}: {attribute} fell from {before[attribute]} to {value}");
                    }
                }
            }
        }

        Assert.True(checkedAttributes > 1000, $"only {checkedAttributes} attributes were checked");
    }

    [Fact]
    public void TheEngineStillRunsWithoutTheMeasuredProfiles()
    {
        // The profiles are generated from an export, so the engine must not
        // depend on them being present — it falls back to the written baseline.
        var engine = RatingEngine.Load(FixturePath("RatingModels.json"), FixturePath("OverallFormulas.json"));
        Assert.Null(engine.Profiles);

        var player = new HistoricalPlayer
        {
            FirstName = "No", LastName = "Profile", Position = "HB",
            ClassYear = "Junior", HeightInches = 71, WeightPounds = 205,
        };
        var ratings = engine.Generate("HB", "HB_ElusiveBack", player, new RatingEvidence { Role = "Starter" });
        Assert.InRange(ratings.Overall, 40, 99);
    }
}
