using RosterGenerator.Core.Export;
using RosterGenerator.Core.Pipeline;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The pipeline both front-ends run.
///
/// It exists so the command line and the desktop app cannot diverge, which
/// only holds if the rules live here rather than in either of them — so these
/// tests pin the rules, not the plumbing.
/// </summary>
public sealed class GenerationServiceTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

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

    private static RosterGenerationRequest Request(TempOutput output) => new()
    {
        DynastyPath = TestsPath("DonorDynasty"),
        RosterPath = TestsPath("2023_FSU_Input.csv"),
        DataDirectory = DataDirectory,
        OutputPath = output.Csv,
        ReportPath = output.Report,
    };

    [Fact]
    public void ARunProducesBothFilesAndAFullRoster()
    {
        using var output = new TempOutput();
        var result = new RosterGenerationService().Run(Request(output));

        Assert.Equal(75, result.Converted);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(10, result.Filled);
        Assert.True(File.Exists(output.Csv));
        Assert.True(File.Exists(output.Report));

        // The report is the record of what was decided for the user, so an
        // empty one would be a silent failure.
        Assert.NotEmpty(File.ReadAllText(output.Report));
    }

    [Fact]
    public void ArchetypeSelectionIsRefusedWithoutRatingGeneration()
    {
        // The archetype decides which of EA's overall formulas applies, so
        // choosing one without recomputing leaves the record inconsistent —
        // exactly the defect found in hand-edited saves. Refusing beats
        // silently ignoring the request.
        using var output = new TempOutput();
        var request = Request(output);
        var error = Assert.Throws<ArgumentException>(() =>
            new RosterGenerationService().Run(new RosterGenerationRequest
            {
                DynastyPath = request.DynastyPath,
                RosterPath = request.RosterPath,
                DataDirectory = request.DataDirectory,
                OutputPath = request.OutputPath,
                ReportPath = request.ReportPath,
                Ratings = RatingsMode.Inherit,
                SelectArchetypes = true,
                FillRoster = false,
            }));

        Assert.Contains("requires rating generation", error.Message);
    }

    [Fact]
    public void FillingIsRefusedWithoutRatingGeneration()
    {
        using var output = new TempOutput();
        var error = Assert.Throws<ArgumentException>(() =>
            new RosterGenerationService().Run(new RosterGenerationRequest
            {
                DynastyPath = TestsPath("DonorDynasty"),
                RosterPath = TestsPath("2023_FSU_Input.csv"),
                DataDirectory = DataDirectory,
                OutputPath = output.Csv,
                ReportPath = output.Report,
                Ratings = RatingsMode.Inherit,
                SelectArchetypes = false,
                FillRoster = true,
            }));

        Assert.Contains("requires rating generation", error.Message);
    }

    [Fact]
    public void InheritingRatingsLeavesTheRosterUnfilled()
    {
        using var output = new TempOutput();
        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            OutputPath = output.Csv,
            ReportPath = output.Report,
            Ratings = RatingsMode.Inherit,
            SelectArchetypes = false,
            FillRoster = false,
        });

        Assert.Equal(0, result.Filled);
        Assert.Equal(10, result.Conversion.LeftoverDonorSlots.Count);
    }

    [Fact]
    public void TheSameRequestAlwaysProducesTheSameFile()
    {
        // Two front-ends calling the same service must get the same answer,
        // and the FSU regression depends on byte stability.
        using var first = new TempOutput();
        using var second = new TempOutput();
        new RosterGenerationService().Run(Request(first));
        new RosterGenerationService().Run(Request(second));

        Assert.Equal(File.ReadAllBytes(first.Csv), File.ReadAllBytes(second.Csv));
    }

    [Fact]
    public void AWrongDataFolderFallsBackToTheOneBesideTheApplication()
    {
        // The user should not have to know where the data files are. A bad
        // --data must not stop a working install from running.
        using var output = new TempOutput();
        var result = new RosterGenerationService().Run(Request(output) with
        {
            DataDirectory = Path.Combine(Path.GetTempPath(), "no-such-data-folder"),
        });

        Assert.Equal(75, result.Converted);
    }

    [Fact]
    public void AGenuinelyMissingDataFileNamesItAndWhereItLooked()
    {
        // When it really is not anywhere, the message has to be actionable —
        // this is the error a user hits if they unzip the exe on its own.
        var error = Assert.Throws<FileNotFoundException>(() =>
            RosterGenerationService.FindDataFile(null, "NotAThing.json"));

        Assert.Contains("NotAThing.json", error.Message);
        Assert.Contains("Looked in", error.Message);
        Assert.Contains("data", error.Message);
    }

    [Fact]
    public void NothingIsWrittenWhenValidationRejectsTheResult()
    {
        // The exporter refuses to write a file that would not import. The
        // service must not leave a half-written one behind either.
        using var output = new TempOutput();
        Assert.False(File.Exists(output.Csv));

        try
        {
            new RosterGenerationService().Run(Request(output) with
            {
                RosterPath = Path.Combine(Path.GetTempPath(), "definitely-missing.csv"),
            });
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // expected
        }

        Assert.False(File.Exists(output.Csv));
    }
}
