using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;
using RosterGenerator.Core.Validation;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Nobody researching an old roster finds a complete record for every player.
/// A user must be able to type in the names, positions, numbers and classes
/// they could find and get a working file back — and a mistake in one cell
/// must never cost them the whole export.
///
/// These tests pin that contract end to end, through generation, validation
/// and export.
/// </summary>
public sealed class SparseInputTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    /// <summary>Writes a roster CSV to a temp file and returns its path.</summary>
    private static string TempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Runs the whole pipeline the way the CLI does — full rating generation,
    /// archetype selection and roster fill — and exports, so a failure
    /// anywhere in the chain surfaces here.
    /// </summary>
    private static (ConversionReport Report, ExportResult Export, Core.Model.PlayerRoster Roster)
        GenerateAndExport(string csvContent)
    {
        var path = TempCsv(csvContent);
        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            var export = DynastyExport.Open(TestsPath("DonorDynasty"));
            var csv = HistoricalCsv.Read(path, school: "Florida State");
            var donor = export.LoadPlayerRoster();
            var session = new RosterEditSession(donor);
            var engine = RatingEngine.Load(
                FixturePath("RatingModels.json"), FixturePath("OverallFormulas.json"));
            var depth = RosterDepthModel.Load(FixturePath("RosterDepth.json"));
            var formulas = OverallFormulaSet.Load(FixturePath("OverallFormulas.json"));

            var report = new HistoricalTeamConverter(
                    export.BuildTeamMappings(),
                    PositionMappingSet.Load(FixturePath("PositionMappings.json")),
                    engine,
                    ArchetypeSelector.Load(FixturePath("ArchetypeRules.json")),
                    new RosterFiller(depth, engine),
                    depth,
                    export.BuildPreviousSchoolMappings(FixturePath("TeamMappings.json")))
                .Convert(session, csv.Roster);

            var result = new RosterExporter().Export(
                new RosterValidationContext(donor, session, overallFormulas: formulas), outputPath);
            return (report, result, donor);
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void NameAndPositionAloneProduceACompletePlayer()
    {
        var (report, _, roster) = GenerateAndExport(
            "FirstName,LastName,Position\n" +
            "Jordan,Travis,QB\n" +
            "Trey,Benson,Tailback\n" +
            "Jared,Verse,Defensive End\n");

        Assert.Equal(3, report.Converted.Count());
        Assert.Empty(report.Skipped);

        foreach (var entry in report.Converted)
        {
            var player = roster.Players.Single(p => p.RowKey == entry.AssignedRowKey!.Value);

            // Everything the user did not supply comes from the donor slot, so
            // the result is still a real player rather than a half-filled row.
            Assert.InRange(player.HeightInches, PlayerSchema.HeightInchesMin, PlayerSchema.HeightInchesMax);
            Assert.InRange(player.WeightPounds, PlayerSchema.WeightPoundsMin, PlayerSchema.WeightPoundsMax);
            Assert.InRange(player.JerseyNumber, PlayerSchema.JerseyNumMin, PlayerSchema.JerseyNumMax);
            Assert.NotEmpty(player.GetRaw(PlayerColumns.PlayerType));
            Assert.NotEmpty(player.GetRaw(PlayerColumns.HomeTown));
            Assert.True(player.OverallRating > 0);

            // And the user is told which fields were filled in for them.
            Assert.NotEmpty(entry.DefaultsUsed);
        }
    }

    [Fact]
    public void NamePositionNumberAndClassAreEnoughForAWholeRoster()
    {
        var (report, export, roster) = GenerateAndExport(
            "FirstName,LastName,Position,Number,Class\n" +
            "Jordan,Travis,QB,13,RS Senior\n" +
            "Trey,Benson,RB,3,RS Junior\n" +
            "Keon,Coleman,WR,4,Junior\n" +
            "Jared,Verse,DE,5,RS Senior\n" +
            "Robert,Scott,OT,74,RS Junior\n" +
            "Ryan,Fitzgerald,K,88,RS Junior\n");

        Assert.Equal(6, report.Converted.Count());
        Assert.NotEmpty(export.ChangedColumnsByRowKey);

        // The rest of the 85-man roster is filled in, so the team is complete
        // without the user researching walk-ons.
        Assert.Equal(79, report.FilledSlots.Count);
        Assert.Equal(85, roster.Players.Count(p => p.TeamIndex == report.TeamId));

        // What the user DID supply is honoured exactly.
        var travis = roster.Players.Single(p => p.FirstName == "Jordan" && p.LastName == "Travis");
        Assert.Equal(13, travis.JerseyNumber);
        Assert.Equal("Senior", travis.SchoolYear);
        Assert.Equal("QB", travis.Position);
    }

    [Theory]
    [InlineData("Jersey number", "FirstName,LastName,Position,Number\nBad,Jersey,WR,199\n")]
    [InlineData("Height", "FirstName,LastName,Position,Height\nBad,Height,WR,7-11\n")]
    [InlineData("Weight", "FirstName,LastName,Position,Weight\nBad,Weight,WR,900\n")]
    public void AnOutOfRangeValueIsReportedAndInheritedRatherThanBlockingTheExport(
        string label, string csv)
    {
        // The failure this prevents: one mistyped cell used to write a value
        // the validator rejected, which failed the export and produced no file
        // at all. A single typo must not cost an 85-player roster.
        var (report, export, _) = GenerateAndExport(csv);

        var entry = Assert.Single(report.Converted);
        Assert.Contains(entry.Warnings, w => w.Contains(label) && w.Contains("outside"));
        Assert.Contains(entry.DefaultsUsed, d => d.Contains(label) && d.Contains("inherited"));
        Assert.NotEmpty(export.ChangedColumnsByRowKey);
    }

    [Fact]
    public void ExcelQuirksAreToleratedBomBlankRowsAndSpacedHeaders()
    {
        // Saving from Excel adds a UTF-8 BOM and usually leaves trailing empty
        // rows behind; people also type "First Name" and lower-case positions.
        var (report, _, _) = GenerateAndExport(
            "﻿first name,Last Name,POSITION,number,class\r\n" +
            " Jordan , Travis ,qb, 13 ,rs senior\r\n" +
            "Trey,Benson,Running Back,3,So.\r\n" +
            ",,,,\r\n" +
            ",,,,\r\n");

        Assert.Equal(2, report.Converted.Count());
        Assert.Equal("Jordan", report.Converted.First().Player.FirstName);
    }

    [Fact]
    public void AnUnmappablePositionSkipsOnlyThatPlayer()
    {
        var (report, export, _) = GenerateAndExport(
            "FirstName,LastName,Position,Number\n" +
            "Real,Player,QB,7\n" +
            "Unknown,Spot,Quarterback Coach,12\n");

        Assert.Single(report.Converted);
        var skipped = Assert.Single(report.Skipped);
        Assert.Contains(skipped.Warnings, w => w.Contains("no mapping"));
        Assert.NotEmpty(export.ChangedColumnsByRowKey);
    }

    [Fact]
    public void MorePlayersThanSlotsFillsTheTeamAndReportsTheRest()
    {
        var rows = string.Concat(Enumerable.Range(0, 95)
            .Select(i => $"Player,Num{i},{new[] { "QB", "RB", "WR", "OT", "DE", "LB", "CB" }[i % 7]},{i % 100}\n"));
        var (report, export, _) = GenerateAndExport("FirstName,LastName,Position,Number\n" + rows);

        Assert.Equal(85, report.Converted.Count());
        Assert.Equal(10, report.Skipped.Count());
        Assert.All(report.Skipped, e => Assert.Contains(e.Warnings, w => w.Contains("No donor roster slot")));
        Assert.NotEmpty(export.ChangedColumnsByRowKey);
    }

    [Fact]
    public void ARosterOfOnePositionStillWorks()
    {
        var rows = string.Concat(Enumerable.Range(1, 12).Select(i => $"Quarter,Back{i},QB,{i}\n"));
        var (report, export, _) = GenerateAndExport("FirstName,LastName,Position,Number\n" + rows);

        Assert.Equal(12, report.Converted.Count());
        Assert.NotEmpty(export.ChangedColumnsByRowKey);
    }

    [Fact]
    public void TheBasicsTemplateGeneratesWithoutWarningsAboutItself()
    {
        // The template a user is pointed at first must itself be sufficient.
        var basics = Path.Combine(
            AppContext.BaseDirectory, "Templates", "HistoricalRosterTemplate_Basics.csv");
        var csv = HistoricalCsv.Read(basics);

        Assert.Empty(csv.Warnings);
        Assert.Equal("Florida State", csv.Roster.School);
        Assert.Equal(2023, csv.Roster.Season);
        Assert.Equal(12, csv.Roster.Players.Count);
        Assert.All(csv.Roster.Players, p =>
        {
            Assert.NotEmpty(p.FirstName);
            Assert.NotEmpty(p.LastName);
            Assert.NotEmpty(p.Position);
        });
    }
}
