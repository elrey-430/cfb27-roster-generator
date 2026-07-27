using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Pipeline;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Preparing a whole season at once: the blank template that goes out, and the
/// filled file that comes back carrying every team in one go.
///
/// The reason this is worth its own suite is that a season file makes two
/// silent failures possible that a one-team file cannot. It can quietly
/// include schools that were still in the FCS that year — CFB27 carries
/// today's 138 teams and gives no other sign — and it can quietly convert only
/// the first team of 119 while reporting success.
/// </summary>
public sealed class SeasonTests
{
    private static string DataPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    private static string TemplatePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "HistoricalRosterTemplate.csv");

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static FbsMembership Membership() => FbsMembership.Load(DataPath("FbsMembership.json"));

    private static SeasonTemplateWriter Writer() =>
        SeasonTemplateWriter.Load(DataPath("RosterSkeleton.json"));

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            Directory.CreateDirectory(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    // ---- Membership -------------------------------------------------------

    [Fact]
    public void ASchoolIsNotEligibleBeforeItReachedTheFbs()
    {
        var membership = Membership();

        // The user's own example. Sacramento State is one of CFB27's 138 teams
        // and was in the FCS for the whole of 2010.
        var problem = membership.Check("Sac State", 2010);
        Assert.NotNull(problem);
        Assert.Contains("2026", problem!.Reason);
        Assert.Equal("Sac State", problem.School);

        Assert.True(membership.Eligible("Sac State", 2026));
        Assert.False(membership.Eligible("UCF", 1995));
        Assert.True(membership.Eligible("UCF", 1996));
    }

    [Fact]
    public void ASchoolWithNoRecordedTransitionIsAlwaysEligible()
    {
        // Alabama is not in the file at all, and must not therefore be read as
        // "never eligible" — the file records changes, not permission.
        Assert.True(Membership().Eligible("Alabama", 1985));
        Assert.True(Membership().Eligible("Some School That Does Not Exist", 1985));
    }

    [Fact]
    public void AGapInMembershipIsHonouredNotJustTheFirstSeason()
    {
        // UAB reached the FBS in 1996 and then had no team for two years.
        var membership = Membership();
        Assert.True(membership.Eligible("UAB", 2014));
        Assert.False(membership.Eligible("UAB", 2015));
        Assert.False(membership.Eligible("UAB", 2016));
        Assert.True(membership.Eligible("UAB", 2017));
    }

    [Fact]
    public void AnEmptyMembershipSetLetsEveryTeamThrough()
    {
        // The escape hatch: a user whose season the tool has no dates for must
        // not be blocked by a file this project wrote.
        Assert.True(FbsMembership.Empty.Eligible("Sac State", 1910));
    }

    [Fact]
    public void TheSeasonIsRequiredBeforeMembershipIsJudged()
    {
        // A roster with no Season says nothing about eligibility, so the
        // question is not asked rather than answered wrongly.
        Assert.Null(Membership().Check("Sac State", 0));
    }

    // ---- The blank season template ---------------------------------------

    [Fact]
    public void TheTemplateGivesEveryEligibleTeamAFullRoster()
    {
        using var temp = new TempDirectory();
        var teams = new[] { "Alabama", "Florida State", "Sac State", "UCF" };
        var result = Writer().Write(temp.File("season.csv"), TemplatePath, teams, 2010, Membership());

        Assert.Equal(3, result.Teams);
        Assert.Equal(85, result.SlotsPerTeam);
        Assert.Equal(3 * 85, result.Rows);

        var written = CsvDocument.Load(result.Path);
        Assert.Equal(result.Rows, written.RowCount);

        // The header is the shipped template's, copied rather than restated,
        // so the blank file and the documented format cannot drift apart.
        Assert.Equal(CsvDocument.Load(TemplatePath).Header, written.Header);

        var byTeam = Enumerable.Range(0, written.RowCount)
            .GroupBy(i => written.GetCell(i, "Team"))
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, byTeam.Count);
        Assert.All(byTeam.Values, count => Assert.Equal(85, count));
        Assert.DoesNotContain("Sac State", byTeam.Keys);
    }

    [Fact]
    public void TheTemplateSaysWhichTeamsItLeftOutAndWhy()
    {
        using var temp = new TempDirectory();
        var result = Writer().Write(
            temp.File("season.csv"), TemplatePath, new[] { "Alabama", "Sac State" }, 2010, Membership());

        // Silently dropping a team the user asked for would be the worst
        // possible behaviour: they would never know it was missing.
        var excluded = Assert.Single(result.Excluded);
        Assert.Equal("Sac State", excluded.School);
        Assert.Equal(2010, excluded.Season);
        Assert.Contains("2026", excluded.Reason);
    }

    [Fact]
    public void EveryTemplateRowCarriesATeamASeasonAndAPositionAndNothingElse()
    {
        using var temp = new TempDirectory();
        var result = Writer().Write(
            temp.File("season.csv"), TemplatePath, new[] { "Alabama" }, 2010, FbsMembership.Empty);

        var written = CsvDocument.Load(result.Path);
        var prefilled = new[] { "Team", "Season", "Position" };
        for (var row = 0; row < written.RowCount; row++)
        {
            Assert.Equal("Alabama", written.GetCell(row, "Team"));
            Assert.Equal("2010", written.GetCell(row, "Season"));
            Assert.NotEqual("", written.GetCell(row, "Position"));

            // Anything else pre-filled would be this tool inventing a player.
            foreach (var column in written.Header.Except(prefilled))
            {
                Assert.Equal("", written.GetCell(row, column));
            }
        }
    }

    [Fact]
    public void TheSkeletonCoversAWholeRosterWithPositionsTheToolCanRead()
    {
        using var temp = new TempDirectory();
        var result = Writer().Write(
            temp.File("season.csv"), TemplatePath, new[] { "Alabama" }, 2010, FbsMembership.Empty);

        // A skeleton offering a position generation would then skip would hand
        // the user rows that quietly cannot be used.
        var positions = PositionMappingSet.Load(DataPath("PositionMappings.json"));
        var written = CsvDocument.Load(result.Path);
        for (var row = 0; row < written.RowCount; row++)
        {
            var position = written.GetCell(row, "Position");
            Assert.True(positions.TryResolve(position, out _), $"'{position}' is not a position the tool knows.");
        }
    }

    // ---- Reading a season file back --------------------------------------

    [Fact]
    public void AFileNamingSeveralTeamsIsReadAsSeveralRosters()
    {
        using var temp = new TempDirectory();
        var path = temp.File("multi.csv");
        File.WriteAllText(path, string.Join("\r\n",
            "FirstName,LastName,Position,Team,Season",
            "Jameis,Winston,QB,Florida State,2014",
            "Amari,Cooper,WR,Alabama,2014",
            "Marcus,Mariota,QB,Oregon,2014",
            "Rashad,Greene,WR,Florida State,2014",
            ""));

        var result = HistoricalCsv.Read(path);

        Assert.True(result.IsMultiTeam);
        Assert.Equal(3, result.Rosters.Count);

        // File order, so a user reading their spreadsheet top to bottom finds
        // the report's teams in the same order.
        Assert.Equal(new[] { "Florida State", "Alabama", "Oregon" }, result.Rosters.Select(r => r.School));
        Assert.Equal(2, result.Rosters[0].Players.Count);
        Assert.Equal(2014, result.Rosters[1].Season);

        // Roster still means "the first team", so single-team callers are
        // unaffected by a file that happens to carry more.
        Assert.Equal("Florida State", result.Roster.School);
    }

    [Fact]
    public void ASingleTeamFileIsNotReportedAsASeason()
    {
        var result = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        Assert.False(result.IsMultiTeam);
        Assert.Single(result.Rosters);
    }

    // ---- Validation -------------------------------------------------------

    [Fact]
    public void ValidationNotesATeamThatHadNotReachedTheFbsYet()
    {
        using var temp = new TempDirectory();
        var path = temp.File("early.csv");
        File.WriteAllText(path, string.Join("\r\n",
            "FirstName,LastName,Position,Team,Season",
            "A,Player,QB,Sac State,2010",
            ""));

        var report = RosterCsvValidator.Check(path, membership: Membership());

        var note = Assert.Single(report.OfSeverity(RosterCsvSeverity.Note)
            .Where(f => f.Message.Contains("FBS")));
        Assert.Contains("2026", note.Message);

        // Advisory, never a gate: the dates are this project's best reading of
        // the record, and the user may know better.
        Assert.True(report.CanGenerate);
    }

    [Fact]
    public void ValidationIsSilentAboutMembershipWhenItHasNoDates()
    {
        using var temp = new TempDirectory();
        var path = temp.File("early.csv");
        File.WriteAllText(path, string.Join("\r\n",
            "FirstName,LastName,Position,Team,Season",
            "A,Player,QB,Sac State,2010",
            ""));

        var report = RosterCsvValidator.Check(path);
        Assert.DoesNotContain(report.Findings, f => f.Message.Contains("FBS"));
    }

    [Fact]
    public void ValidationChecksEveryTeamInASeasonFileNotJustTheFirst()
    {
        using var temp = new TempDirectory();
        var path = temp.File("season.csv");
        File.WriteAllText(path, string.Join("\r\n",
            "FirstName,LastName,Position,Team,Season",
            "A,Player,QB,Alabama,2014",
            // The bad position is on the *second* team, which is exactly what a
            // validator that only read the first roster would miss.
            "B,Player,Quarterback Sneak Specialist,Oregon,2014",
            ""));

        var report = RosterCsvValidator.Check(
            path, PositionMappingSet.Load(DataPath("PositionMappings.json")));

        var warning = Assert.Single(report.OfSeverity(RosterCsvSeverity.Warning));
        Assert.Contains("Oregon", warning.ToString());
        Assert.Equal(2, report.Rosters.Count);
        Assert.Equal(2, report.UsablePlayers);
    }

    // ---- Converting a season ---------------------------------------------

    /// <summary>
    /// Builds a two-team donor from the one-team regression fixture, by copying
    /// its 85 real exported rows onto a second team with fresh row keys.
    ///
    /// Nothing is invented: every value in the copies came out of a real save,
    /// and only <c>TeamIndex</c> and <c>_row</c> — the two the tool itself
    /// writes — are changed. A fixture with two real teams would be better, but
    /// it would also be 27 MB.
    /// </summary>
    private static string TwoTeamDonor(TempDirectory temp, int secondTeamIndex)
    {
        var folder = Directory.CreateDirectory(temp.File("donor")).FullName;
        foreach (var file in Directory.GetFiles(TestsPath("DonorDynasty")))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }

        var players = Path.Combine(folder, "0152_Player.csv");
        var table = CsvDocument.Load(players);
        var rows = Enumerable.Range(0, table.RowCount).Select(table.CopyRow).ToList();
        var nextRow = rows.Max(r => int.Parse(r[table.GetColumnIndex("_row")])) + 1;

        var copies = new List<IReadOnlyList<string>>(rows);
        foreach (var row in rows)
        {
            var copy = (string[])row.Clone();
            copy[table.GetColumnIndex("_row")] = nextRow++.ToString();
            copy[table.GetColumnIndex("TeamIndex")] = secondTeamIndex.ToString();
            copies.Add(copy);
        }

        CsvDocument.FromRows(table.Header, copies).Save(players);
        return folder;
    }

    [Fact]
    public void EveryTeamInASeasonFileIsConvertedNotJustTheFirst()
    {
        using var temp = new TempDirectory();

        // Alabama is TeamIndex 2 in the fixture's Team table.
        var donor = TwoTeamDonor(temp, secondTeamIndex: 2);

        var roster = temp.File("season.csv");
        File.WriteAllText(roster, string.Join("\r\n",
            "FirstName,LastName,Position,Team,Season",
            "Jameis,Winston,QB,Florida State,2014",
            "Rashad,Greene,WR,Florida State,2014",
            "Amari,Cooper,WR,Alabama,2014",
            ""));

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = donor,
            RosterPath = roster,
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
            OutputPath = temp.File("out.csv"),
            ReportPath = temp.File("report.txt"),
        });

        Assert.Equal(2, result.Teams.Count);
        Assert.Equal(new[] { "Florida State", "Alabama" }, result.Teams.Select(t => t.Source.School));

        // The tallies are over every team, so a caller that only reports
        // totals — the desktop app does — is right without knowing about this.
        Assert.Equal(3, result.Converted);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(2 * 85 - 3, result.Filled);

        // Both teams land in the one output table: their slots are disjoint,
        // so the user imports one file however many teams they recreated.
        var written = CsvDocument.Load(temp.File("out.csv"));
        Assert.Equal(2 * 85, written.RowCount);
        Assert.Contains("Amari", Enumerable.Range(0, written.RowCount)
            .Select(i => written.GetCell(i, "FirstName")));

        // And the report accounts for every team, not only the first.
        var report = File.ReadAllText(temp.File("report.txt"));
        Assert.Contains("Florida State", report);
        Assert.Contains("Alabama", report);
    }

    [Fact]
    public void ATeamTheDynastyDoesNotCarryCostsTheUserOnlyThatTeam()
    {
        using var temp = new TempDirectory();
        var donor = TwoTeamDonor(temp, secondTeamIndex: 2);

        var roster = temp.File("season.csv");
        File.WriteAllText(roster, string.Join("\r\n",
            "FirstName,LastName,Position,Team,Season",
            "Jameis,Winston,QB,Florida State,2014",
            "A,Player,QB,Not A Real School,2014",
            ""));

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = donor,
            RosterPath = roster,
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
            OutputPath = temp.File("out.csv"),
            ReportPath = temp.File("report.txt"),
        });

        // One unrecognised school out of a season's worth must not cost the
        // user the other 130 teams — but it must be said out loud.
        Assert.Single(result.Teams);
        Assert.Contains(result.Teams[0].GlobalWarnings, w => w.Contains("Not A Real School"));
    }

    // ---- Building the file the tool writes -------------------------------

    [Fact]
    public void ADocumentBuiltFromRowsReadsBackAsItWasWritten()
    {
        using var temp = new TempDirectory();
        var path = temp.File("built.csv");
        var header = new[] { "FirstName", "LastName", "Notes" };
        CsvDocument.FromRows(header, new[]
        {
            new[] { "Jameis", "Winston", "" },
            // A comma in a cell must survive the round trip rather than
            // silently becoming two columns.
            new[] { "Rashad", "Greene", "quick, sure-handed" },
        }).Save(path);

        var read = CsvDocument.Load(path);
        Assert.Equal(header, read.Header);
        Assert.Equal(2, read.RowCount);
        Assert.Equal("quick, sure-handed", read.GetCell(1, "Notes"));
    }

    [Fact]
    public void ARowOfTheWrongWidthIsRefusedRatherThanWritten()
    {
        // Writing it would shift every later column, which is exactly the
        // failure that is hardest to spot in a 10,000-row file.
        var error = Assert.Throws<ArgumentException>(() =>
            CsvDocument.FromRows(new[] { "A", "B" }, new[] { new[] { "only one" } }));
        Assert.Contains("has 1 field", error.Message);
    }
}
