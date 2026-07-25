using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A pre-flight check is only worth having if it agrees with the thing it
/// checks. A validator that misses a problem is worse than none, because the
/// user trusts it; one that invents problems trains them to ignore it.
///
/// These tests hold <c>validate</c> to its two promises against a corpus of
/// real mistakes:
///
/// <list type="number">
/// <item>Its verdict is honest — "ready to generate" means generation
///       succeeds, and a blocking finding means it does not.</item>
/// <item>It misses nothing that costs the user a player or a value.</item>
/// </list>
/// </summary>
public sealed class ValidateIntegrityTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static RatingEngine Engine() =>
        RatingEngine.Load(FixturePath("RatingModels.json"), FixturePath("OverallFormulas.json"));

    private static PositionMappingSet Positions() =>
        PositionMappingSet.Load(FixturePath("PositionMappings.json"));

    /// <summary>
    /// Every mistake covered elsewhere in the suite, in one corpus, so the two
    /// paths are compared over the same ground.
    /// </summary>
    public static TheoryData<string, string> Corpus() => new()
    {
        { "clean", "FirstName,LastName,Position,Number,Class,Role\nJordan,Travis,QB,13,RS Senior,Starter\n" },
        { "bare minimum", "FirstName,LastName,Position\nJordan,Travis,QB\n" },
        { "bad jersey", "FirstName,LastName,Position,Number\nBad,Jersey,WR,199\n" },
        { "bad weight", "FirstName,LastName,Position,Weight\nBad,Weight,WR,900\n" },
        { "bad height", "FirstName,LastName,Position,Height\nBad,Height,WR,7-11\n" },
        { "bad class", "FirstName,LastName,Position,Class\nOdd,Class,LB,Sinior\n" },
        { "bad role", "FirstName,LastName,Position,Role\nOdd,Role,LB,Startr\n" },
        { "unknown position", "FirstName,LastName,Position\nUnknown,Spot,Quarterback Coach\n" },
        { "duplicate player", "FirstName,LastName,Position\nJordan,Travis,QB\nJordan,Travis,QB\n" },
        { "short row", "FirstName,LastName,Position,Number,Class,Role\nJordan,Travis,QB\n" },
        { "long row", "FirstName,LastName,Position\nJordan,Travis,QB,13,RS Senior\n" },
        { "decorated numbers", "FirstName,LastName,Position,Number,Weight\nJordan,Travis,QB,#13,212 lbs\n" },
        { "unreadable number", "FirstName,LastName,Position,Number\nJordan,Travis,QB,twelve\n" },
        { "repeated column", "FirstName,LastName,Position,Position\nJordan,Travis,QB,WR\n" },
        {
            "everything at once",
            "FirstName,LastName,Position,Number,Height,Weight,Class,Role\n" +
            "Jordan,Travis,QB,#13,6-1,212 lbs,RS Senior,Starter\n" +
            "Jordan,Travis,QB,13,6-1,212,RS Senior,Starter\n" +
            "Bad,Jersey,WR,199,6-0,190,Junior,Backup\n" +
            "Odd,Class,LB,44,6-2,230,Sinior,Startr\n" +
            "Unknown,Spot,Quarterback Coach,12,6-0,200,Senior,\n" +
            "Short,Row,CB\n"
        },
    };

    private static string TempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path, content);
        return path;
    }

    private static RosterCsvReport Validate(string path, DynastyExport dynasty) =>
        RosterCsvValidator.Check(path, Positions(), dynasty, school: "Florida State", ratings: Engine());

    private static ConversionReport Generate(string path, DynastyExport dynasty)
    {
        var csv = HistoricalCsv.Read(path, school: "Florida State");
        var donor = dynasty.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var engine = Engine();
        var depth = RosterDepthModel.Load(FixturePath("RosterDepth.json"));
        return new HistoricalTeamConverter(
                dynasty.BuildTeamMappings(),
                Positions(),
                engine,
                ArchetypeSelector.Load(FixturePath("ArchetypeRules.json")),
                new RosterFiller(depth, engine),
                depth,
                dynasty.BuildPreviousSchoolMappings(FixturePath("TeamMappings.json")))
            .Convert(session, csv.Roster);
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void TheVerdictIsHonest(string label, string content)
    {
        var path = TempCsv(content);
        try
        {
            var dynasty = DynastyExport.Open(TestsPath("DonorDynasty"));
            var check = Validate(path, dynasty);

            if (check.CanGenerate)
            {
                // "Ready to generate" has to mean it.
                var report = Generate(path, dynasty);
                Assert.True(
                    check.UsablePlayers == report.Entries.Count,
                    $"[{label}] validate counted {check.UsablePlayers} usable players, generation saw " +
                    $"{report.Entries.Count}");
            }
            else
            {
                // And a blocking verdict has to mean generation really fails.
                Assert.ThrowsAny<Exception>(() => Generate(path, dynasty));
                Assert.NotEmpty(check.OfSeverity(RosterCsvSeverity.Blocking));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void EveryPlayerGenerationSkipsWasFlaggedFirst(string label, string content)
    {
        var path = TempCsv(content);
        try
        {
            var dynasty = DynastyExport.Open(TestsPath("DonorDynasty"));
            var check = Validate(path, dynasty);
            if (!check.CanGenerate)
            {
                return;
            }

            var report = Generate(path, dynasty);
            foreach (var skipped in report.Skipped)
            {
                var name = $"{skipped.Player.FirstName} {skipped.Player.LastName}";
                Assert.True(
                    check.Findings.Any(f => f.Player == name),
                    $"[{label}] generation skipped {name} but validate said nothing about them");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void EveryValueGenerationRejectsWasFlaggedFirst(string label, string content)
    {
        var path = TempCsv(content);
        try
        {
            var dynasty = DynastyExport.Open(TestsPath("DonorDynasty"));
            var check = Validate(path, dynasty);
            if (!check.CanGenerate)
            {
                return;
            }

            var report = Generate(path, dynasty);

            // Generation warns per player when a supplied value could not be
            // used. Each of those is something the user could have fixed
            // beforehand, so each must appear in the check.
            foreach (var entry in report.Converted)
            {
                var name = $"{entry.Player.FirstName} {entry.Player.LastName}";
                var rejected = entry.Warnings.Where(w =>
                    w.Contains("outside the") ||
                    w.Contains("is unrecognized") ||
                    w.Contains("not one the tool recognizes")).ToList();

                foreach (var warning in rejected)
                {
                    Assert.True(
                        check.Findings.Any(f => f.Player == name),
                        $"[{label}] generation rejected a value for {name} ({warning}) but validate " +
                        "said nothing about them");
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ACleanFileProducesNoWarningsAtAll()
    {
        // The other half of integrity: it must not cry wolf. The shipped
        // template is the file users are told to start from.
        var dynasty = DynastyExport.Open(TestsPath("DonorDynasty"));
        var template = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Templates", "HistoricalRosterTemplate_Basics.csv");

        // Without this the missing file arrives as a blocking finding, and the
        // failure below reads as if the template itself were broken.
        Assert.True(File.Exists(template), $"template not copied to the test output: {template}");

        var check = RosterCsvValidator.Check(template, Positions(), dynasty, ratings: Engine());

        Assert.True(check.CanGenerate);
        Assert.Empty(check.OfSeverity(RosterCsvSeverity.Blocking));
        Assert.Empty(check.OfSeverity(RosterCsvSeverity.Warning));
    }

    [Fact]
    public void TheRealFsuRosterIsClean()
    {
        var dynasty = DynastyExport.Open(TestsPath("DonorDynasty"));
        var check = RosterCsvValidator.Check(
            TestsPath("2023_FSU_Input.csv"), Positions(), dynasty, ratings: Engine());

        Assert.True(check.CanGenerate);
        Assert.Empty(check.OfSeverity(RosterCsvSeverity.Blocking));
        Assert.Empty(check.OfSeverity(RosterCsvSeverity.Warning));
        Assert.Equal(75, check.UsablePlayers);

        // It should still say the roster will be topped up to 85.
        Assert.Contains(check.OfSeverity(RosterCsvSeverity.Note), f => f.Message.Contains("filled in"));
    }

    [Fact]
    public void AnUnknownTeamBlocksBeforeAnythingIsWritten()
    {
        var path = TempCsv("FirstName,LastName,Position,Team\nJordan,Travis,QB,Hogwarts\n");
        try
        {
            var dynasty = DynastyExport.Open(TestsPath("DonorDynasty"));
            var check = RosterCsvValidator.Check(path, Positions(), dynasty, ratings: Engine());

            Assert.False(check.CanGenerate);
            Assert.Contains(check.OfSeverity(RosterCsvSeverity.Blocking),
                f => f.Message.Contains("Hogwarts") && f.Message.Contains("list-teams"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AFileThatCannotBeReadIsBlockingRatherThanAnException()
    {
        // The GUI shows findings; it must never have to catch exceptions to
        // learn that a file is unusable.
        var path = TempCsv("Player,Position\nJordan Travis,QB\n");
        try
        {
            var check = RosterCsvValidator.Check(path, Positions(), ratings: Engine());

            Assert.Null(check.Roster);
            Assert.False(check.CanGenerate);
            Assert.Contains(check.OfSeverity(RosterCsvSeverity.Blocking),
                f => f.Message.Contains("firstname"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AMissingFileIsReportedNotThrown()
    {
        var check = RosterCsvValidator.Check(
            Path.Combine(Path.GetTempPath(), "definitely-not-here.csv"), Positions());

        Assert.False(check.CanGenerate);
        Assert.Null(check.Roster);
        Assert.Contains(check.Findings, f => f.Message.Contains("does not exist"));
    }
}
