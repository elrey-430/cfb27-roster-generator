using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Players the roster file gives nothing but a role must not all come out on
/// the same number.
///
/// <para><b>What was wrong.</b> A generated roster came out in spikes where the
/// game's is a curve. On the 2023 Florida State file — 64 of whose 75 rows
/// carry no stats, no award and no draft slot, eleven of them the identical
/// "Reserve, redshirt freshman" — 18 players landed on exactly 78 and 25 on
/// exactly 68. EA's own Florida State puts three to nine players on each value
/// from 69 to 84.</para>
///
/// <para><b>Why no role score could fix it.</b> The game spreads 14 points
/// inside its starters (73 at the 10th percentile, 87 at the 90th) and 8 to 9
/// inside every other role. Class year does not explain that spread either:
/// measured across 11,730 players on 138 teams it moves the median within a
/// role by a single point, four for starters.</para>
///
/// <para>So the distribution is reproduced without claiming to know which
/// player is which — the same thing <c>RosterFiller</c> has always done for
/// empty slots.</para>
/// </summary>
public sealed class RoleSpreadTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private static RosterGenerationResult Generate(TempDirectory folder, string rosterPath) =>
        new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = rosterPath,
            DataDirectory = DataDirectory,
            Team = "Florida State",
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            Ratings = RatingsMode.Generate,
            FillRoster = false,
        });

    /// <summary>A roster of players separated by nothing but their role.</summary>
    private static string RoleOnlyRoster(TempDirectory folder, int perRole)
    {
        var lines = new List<string> { "FirstName,LastName,Position,Class,Role,Team,Season" };
        foreach (var (role, tag) in new[] { ("Starter", "S"), ("Backup", "B"), ("Reserve", "R") })
        {
            for (var i = 0; i < perRole; i++)
            {
                lines.Add($"{tag}{i},Player,WR,Senior,{role},Florida State,2014");
            }
        }

        var path = folder.File("roleonly.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        return path;
    }

    private static List<int> Overalls(string outputPath, string lastName = "Player")
    {
        var table = CsvDocument.Load(outputPath);
        var overalls = new List<int>();
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.LastName) == lastName &&
                table.GetCell(row, PlayerColumns.IsEmpty) == "false")
            {
                overalls.Add(int.Parse(table.GetCell(row, PlayerColumns.OverallRating)));
            }
        }

        return overalls;
    }

    // ---- The defect, stated directly ---------------------------------------

    [Fact]
    public void PlayersWithNothingButARoleDoNotAllLandOnOneNumber()
    {
        using var folder = new TempDirectory();
        var result = Generate(folder, RoleOnlyRoster(folder, perRole: 8));

        var overalls = Overalls(result.OutputPath);
        Assert.True(overalls.Count >= 20, $"only {overalls.Count} players were generated.");

        var biggestPile = overalls.GroupBy(o => o).Max(g => g.Count());
        Assert.True(biggestPile < overalls.Count / 2,
            $"{biggestPile} of {overalls.Count} players came out on the same overall.");
        Assert.True(overalls.Distinct().Count() >= 5,
            $"only {overalls.Distinct().Count()} distinct overalls across {overalls.Count} players.");
    }

    [Fact]
    public void TheSpreadStaysInsideTheRangeTheGameCarriesForThatRole()
    {
        using var folder = new TempDirectory();
        var result = Generate(folder, RoleOnlyRoster(folder, perRole: 8));

        // Measured 10th-to-90th percentile bands, with a point of slack for the
        // program adjustment and integer settling.
        var overalls = Overalls(result.OutputPath);
        Assert.All(overalls, o => Assert.InRange(o, 55, 90));
    }

    [Fact]
    public void TheSameFileAlwaysProducesTheSameRoster()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        var roster = RoleOnlyRoster(first, perRole: 8);

        var a = Overalls(Generate(first, roster).OutputPath);
        var b = Overalls(Generate(second, roster).OutputPath);

        // The order within a role is decided by evidence and then by name, never
        // by chance, so a user re-running the same file gets the same result.
        Assert.Equal(a, b);
    }

    // ---- What it must not touch --------------------------------------------

    [Fact]
    public void APlayerWithARecordOfTheirOwnIsLeftAlone()
    {
        using var folder = new TempDirectory();
        var path = folder.File("mixed.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine, new[]
        {
            "FirstName,LastName,Position,Class,Role,Team,Season,RecYards,RecTD,Receptions,DraftRound,DraftPick",
            "Star,Receiver,WR,Senior,Starter,Florida State,2014,1400,14,80,1,12",
            "Aa,Player,WR,Senior,Starter,Florida State,2014,,,,,",
            "Bb,Player,WR,Senior,Starter,Florida State,2014,,,,,",
            "Cc,Player,WR,Senior,Starter,Florida State,2014,,,,,",
            "Dd,Player,WR,Senior,Starter,Florida State,2014,,,,,",
        }) + Environment.NewLine);

        var result = Generate(folder, path);

        // The drafted receiver has a number of their own; the spread is for
        // players the file says nothing about, and moving him would be throwing
        // away the only real evidence on the roster.
        var star = Overalls(result.OutputPath, "Receiver").Single();
        Assert.True(star >= 90, $"the first-round receiver came out at {star}.");
    }

    [Fact]
    public void OneOfAKindIsNotMoved()
    {
        // A single player in a role is not a pile. Spreading them would move
        // them off their own blended number for no reason.
        var rated = new[] { Rated("Only", "Starter") };
        var moves = RoleSpread.Plan(rated, Model);

        Assert.Empty(moves);
    }

    [Fact]
    public void EvidenceOtherThanARoleMakesAPlayerIneligible()
    {
        Assert.False(RoleSpread.IsUndifferentiated(Rated("Drafted", "Backup",
            new RatingEvidence { Role = "Backup", DraftPickOverall = 40 })));
        Assert.False(RoleSpread.IsUndifferentiated(Rated("Honoured", "Backup",
            new RatingEvidence { Role = "Backup", Awards = new[] { "Heisman" } })));

        // A recruiting rating is shared by whole classes of players and does
        // not separate them, so it does not disqualify one from the spread.
        Assert.True(RoleSpread.IsUndifferentiated(Rated("Recruit", "Backup",
            new RatingEvidence { Role = "Backup", StarRating = 4 })));
    }

    private static RatingModelSet Model =>
        RatingModelSet.Load(Path.Combine(DataDirectory, "RatingModels.json"));

    private static RatedPlayer Rated(string name, string role, RatingEvidence? evidence = null)
    {
        var player = new HistoricalPlayer
        {
            FirstName = name,
            LastName = "Test",
            Position = "WR",
            ClassYear = "Senior",
            Evidence = evidence ?? new RatingEvidence { Role = role },
        };

        var ratings = TestFixtures.RatingEngine().Generate("WR", "WR_PhysicalReceiver", player, player.Evidence);
        return new RatedPlayer(player, "WR", ratings.PlayerType, ratings);
    }
}
