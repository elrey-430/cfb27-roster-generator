using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Which name the announcers say.
///
/// <para><c>PLYR_COMMENT</c> indexes the recorded commentary audio. A
/// recreated player who keeps the value of the slot they took over is called
/// by that person's name for the rest of the dynasty — a mistake nothing in
/// the game surfaces and nobody would think to look for.</para>
/// </summary>
public sealed class CommentaryTests
{
    private static CommentaryIdSet Commentary() =>
        CommentaryIdSet.Load(TestFixtures.DataPath("CommentaryIds.json"));

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private sealed class TempOutput : IDisposable
    {
        public string Csv { get; } = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");

        public string Report { get; } = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");

        public void Dispose()
        {
            File.Delete(Csv);
            File.Delete(Report);
        }
    }

    // ---- The mapping itself ----------------------------------------------

    [Fact]
    public void TheShippedMappingCoversThousandsOfSurnames()
    {
        // Measured from 146,295 player rows across nine game-generated saves.
        // A mapping that had quietly shrunk to a handful would still "work"
        // while silencing most of a roster.
        var commentary = Commentary();
        Assert.True(commentary.Count > 5_000, $"only {commentary.Count} surnames are mapped.");
    }

    [Fact]
    public void AKnownSurnameGetsItsRecordedIndex()
    {
        var commentary = Commentary();
        Assert.NotEqual(CommentaryIdSet.None, commentary.ForLastName("Travis"));
        Assert.NotEqual(CommentaryIdSet.None, commentary.ForLastName("Smith"));
        Assert.True(commentary.CanSay("Johnson"));
    }

    [Fact]
    public void AnUnknownSurnameGetsNoneRatherThanAGuess()
    {
        var commentary = Commentary();

        // The rule the user asked for, and the one that matters: no match
        // means 0, never the nearest thing or the previous occupant's index.
        Assert.Equal(CommentaryIdSet.None, commentary.ForLastName("Zzzyzyx"));
        Assert.Equal(CommentaryIdSet.None, commentary.ForLastName(""));
        Assert.Equal(CommentaryIdSet.None, commentary.ForLastName(null));
        Assert.False(commentary.CanSay("Zzzyzyx"));
    }

    [Fact]
    public void MatchingIgnoresCaseAndSurroundingSpace()
    {
        // A roster somebody typed is not consistent about either.
        var commentary = Commentary();
        var expected = commentary.ForLastName("Travis");

        Assert.Equal(expected, commentary.ForLastName("travis"));
        Assert.Equal(expected, commentary.ForLastName("TRAVIS"));
        Assert.Equal(expected, commentary.ForLastName("  Travis  "));
    }

    [Fact]
    public void AnEmptyMappingSaysNothingRatherThanZeroForEveryone()
    {
        // Count is what the converter checks before writing anything, so the
        // difference between "no file" and "name not recorded" stays visible.
        Assert.Equal(0, CommentaryIdSet.Empty.Count);
        Assert.Equal(CommentaryIdSet.None, CommentaryIdSet.Empty.ForLastName("Travis"));
    }

    // ---- What generation actually writes ----------------------------------

    [Fact]
    public void EveryConvertedPlayerGetsTheIndexForTheirOwnName()
    {
        using var output = new TempOutput();
        var commentary = Commentary();

        new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
            OutputPath = output.Csv,
            ReportPath = output.Report,
        });

        var table = Csv.CsvDocument.Load(output.Csv);
        var checkedRows = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.TeamIndex) != "27")
            {
                continue;
            }

            var surname = table.GetCell(row, PlayerColumns.LastName);
            var written = table.GetCell(row, PlayerColumns.Comment);
            Assert.Equal(commentary.ForLastName(surname).ToString(), written);
            checkedRows++;
        }

        Assert.Equal(85, checkedRows);
    }

    [Fact]
    public void APlayerWhoseNameIsNotRecordedIsSilencedNotMisnamed()
    {
        using var output = new TempOutput();

        new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
            OutputPath = output.Csv,
            ReportPath = output.Report,
        });

        var commentary = Commentary();
        var table = Csv.CsvDocument.Load(output.Csv);
        var silenced = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.TeamIndex) != "27")
            {
                continue;
            }

            if (!commentary.CanSay(table.GetCell(row, PlayerColumns.LastName)))
            {
                // Not the slot's old index, which would be somebody else's name.
                Assert.Equal("0", table.GetCell(row, PlayerColumns.Comment));
                silenced++;
            }
        }

        Assert.True(silenced > 0, "the fixture roster should contain some unrecorded surnames.");
    }

    [Fact]
    public void TheReportSaysHowManyPlayersWillBeNamed()
    {
        using var output = new TempOutput();

        new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
            OutputPath = output.Csv,
            ReportPath = output.Report,
        });

        // Said once for the roster. A per-player note would fire on roughly a
        // third of any historical squad and bury everything else.
        Assert.Contains("named by the announcers", File.ReadAllText(output.Report));
    }
}
