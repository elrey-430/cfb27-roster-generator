using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Scores a generated roster against the one the game itself ships for the
/// same team.
///
/// Individual ratings can be argued about; the *shape* of a roster cannot.
/// EA's own Florida State carries 85 players running from 92 down to 64, and
/// a recreation of the same program should look like that. Benchmarking
/// against it found the defect this file now guards: with no evidence beyond
/// a depth-chart role, generated players were rated from league-average role
/// scores, so the middle of the roster collapsed — 69 at rank 30 where the
/// game carries 76. Mean absolute deviation from the game's curve was 4.48
/// before the program adjustment and 2.01 after.
/// </summary>
public sealed class RosterFidelityTests
{
    /// <summary>
    /// Mean absolute deviation, rank by rank, allowed between the generated
    /// roster and the game's own. The manual human recreation of the same
    /// roster scores 3.02 on this measure, so the bar is set inside it.
    /// </summary>
    private const double MaxMeanAbsoluteDeviation = 3.0;

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static List<int> Curve(IEnumerable<Core.Model.Player> players, int teamId) =>
        players.Where(p => p.TeamIndex == teamId)
            .Select(p => p.OverallRating)
            .OrderByDescending(o => o)
            .ToList();

    [Fact]
    public void GeneratedRosterMatchesTheShapeOfTheGamesOwnRoster()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        var donor = export.LoadPlayerRoster();

        // The donor fixture is EA's untouched Florida State roster, so its own
        // curve is the reference. Capture it before anything is written.
        var reference = Curve(donor.Players, 27);
        Assert.Equal(85, reference.Count);

        var session = new RosterEditSession(donor);
        var engine = TestFixtures.RatingEngine();
        var depth = RosterDepthModel.Load(FixturePath("RosterDepth.json"));
        new HistoricalTeamConverter(
                export.BuildTeamMappings(),
                PositionMappingSet.Load(FixturePath("PositionMappings.json")),
                engine,
                ArchetypeSelector.Load(FixturePath("ArchetypeRules.json")),
                new RosterFiller(depth, engine),
                depth)
            .Convert(session, csv.Roster);

        var generated = Curve(donor.Players, 27);
        Assert.Equal(reference.Count, generated.Count);

        var deviation = Enumerable.Range(0, reference.Count)
            .Average(i => Math.Abs(generated[i] - reference[i]));

        Assert.True(
            deviation <= MaxMeanAbsoluteDeviation,
            $"Generated roster deviates from the game's own Florida State by {deviation:0.00} points per " +
            $"rank, above the {MaxMeanAbsoluteDeviation:0.00} bar. Rank 30: generated {generated[29]}, " +
            $"game {reference[29]}. Rank 60: generated {generated[59]}, game {reference[59]}.");
    }

    [Fact]
    public void ProgramStandingLiftsThinlyEvidencedPlayersButNotWellEvidencedOnes()
    {
        var engine = TestFixtures.RatingEngine();
        var player = new HistoricalPlayer
        {
            FirstName = "Anonymous", LastName = "Backup", Position = "CB",
            ClassYear = "Junior", HeightInches = 71, WeightPounds = 190,
        };

        var thin = new RatingEvidence { Role = "Backup" };
        var atAverage = engine.Generate("CB", null, player, thin);
        var atPowerhouse = engine.Generate("CB", null, player, thin, programAdjustment: 8);

        // Role, awards and statistics say what a player did, never where.
        Assert.True(
            atPowerhouse.Overall > atAverage.Overall,
            $"program adjustment did not move a thinly evidenced player ({atAverage.Overall} vs " +
            $"{atPowerhouse.Overall})");

        // A first-round pick is rated on their own record, not their school's.
        var strong = new RatingEvidence
        {
            Role = "Starter", DraftRound = 1, DraftPickOverall = 5,
            Awards = new List<string> { "consensus all-american" },
        };
        var starAtAverage = engine.Generate("CB", null, player, strong);
        var starAtPowerhouse = engine.Generate("CB", null, player, strong, programAdjustment: 8);
        Assert.Equal(starAtAverage.Overall, starAtPowerhouse.Overall);
    }

    [Fact]
    public void NoPlayerIsGeneratedBetterThanTheBestOneTheGameCarriesAtThatPosition()
    {
        var models = RatingModelSet.Load(FixturePath("RatingModels.json"));
        var engine = TestFixtures.RatingEngine();

        // Award and draft scores share one scale across every position, but the
        // game's positions do not share a range: its best punter is an 86 and
        // its best receiver a 99. Before the cap, a nation-leading All-American
        // punter generated at 91.
        var punter = new HistoricalPlayer
        {
            FirstName = "Elite", LastName = "Punter", Position = "P",
            ClassYear = "Senior", HeightInches = 74, WeightPounds = 220,
        };
        var evidence = new RatingEvidence
        {
            Role = "Starter",
            Awards = new List<string> { "unanimous all-american" },
            Stats = new Dictionary<string, double> { ["puntAverage"] = 49.3 },
        };

        var ratings = engine.Generate("P", null, punter, evidence);
        var cap = models.PositionOverallCaps["P"];
        Assert.True(ratings.Overall <= cap, $"punter generated at {ratings.Overall}, above the game's best ({cap})");

        // The cap is the observed maximum, not a haircut: a genuinely elite
        // specialist still reaches the top of their own position.
        Assert.True(ratings.Overall >= cap - 2, $"punter generated at {ratings.Overall}, far below the {cap} cap");
    }
}
