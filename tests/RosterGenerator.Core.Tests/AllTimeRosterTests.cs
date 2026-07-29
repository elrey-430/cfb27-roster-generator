using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// All-time rosters: one team, one row per player, and a different year on
/// every row.
///
/// <para>They are clearly something people build — the first one reported was
/// an All-Time USC squad — and until now the tool had to pick a single season
/// out of the file and apply it to everybody. It picked whichever year was
/// typed first, so a fifty-year span of players all ended up in one era's
/// helmets: on that USC file, 1980's Riddell TKs for the whole team including
/// Reggie Bush.</para>
///
/// <para>A player's own <c>Season</c> now picks their own equipment era. The
/// roster still has one season, because the report heading and anything
/// roster-wide needs one, and a player who gives no year of their own still
/// takes it.</para>
/// </summary>
public sealed class AllTimeRosterTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            Directory.CreateDirectory(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>A one-team roster whose players come from four decades.</summary>
    private static string WriteAllTimeRoster(TempDirectory folder)
    {
        var path = folder.File("alltime.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine, new[]
        {
            "FirstName,LastName,Position,Team,Season,HeightInches,WeightPounds,Role",
            "Early,Tackle,LT,Florida State,1975,77,265,Starter",
            "Eighties,Back,HB,Florida State,1985,70,205,Starter",
            "Nineties,Receiver,WR,Florida State,1995,73,190,Starter",
            "Modern,Quarterback,QB,Florida State,2014,74,215,Starter",
        }) + Environment.NewLine);
        return path;
    }

    // ---- Reading -----------------------------------------------------------

    [Fact]
    public void EachRowKeepsItsOwnSeason()
    {
        using var folder = new TempDirectory();

        var result = HistoricalCsv.Read(WriteAllTimeRoster(folder));

        Assert.Equal(new int?[] { 1975, 1985, 1995, 2014 },
            result.Roster.Players.Select(p => p.Season));
    }

    [Fact]
    public void TheRosterStillCarriesOneSeasonForEverythingRosterWide()
    {
        using var folder = new TempDirectory();

        var result = HistoricalCsv.Read(WriteAllTimeRoster(folder));

        // The report heading, the FBS membership check and the filled depth
        // slots all need a single answer, and the file's first year is it.
        Assert.Equal(1975, result.Roster.Season);
    }

    [Fact]
    public void AnExplicitSeasonOverridesEveryRow()
    {
        using var folder = new TempDirectory();

        var result = HistoricalCsv.Read(WriteAllTimeRoster(folder), season: 1999);

        // "Treat this file as 1999" has to mean all of it. Leaving the rows in
        // charge would put half the squad in the year the user just overrode.
        Assert.All(result.Roster.Players, p => Assert.Null(p.Season));
        Assert.Equal(1999, result.Roster.Season);
    }

    [Fact]
    public void ARowWithNoSeasonFallsBackToTheRosters()
    {
        using var folder = new TempDirectory();
        var path = folder.File("mixed.csv");
        File.WriteAllText(path,
            "FirstName,LastName,Position,Team,Season\n" +
            "Dated,Player,QB,Florida State,1988\n" +
            "Undated,Player,HB,Florida State,\n");

        var players = HistoricalCsv.Read(path).Roster.Players;

        Assert.Equal(1988, players[0].Season);
        Assert.Null(players[1].Season);
    }

    // ---- What the players end up wearing -----------------------------------

    [Fact]
    public void PlayersFromDifferentDecadesGetDifferentEras()
    {
        using var folder = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("EquipmentDynasty"),
            RosterPath = WriteAllTimeRoster(folder),
            DataDirectory = DataDirectory,
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            EquipmentOutputPath = folder.File("equipment.csv"),
        });

        var equipment = result.Equipment;
        Assert.NotNull(equipment);

        // The bug this closes: one era for the whole squad. Four players from
        // 1975, 1985, 1995 and 2014 span four of the five eras, and the fifth
        // is the team season the filled depth slots take.
        Assert.True(equipment!.Seasons.Count > 1,
            $"only one season was applied: {string.Join(", ", equipment.Seasons)}");
        Assert.Contains(2014, equipment.Seasons);
        Assert.Contains(1975, equipment.Seasons);
        Assert.True(equipment.EraNames.Count > 1,
            $"only one era was applied: {string.Join(", ", equipment.EraNames)}");
    }

    [Fact]
    public void TheReportSaysEachPlayerIsInTheirOwnSeasonsGear()
    {
        using var folder = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("EquipmentDynasty"),
            RosterPath = WriteAllTimeRoster(folder),
            DataDirectory = DataDirectory,
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            EquipmentOutputPath = folder.File("equipment.csv"),
        });

        // Worth saying out loud: on an all-time roster this is the difference
        // between a squad that looks right and one nobody can explain.
        Assert.Contains("own season's gear", result.Equipment!.Describe());
    }

    [Fact]
    public void OneTeamIsReportedAsOneTeamHoweverManySeasonsItSpans()
    {
        using var folder = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("EquipmentDynasty"),
            RosterPath = WriteAllTimeRoster(folder),
            DataDirectory = DataDirectory,
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            EquipmentOutputPath = folder.File("equipment.csv"),
        });

        // Merging a whole season's reports sums their teams, because those
        // parts are disjoint schools. An all-time roster's parts are seasons of
        // the same school, and summing them said "across 5 teams" about one.
        Assert.Equal(1, result.Equipment!.TeamCount);
        Assert.DoesNotContain("teams", result.Equipment.Describe());
    }

    [Fact]
    public void AnOrdinarySingleSeasonRosterIsUnaffected()
    {
        using var folder = new TempDirectory();
        var path = folder.File("one-season.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine, new[]
        {
            "FirstName,LastName,Position,Team,Season",
            "One,Tackle,LT,Florida State,2014",
            "Two,Back,HB,Florida State,2014",
            "Three,Receiver,WR,Florida State,2014",
        }) + Environment.NewLine);

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("EquipmentDynasty"),
            RosterPath = path,
            DataDirectory = DataDirectory,
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            EquipmentOutputPath = folder.File("equipment.csv"),
        });

        // Every row says 2014, so there is one era and the wording stays the
        // simple one. Per-player seasons must cost the common case nothing.
        Assert.Equal(new[] { 2014 }, result.Equipment!.Seasons);
        Assert.DoesNotContain("own season's gear", result.Equipment.Describe());
    }

    [Fact]
    public void FilledDepthSlotsTakeTheTeamsSeasonNotAModernHelmet()
    {
        using var folder = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("EquipmentDynasty"),
            RosterPath = WriteAllTimeRoster(folder),
            DataDirectory = DataDirectory,
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            EquipmentOutputPath = folder.File("equipment.csv"),
        });

        // A slot the roster never named has no season of its own, and leaving
        // it in the game's present-day gear would put 81 modern helmets around
        // four period ones.
        var teamSeason = result.Conversion.Source.Season;
        Assert.Contains(teamSeason, result.Equipment!.Seasons);

        var rehelmeted = result.Equipment.Changed.Count + result.Equipment.AlreadyCorrect;
        Assert.True(rehelmeted > result.Converted,
            $"only {rehelmeted} players were considered; the whole team should be.");
    }

    // ---- The rest of the roster is untouched -------------------------------

    [Fact]
    public void SeasonNeverReachesThePlayerTable()
    {
        using var folder = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("EquipmentDynasty"),
            RosterPath = WriteAllTimeRoster(folder),
            DataDirectory = DataDirectory,
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            EquipmentOutputPath = folder.File("equipment.csv"),
        });

        // A season is not a player field. It selects equipment and titles the
        // report; nothing in the save records which year somebody came from,
        // so per-row seasons must not have grown a column to write it into.
        var table = Csv.CsvDocument.Load(result.OutputPath);
        Assert.DoesNotContain("Season", table.Header);
        Assert.Contains(PlayerColumns.TeamIndex, table.Header);
    }
}
