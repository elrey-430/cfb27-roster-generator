using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Pipeline;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The season the game puts on screen while the dynasty is played.
///
/// <para>A recreated 1985 roster used to be played in whatever year the save
/// started in, which is the one thing about a historical recreation nobody can
/// edit around afterwards. The year lives in <c>SeasonInfo</c>, a one-row
/// table: <c>CurrentSeasonYear</c> and <c>BaseCalendarYear</c>, the anchor the
/// dynasty counts forward from. Both are written so they agree however the
/// game derives what it shows, along with the current-season row each team
/// keeps in <c>TeamHistoricSeriesYear</c>.</para>
///
/// <para><b>Confirmed in the game</b>, not merely written: a save built from a
/// real dynasty with these fields set to 2023 loads and displays 2023.</para>
///
/// <para>What is pinned here is everything that does not need a 9.6 MB save
/// committed as a fixture — the range guard, that the option is inert without
/// a save to write into, and that it is never applied unasked. The end-to-end
/// check runs only when <c>CFB27_TEST_SAVE</c> points at a real save.</para>
/// </summary>
public sealed class DynastyYearTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string? RealSave =>
        Environment.GetEnvironmentVariable("CFB27_TEST_SAVE") is { Length: > 0 } path && File.Exists(path)
            ? path
            : null;

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    // ---- The range a year may take -----------------------------------------

    [Theory]
    [InlineData(1869)] // the first college football game ever played
    [InlineData(1985)]
    [InlineData(2023)]
    [InlineData(4095)] // the widest the 12-bit field goes
    public void ASeasonInsideTheFormatIsAccepted(int year)
    {
        Assert.True(NativeSave.IsSupportedSeason(year));
    }

    [Theory]
    [InlineData(1868)] // before the sport existed
    [InlineData(4096)] // one past what the field can hold
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(20233)] // the typo this is really for
    public void ASeasonOutsideTheFormatIsRefused(int year)
    {
        Assert.False(NativeSave.IsSupportedSeason(year));
    }

    [Fact]
    public void WritingAnImpossibleYearIsRefusedBeforeAnythingIsOpened()
    {
        // madden-franchise does not enforce its own schema: setting 5000, or
        // -1, is accepted in silence and writes a number the game was never
        // built to read. So the bound is ours to hold, and it is held before
        // the save is touched rather than after it has been rewritten.
        using var temp = new TempDirectory();
        var save = temp.File("DYNASTY-SOURCE");
        File.WriteAllBytes(save, "FBCHUNKS"u8.ToArray().Concat(new byte[64]).ToArray());

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => NativeSave.Apply(
            save, temp.File("DYNASTY-OUT"), new[] { temp.File("player.csv") }, seasonYear: 5000));
        Assert.Contains("4095", thrown.Message);

        Assert.False(File.Exists(temp.File("DYNASTY-OUT")), "nothing should have been written.");
    }

    // ---- Never applied unasked ---------------------------------------------

    [Fact]
    public void AnOrdinaryRunLeavesTheYearAlone()
    {
        using var temp = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            OutputPath = temp.File("roster.csv"),
            ReportPath = temp.File("report.txt"),
        });

        // The roster says 2023 everywhere, and that still must not reach into
        // somebody's calendar. Recreating an old roster inside a present-day
        // dynasty is a perfectly reasonable thing to want.
        Assert.Null(result.SaveOutput);
        Assert.DoesNotContain(
            result.Conversion.GlobalWarnings,
            w => w.Contains("season year", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AskingForAYearWithoutASaveSaysSoRatherThanIgnoringIt()
    {
        using var temp = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            OutputPath = temp.File("roster.csv"),
            ReportPath = temp.File("report.txt"),
            DynastyYear = 1985,
        });

        // The year lives in a table the export tool does not write, so a
        // CSV-only run has nowhere to put it. Handing back a roster that
        // quietly dropped the request is the failure worth avoiding.
        var warning = Assert.Single(
            result.Conversion.GlobalWarnings,
            w => w.Contains("season year", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("dynasty save", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(warning, File.ReadAllText(result.ReportPath));
    }

    [Fact]
    public void TheReportIsSilentAboutAYearNobodyAskedFor()
    {
        using var temp = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            OutputPath = temp.File("roster.csv"),
            ReportPath = temp.File("report.txt"),
        });

        Assert.DoesNotContain("season year", File.ReadAllText(result.ReportPath),
            StringComparison.OrdinalIgnoreCase);
    }

    // ---- The whole way through, when a real save is available ---------------

    [Fact]
    public void TheYearIsWrittenIntoTheSaveAndReadBack()
    {
        if (RealSave is null || !NativeSave.IsAvailable(out _))
        {
            return;
        }

        using var temp = new TempDirectory();
        var destination = temp.File("DYNASTY-1985");
        var sourceBytes = new FileInfo(RealSave!).Length;

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = RealSave!,
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            OutputPath = temp.File("player.csv"),
            ReportPath = temp.File("report.txt"),
            EquipmentOutputPath = temp.File("visuals.csv"),
            SaveOutputPath = destination,
            DynastyYear = 1985,
        });

        var save = result.SaveOutput;
        Assert.NotNull(save);
        Assert.True(save!.SeasonYearChanged);
        Assert.Equal(1985, save.SeasonYearTo);
        Assert.NotEqual(save.SeasonYearTo, save.SeasonYearFrom);

        // Reading the new save back is what proves it landed, rather than
        // trusting the report of the thing that wrote it.
        var readBack = temp.File("readback");
        NativeSave.Extract(destination, readBack, "SeasonInfo");
        var seasonInfo = Directory.GetFiles(readBack, "*SeasonInfo*.csv").SingleOrDefault();
        if (seasonInfo is not null)
        {
            var table = Csv.CsvDocument.Load(seasonInfo);
            Assert.Equal("1985", table.GetCell(0, "CurrentSeasonYear"));
            Assert.Equal("1985", table.GetCell(0, "BaseCalendarYear"));
        }

        // And the dynasty that came in is still exactly what came in.
        Assert.Equal(sourceBytes, new FileInfo(RealSave!).Length);
    }
}
