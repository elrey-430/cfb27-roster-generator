using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Where a player goes is decided by their own <c>Team</c> cell.
///
/// <para>Reported: a roster covering every team could not be generated,
/// because the run was limited to one selected team. It was worse than a
/// limit — the desktop app sent the team it had detected on <em>every</em>
/// run, and an explicit team used to win over each row's own, so a
/// whole-season file silently collapsed onto whichever school appeared
/// first. 10,115 players, one team, nothing reported.</para>
///
/// <para>The caller's team is now a fallback for rows that name none, and
/// nothing more.</para>
/// </summary>
public sealed class TeamAssignmentTests
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

    private static string ThreeTeams(TempDirectory folder)
    {
        var path = folder.File("three.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine, new[]
        {
            "FirstName,LastName,Position,Team,Season",
            "Aa,Alpha,QB,Florida State,2014",
            "Bb,Bravo,HB,Florida State,2014",
            "Cc,Charlie,QB,Alabama,2014",
            "Dd,Delta,WR,Alabama,2014",
            "Ee,Echo,QB,Michigan,2014",
        }) + Environment.NewLine);
        return path;
    }

    // ---- The bug, stated directly ------------------------------------------

    [Fact]
    public void AnExplicitTeamDoesNotSwallowAFileThatNamesItsOwn()
    {
        using var folder = new TempDirectory();

        var result = HistoricalCsv.Read(ThreeTeams(folder), school: "Florida State");

        // Before, every one of the five landed on Florida State.
        Assert.Equal(3, result.Rosters.Count);
        Assert.Equal(
            new[] { "Alabama", "Florida State", "Michigan" },
            result.Rosters.Select(r => r.School).OrderBy(s => s, StringComparer.Ordinal));
    }

    [Fact]
    public void EachPlayerGoesToTheTeamTheirOwnCellNames()
    {
        using var folder = new TempDirectory();

        var result = HistoricalCsv.Read(ThreeTeams(folder), school: "Michigan");
        var bySchool = result.Rosters.ToDictionary(r => r.School, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(2, bySchool["Florida State"].Players.Count);
        Assert.Equal(2, bySchool["Alabama"].Players.Count);
        Assert.Single(bySchool["Michigan"].Players);
        Assert.Equal("Charlie", bySchool["Alabama"].Players[0].LastName);
    }

    // ---- The fallback still works ------------------------------------------

    [Fact]
    public void AFileWithNoTeamColumnUsesTheTeamTheCallerGives()
    {
        using var folder = new TempDirectory();
        var path = folder.File("noteam.csv");
        File.WriteAllText(path,
            "FirstName,LastName,Position,Season\nGg,Golf,QB,2014\nHh,Hotel,WR,2014\n");

        var result = HistoricalCsv.Read(path, school: "Florida State");

        Assert.Single(result.Rosters);
        Assert.Equal("Florida State", result.Roster.School);
        Assert.Equal(2, result.Roster.Players.Count);
    }

    [Fact]
    public void RowsThatNameNoTeamFallBackWhileRowsThatDoAreKept()
    {
        using var folder = new TempDirectory();
        var path = folder.File("mixed.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine, new[]
        {
            "FirstName,LastName,Position,Team,Season",
            "Aa,Alpha,QB,Alabama,2014",
            "Bb,Bravo,HB,,2014",
        }) + Environment.NewLine);

        var result = HistoricalCsv.Read(path, school: "Michigan");
        var bySchool = result.Rosters.ToDictionary(r => r.School, StringComparer.OrdinalIgnoreCase);

        // A half-filled Team column is a real thing in a hand-made file, and
        // neither half should be lost to the other.
        Assert.Equal("Alpha", bySchool["Alabama"].Players.Single().LastName);
        Assert.Equal("Bravo", bySchool["Michigan"].Players.Single().LastName);
    }

    [Fact]
    public void WithNoTeamAnywhereItStillRefusesRatherThanGuessing()
    {
        using var folder = new TempDirectory();
        var path = folder.File("nothing.csv");
        File.WriteAllText(path, "FirstName,LastName,Position\nGg,Golf,QB\n");

        Assert.Throws<Csv.CsvSchemaException>(() => HistoricalCsv.Read(path));
    }

    // ---- End to end --------------------------------------------------------

    [Fact]
    public void GeneratingAMultiTeamFileWithATeamSetStillWritesEveryTeam()
    {
        using var folder = new TempDirectory();
        var path = folder.File("two.csv");

        // Both teams exist in the donor fixture's Team table, so both convert.
        File.WriteAllText(path, string.Join(Environment.NewLine, new[]
        {
            "FirstName,LastName,Position,Team,Season",
            "Aa,Alpha,QB,Florida State,2014",
            "Bb,Bravo,HB,Florida State,2014",
            "Cc,Charlie,QB,Alabama,2014",
        }) + Environment.NewLine);

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = path,
            DataDirectory = DataDirectory,
            Team = "Florida State",
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
        });

        Assert.True(result.Teams.Count >= 2,
            $"a team override collapsed the file to {result.Teams.Count} team(s).");
        Assert.Contains(result.Teams, t => t.Source.School == "Alabama");
        Assert.Contains(result.Teams, t => t.Source.School == "Florida State");
    }

    [Fact]
    public void APlayerFromAnotherTeamIsNeverWrittenOntoTheChosenOne()
    {
        using var folder = new TempDirectory();
        var path = folder.File("two.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine, new[]
        {
            "FirstName,LastName,Position,Team,Season",
            "Aa,Alpha,QB,Florida State,2014",
            "Cc,Charlie,QB,Alabama,2014",
        }) + Environment.NewLine);

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = path,
            DataDirectory = DataDirectory,
            Team = "Florida State",
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
        });

        // The donor fixture carries roster slots for Florida State only, so
        // Alabama's player has nowhere to go and is reported as skipped. What
        // must never happen is the old behaviour: him quietly appearing on
        // Florida State because that was the team passed in.
        var table = Csv.CsvDocument.Load(result.OutputPath);
        var written = new List<string>();
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.LastName) is "Alpha" or "Charlie")
            {
                written.Add(
                    $"{table.GetCell(row, PlayerColumns.LastName)}@{table.GetCell(row, PlayerColumns.TeamIndex)}");
            }
        }

        Assert.Contains("Alpha@27", written);
        Assert.DoesNotContain("Charlie@27", written);
        Assert.Contains(result.Teams, t => t.Source.School == "Alabama" && t.Skipped.Any());
    }
}
