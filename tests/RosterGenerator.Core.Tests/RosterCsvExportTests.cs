using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A dynasty can be written out <em>as a roster file</em> — the format the
/// generator reads.
///
/// <para><b>Why this is the other half of the tool.</b> It could read a roster
/// file and not write one, and that asymmetry cost users twice: correcting one
/// player in ten thousand meant retyping the roster or editing the result in a
/// third-party editor where the correction was invisible here and lost on the
/// next run, and every new project started from a blank template rather than
/// from what the dynasty already had.</para>
///
/// <para>The property that matters is the round trip: <b>export a team, feed
/// the file straight back in, and every identity field comes out the same.</b>
/// Ratings are deliberately not part of that — see below.</para>
/// </summary>
public sealed class RosterCsvExportTests
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

    private static string ExportFsu(TempDirectory folder, int? season = null)
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var path = folder.File("exported.csv");
        RosterCsvExporter.Write(
            export, export.LoadPlayerRoster(), path,
            new[] { export.BuildTeamMappings().Resolve("Florida State") },
            season, export.LoadCharacterVisuals());
        return path;
    }

    // ---- The round trip -----------------------------------------------------

    [Fact]
    public void EveryIdentityFieldSurvivesAnExportAndReImport()
    {
        using var folder = new TempDirectory();
        var exported = ExportFsu(folder);

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = exported,
            DataDirectory = DataDirectory,
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
        });

        var columns = new[]
        {
            PlayerColumns.Position, PlayerColumns.JerseyNum, PlayerColumns.Height,
            PlayerColumns.Weight, PlayerColumns.SchoolYear, PlayerColumns.RedshirtStatus,
            PlayerColumns.HomeTown, PlayerColumns.HomeState, PlayerColumns.PrevTeamId,
        };

        // Compared by player, not by row. A recreated player takes whichever
        // donor slot fits his position, so he need not come back to the row he
        // left — what has to survive is the man, not his seat.
        var before = ByPlayer(TestsPath("DonorDynasty", "0152_Player.csv"), columns);
        var after = ByPlayer(result.OutputPath, columns);

        Assert.True(before.Count >= 80, $"the fixture only has {before.Count} players.");
        foreach (var (name, fields) in before)
        {
            Assert.True(after.ContainsKey(name), $"{name} did not come back.");
            Assert.Equal(fields, after[name]);
        }
    }

    [Fact]
    public void ATransferFromOutsideTheDynastyStaysATransfer()
    {
        // Left blank, these players come back as having never transferred at
        // all — a different and untrue thing. They are the reason the exporter
        // has a word for "a school your dynasty does not carry".
        using var folder = new TempDirectory();
        var table = CsvDocument.Load(ExportFsu(folder));

        var unlisted = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, "PreviousSchool") == PlayerSchema.PreviousSchoolNotInDynasty)
            {
                unlisted++;
            }
        }

        Assert.True(unlisted > 0, "the fixture no longer carries a transfer from outside the dynasty.");
    }

    [Fact]
    public void TheFileReadsAsAnOrdinaryRosterFile()
    {
        using var folder = new TempDirectory();

        // No warnings, no corrections — an exported file is not a rough draft.
        var read = HistoricalCsv.Read(ExportFsu(folder, season: 2014));

        Assert.Single(read.Rosters);
        Assert.Equal("Florida State", read.Roster.School);
        Assert.Equal(2014, read.Roster.Season);
        Assert.Empty(read.Warnings);
    }

    /// <summary>Every Florida State player's identity fields, keyed by name.</summary>
    private static Dictionary<string, string> ByPlayer(string path, IReadOnlyList<string> columns)
    {
        var table = CsvDocument.Load(path);
        var byPlayer = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.IsEmpty) == "true" ||
                table.GetCell(row, PlayerColumns.TeamIndex) != "27")
            {
                continue;
            }

            var name = table.GetCell(row, PlayerColumns.FirstName) + " " +
                       table.GetCell(row, PlayerColumns.LastName);
            byPlayer[name] = string.Join("|", columns.Select(c => table.GetCell(row, c)));
        }

        return byPlayer;
    }

    // ---- What it fills in ---------------------------------------------------

    [Fact]
    public void ItIsTheTemplateAUserAlreadyKnows()
    {
        using var folder = new TempDirectory();
        var header = CsvDocument.Load(ExportFsu(folder)).Header;
        var template = CsvDocument.Load(
            Path.Combine(DataDirectory, "Templates", "HistoricalRosterTemplate.csv")).Header;

        Assert.Equal(template, header);
    }

    [Fact]
    public void TheThingsTheSaveKnowsAreFilledIn()
    {
        using var folder = new TempDirectory();
        var table = CsvDocument.Load(ExportFsu(folder));

        var filled = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var column in new[] { "FirstName", "Position", "Number", "HeightInches", "Weight", "Class" })
        {
            for (var row = 0; row < table.RowCount; row++)
            {
                if (table.GetCell(row, column).Length > 0)
                {
                    filled[column] = filled.GetValueOrDefault(column) + 1;
                }
            }

            Assert.True(filled.GetValueOrDefault(column) == table.RowCount,
                $"{column} was filled for {filled.GetValueOrDefault(column)} of {table.RowCount} players.");
        }
    }

    [Fact]
    public void TheEvidenceColumnsAreEmptyBecauseASaveHasNeverHeldThem()
    {
        // Being honest about this is the point. A save records what a player
        // IS, never what he DID, so exporting cannot invent a stat line — and
        // pretending otherwise would put made-up numbers in a user's file.
        using var folder = new TempDirectory();
        var table = CsvDocument.Load(ExportFsu(folder));

        foreach (var column in new[] { "RushYards", "Awards", "DraftPick", "Forty", "GamesStarted" })
        {
            for (var row = 0; row < table.RowCount; row++)
            {
                Assert.Equal("", table.GetCell(row, column));
            }
        }
    }

    [Fact]
    public void AClassLabelComesBackTheWayAUserWouldWriteIt()
    {
        using var folder = new TempDirectory();
        var table = CsvDocument.Load(ExportFsu(folder));

        var labels = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 0; row < table.RowCount; row++)
        {
            labels.Add(table.GetCell(row, "Class"));
        }

        Assert.Contains("Redshirt Senior", labels);
        Assert.All(labels, l => Assert.True(
            Conversion.ClassYear.TryParse(l, out _, out _), $"'{l}' does not read back."));
    }

    [Fact]
    public void AHometownIsWrittenTheWayTheReaderParsesIt()
    {
        using var folder = new TempDirectory();
        var table = CsvDocument.Load(ExportFsu(folder));

        var spaced = false;
        for (var row = 0; row < table.RowCount; row++)
        {
            var hometown = table.GetCell(row, "Hometown");
            if (hometown.Length == 0)
            {
                continue;
            }

            // The save spells states in PascalCase; a user writes them spaced.
            spaced |= hometown.Contains("West Virginia", StringComparison.Ordinal) ||
                      hometown.Contains("South Carolina", StringComparison.Ordinal) ||
                      hometown.Contains("North Carolina", StringComparison.Ordinal) ||
                      hometown.Contains("New York", StringComparison.Ordinal);
            Assert.NotNull(Conversion.Hometown.Parse(hometown));
        }

        Assert.True(spaced, "no two-word state appeared, so the spelling was never exercised.");
    }

    // ---- Whole-season export ------------------------------------------------

    [Fact]
    public void OmittingTheTeamWritesEveryPlayerTheDynastyCarries()
    {
        using var folder = new TempDirectory();
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var roster = export.LoadPlayerRoster();
        var path = folder.File("everyone.csv");

        var report = RosterCsvExporter.Write(export, roster, path);

        // One file, a whole dynasty. The generator reads it straight back,
        // since a roster file's Team column decides where each player goes —
        // which is how a whole season comes out in one pass. (This fixture is
        // a single trimmed team, so the count is what proves it, not the team
        // total.)
        var live = roster.Players.Count(p => !p.IsEmpty);
        Assert.Equal(live, report.Players);
        Assert.Equal(report.Players, CsvDocument.Load(path).RowCount);
        Assert.NotEmpty(report.Teams);
    }
}
