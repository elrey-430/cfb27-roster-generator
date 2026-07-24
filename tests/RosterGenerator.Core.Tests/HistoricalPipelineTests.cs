using RosterGenerator.Core.Comparison;
using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;
using RosterGenerator.Core.Validation;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Milestone 2 pipeline tests: historical JSON model, external team/position
/// mappings, class-year parsing, the historical→CFB27 converter (including
/// missing-value defaults and slot assignment), and the comparison utility.
/// The fixture's team 27 has a QB, an RE and an SS slot, which exercises
/// exact, interchangeable-group and overflow assignment.
/// </summary>
public sealed class HistoricalPipelineTests
{
    private static string RepoDataFile(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static TeamMappingSet Teams() => TeamMappingSet.Load(RepoDataFile("TeamMappings.json"));

    private static PositionMappingSet Positions() => PositionMappingSet.Load(RepoDataFile("PositionMappings.json"));

    private static HistoricalRoster SampleRoster(params HistoricalPlayer[] players) => new()
    {
        Season = 2023,
        School = "Florida State",
        Players = players,
    };

    // -- Mappings ------------------------------------------------------------

    [Theory]
    [InlineData("Florida State", 27)]
    [InlineData("FSU", 27)]
    [InlineData("florida state university", 27)]
    [InlineData("Alabama", 2)]
    public void TeamMappingResolvesAliases(string name, int expectedTeamId)
    {
        Assert.Equal(expectedTeamId, Teams().Resolve(name));
    }

    [Fact]
    public void UnknownSchoolGetsDescriptiveError()
    {
        var ex = Assert.Throws<KeyNotFoundException>(() => Teams().Resolve("Hogwarts"));

        Assert.Contains("TeamMappings.json", ex.Message);
    }

    [Theory]
    [InlineData("Tailback", "HB")]
    [InlineData("Halfback", "HB")]
    [InlineData("Cornerback", "CB")]
    [InlineData("Defensive Tackle", "DT")]
    [InlineData("EDGE", "LE")]
    [InlineData("Long Snapper", "TE")]
    [InlineData("safety", "FS")]
    public void PositionMappingResolvesAliases(string alias, string expected)
    {
        Assert.Equal(expected, Positions().Resolve(alias));
    }

    [Fact]
    public void PositionGroupsMakeSidesInterchangeable()
    {
        var positions = Positions();

        Assert.True(positions.AreInterchangeable("LE", "RE"));
        Assert.True(positions.AreInterchangeable("LT", "RG"));
        Assert.True(positions.AreInterchangeable("FS", "SS"));
        Assert.False(positions.AreInterchangeable("QB", "P"));
        Assert.False(positions.AreInterchangeable("CB", "FS"));
    }

    // -- Class year ----------------------------------------------------------

    [Theory]
    [InlineData("Freshman", "Freshman", "Eligible")]
    [InlineData("Redshirt Freshman", "Freshman", "Previous")]
    [InlineData("RS Jr", "Junior", "Previous")]
    [InlineData("senior", "Senior", "Eligible")]
    [InlineData("Graduate", "Senior", "Eligible")]
    public void ClassYearParses(string label, string expectedYear, string expectedRedshirt)
    {
        Assert.True(ClassYear.TryParse(label, out var year, out var redshirt));
        Assert.Equal(expectedYear, year);
        Assert.Equal(expectedRedshirt, redshirt);
    }

    [Fact]
    public void UnknownClassYearFailsParse()
    {
        Assert.False(ClassYear.TryParse("Fifth-year weirdness", out _, out _));
    }

    // -- Historical model ----------------------------------------------------

    [Fact]
    public void HistoricalRosterRoundTripsJsonWithMissingFields()
    {
        var roster = SampleRoster(new HistoricalPlayer
        {
            FirstName = "John",
            LastName = "Smith",
            Position = "Tailback",
            // Jersey, height, weight, class year all deliberately missing.
        });

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            File.WriteAllText(path, roster.ToJson());
            var loaded = HistoricalRoster.Load(path);

            var player = Assert.Single(loaded.Players);
            Assert.Equal("Smith", player.LastName);
            Assert.Null(player.JerseyNumber);
            Assert.Null(player.WeightPounds);
            Assert.Null(player.ClassYear);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -- Simple historical CSV ----------------------------------------------

    [Theory]
    [InlineData("74", 74)]
    [InlineData("6-2", 74)]
    [InlineData("6'2", 74)]
    [InlineData("6'2\"", 74)]
    [InlineData("6 2", 74)]
    [InlineData("5-11", 71)]
    public void HeightParserAcceptsInchesAndFeetInches(string value, int expected)
    {
        var warnings = new List<string>();

        Assert.Equal(expected, HistoricalCsv.ParseHeight(value, "row 2", warnings));
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("tall")]
    [InlineData("12")]
    [InlineData("6-15")]
    public void HeightParserRejectsNonsenseWithWarning(string value)
    {
        var warnings = new List<string>();

        Assert.Null(HistoricalCsv.ParseHeight(value, "row 2", warnings));
        Assert.Single(warnings);
    }

    [Fact]
    public void SimpleCsvMissingRequiredColumnFailsWithGuidance()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path, "FirstName,LastName\r\nJohn,Smith\r\n");
        try
        {
            var ex = Assert.Throws<Csv.CsvSchemaException>(() => HistoricalCsv.Read(path));
            Assert.Contains("position", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SimpleCsvCallerTeamOverridesFileTeamColumn()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path,
            "FirstName,LastName,Position,Team,Season\r\nJohn,Smith,QB,Alabama,2015\r\n");
        try
        {
            var result = HistoricalCsv.Read(path, school: "Florida State", season: 2013);

            Assert.Equal("Florida State", result.Roster.School);
            Assert.Equal(2013, result.Roster.Season);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -- Converter -----------------------------------------------------------

    [Fact]
    public void ConverterReplacesTeamAndExportsValidCsv()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        var historical = SampleRoster(
            new HistoricalPlayer
            {
                FirstName = "Jordan", LastName = "Travis", Position = "QB",
                JerseyNumber = 13, HeightInches = 73, WeightPounds = 212,
                ClassYear = "Redshirt Senior",
            },
            new HistoricalPlayer
            {
                // "Defensive End" maps to LE; the only DE-group slot is RE,
                // which must be kept (interchangeable), not overwritten.
                FirstName = "Jared", LastName = "Verse", Position = "Defensive End",
                JerseyNumber = 5, HeightInches = 76, ClassYear = "RS Sr",
            },
            new HistoricalPlayer
            {
                // Missing jersey/height/class — donor defaults must be
                // inherited and reported.
                FirstName = "Akeem", LastName = "Dent", Position = "Safety",
            });

        var report = new HistoricalTeamConverter(Teams(), Positions()).Convert(session, historical);

        Assert.Equal(27, report.TeamId);
        Assert.Equal(3, report.Converted.Count());
        Assert.Empty(report.Skipped);
        Assert.Empty(report.LeftoverDonorSlots);

        var travis = roster.Players.Single(p => p.LastName == "Travis");
        Assert.Equal(13, travis.JerseyNumber);
        Assert.Equal("Senior", travis.SchoolYear);
        Assert.Equal("Previous", travis.RedshirtStatus);
        Assert.Equal(212, travis.WeightPounds);
        Assert.Equal("52", travis.WeightRaw);

        var verse = roster.Players.Single(p => p.LastName == "Verse");
        Assert.Equal("RE", verse.Position);

        var dent = roster.Players.Single(p => p.LastName == "Dent");
        var dentEntry = report.Entries.Single(e => e.Player.LastName == "Dent");
        Assert.Equal("SS", dent.Position);
        Assert.Contains("Jersey number", dentEntry.MissingFields);
        Assert.Contains(dentEntry.DefaultsUsed, d => d.StartsWith("Jersey number: 7"));
        Assert.Contains("Height", dentEntry.MissingFields);
        Assert.Contains("Class year", dentEntry.MissingFields);

        // The full pipeline must export cleanly (replace-identity edits keep
        // donor assets, which is a warning, not an error).
        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            var result = new RosterExporter().Export(new RosterValidationContext(roster, session), outputPath);
            Assert.Equal(3, result.ChangedColumnsByRowKey.Count);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void OverflowPlayersAreSkippedAndReported()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        var players = Enumerable.Range(1, 5)
            .Select(i => new HistoricalPlayer { FirstName = $"P{i}", LastName = $"Player{i}", Position = "QB" })
            .ToArray();

        var report = new HistoricalTeamConverter(Teams(), Positions()).Convert(session, SampleRoster(players));

        // Only three donor slots exist on team 27 in the fixture.
        Assert.Equal(3, report.Converted.Count());
        Assert.Equal(2, report.Skipped.Count());
        Assert.All(report.Skipped, e => Assert.Contains(e.Warnings, w => w.Contains("No donor roster slot")));
    }

    [Fact]
    public void UnmappedPositionSkipsPlayerWithWarning()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        var historical = SampleRoster(new HistoricalPlayer
        {
            FirstName = "Weird", LastName = "Position", Position = "Wingback",
        });

        var report = new HistoricalTeamConverter(Teams(), Positions()).Convert(session, historical);

        var entry = Assert.Single(report.Skipped);
        Assert.Contains(entry.Warnings, w => w.Contains("PositionMappings.json"));
    }

    [Fact]
    public void ReportMarkdownContainsRequiredSections()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        var historical = SampleRoster(new HistoricalPlayer
        {
            FirstName = "John", LastName = "Smith", Position = "QB",
        });

        var report = new HistoricalTeamConverter(Teams(), Positions()).Convert(session, historical);
        var markdown = report.ToMarkdown();

        Assert.Contains("Players generated", markdown);
        Assert.Contains("Global assumptions", markdown);
        Assert.Contains("John Smith", markdown);
        Assert.Contains("Missing:", markdown);
        Assert.Contains("Default used:", markdown);
    }

    // -- Comparison ----------------------------------------------------------

    [Fact]
    public void ComparerFindsFieldDifferencesAndUnmatchedPlayers()
    {
        var left = TestFixtures.LoadSampleRoster();
        var right = TestFixtures.LoadSampleRoster();

        // Change one field on a shared player and rename another so it only
        // matches on one side.
        var session = new RosterEditSession(right);
        session.SetJerseyNumber(right.FindByRowKey(330)!, 44);
        session.RenamePlayer(right.FindByRowKey(591)!, "Different", "Person");

        var report = new RosterComparer().Compare(left, right, teamId: 27, "generated", "manual");

        var applewhite = report.Matched.Single(m => m.Left.LastName == "Applewhite");
        var diff = Assert.Single(applewhite.Differences);
        Assert.Equal(PlayerColumns.JerseyNum, diff.Column);
        Assert.Equal("2", diff.LeftValue);
        Assert.Equal("44", diff.RightValue);

        Assert.Single(report.OnlyInLeft);   // Ashlynd Barker (renamed on right)
        Assert.Single(report.OnlyInRight);  // Different Person
        Assert.Contains("Field differences", report.ToMarkdown());
    }
}
