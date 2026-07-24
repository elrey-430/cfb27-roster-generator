using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Validation;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The Milestone 2 success case as a permanent regression test: generating
/// the 2023 Florida State roster from the simple-format input CSV against
/// the trimmed donor dynasty must keep producing byte-identical output.
/// The fixtures live in the repo's top-level <c>Tests/</c> folder.
/// </summary>
public sealed class FsuRegressionTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    [Fact]
    public void Fsu2023GenerationIsByteStable()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        Assert.Equal(138, export.Teams.Count);

        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        Assert.Empty(csv.Warnings);
        Assert.Equal("Florida State", csv.Roster.School);
        Assert.Equal(2023, csv.Roster.Season);
        Assert.Equal(75, csv.Roster.Players.Count);

        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var converter = new HistoricalTeamConverter(
            export.BuildTeamMappings(),
            PositionMappingSet.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PositionMappings.json")));
        var report = converter.Convert(session, csv.Roster);

        Assert.Equal(27, report.TeamId);
        Assert.Equal(75, report.Converted.Count());
        Assert.Empty(report.Skipped);
        Assert.Equal(10, report.LeftoverDonorSlots.Count);

        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            new RosterExporter().Export(new RosterValidationContext(donor, session), outputPath);

            Assert.Equal(
                File.ReadAllBytes(TestsPath("2023_FSU_Expected_Output.csv")),
                File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }
}
