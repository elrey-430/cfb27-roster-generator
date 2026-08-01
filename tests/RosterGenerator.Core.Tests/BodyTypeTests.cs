using RosterGenerator.Core.Appearance;
using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A recreated player gets their own body, built from position, height and
/// weight — nothing is asked of the user.
///
/// <para>The build lives in the Player table's <c>CharacterBodyType</c> column.
/// <b><c>Freshman</c> is the stored name for the build the game's own editor
/// calls Lean</b>, confirmed by a save in which five named Florida State
/// players were each given a different build in-game and read back out:
/// Lean wrote <c>Freshman</c>, and Thin, Standard, Muscular and Heavy wrote
/// themselves.</para>
///
/// <para>Two sources decide it. EA's player builder says which builds a given
/// height and weight can carry, which is what stops a 175 lb receiver being
/// written as Muscular. The base save's own census says what the game puts on
/// each position, and where a position is not in question — a guard is Heavy,
/// an end is Muscular — the position decides outright.</para>
/// </summary>
public sealed class BodyTypeTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static BodyTypeModel Model =>
        BodyTypeModel.Load(Path.Combine(DataDirectory, "BodyTypeRules.json"));

    private const string Lean = "Freshman";

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    // ---- The five builds, as the save spells them ---------------------------

    [Fact]
    public void TheBuildTheGameCallsLeanIsStoredAsFreshman()
    {
        // Read off a save where the option was set in-game and named. Nothing
        // else in the schema hints at it, and a tool that wrote "Lean" would be
        // writing a value the game has never held.
        Assert.Contains(Lean, Model.Values);
        Assert.DoesNotContain("Lean", Model.Values);
    }

    [Fact]
    public void OnlyTheFiveBuildsTheGameUsesExist()
    {
        Assert.Equal(
            new[] { "Freshman", "Heavy", "Muscular", "Standard", "Thin" },
            Model.Values.OrderBy(v => v, StringComparer.Ordinal));
    }

    // ---- The positions whose build is not in question -----------------------

    [Theory]
    [InlineData("LE")]
    [InlineData("RE")]
    [InlineData("LT")]
    [InlineData("RT")]
    public void EndsAndTacklesAreMuscular(string position)
    {
        // Asked for directly, and the census agrees: 95.0%, 97.3%, 81.2%, 81.5%.
        Assert.Equal("Muscular", Model.For(position, 77, 290));
        Assert.Equal("Muscular", Model.For(position, 75, 250));
    }

    [Theory]
    [InlineData("DT")]
    [InlineData("LG")]
    [InlineData("RG")]
    [InlineData("C")]
    public void TheInteriorLineIsHeavy(string position)
    {
        // C was not named in the request; the census put it here — 90.2% Heavy,
        // the highest of the three interior spots.
        Assert.Equal("Heavy", Model.For(position, 75, 305));
        Assert.Equal("Heavy", Model.For(position, 73, 280));
    }

    [Fact]
    public void ThosePositionsAreNotTalkedOutOfItByALightWeight()
    {
        // The builder's envelope describes the light builds. Applying it here
        // would make a 215 lb linebacker "Standard", which the game itself
        // does not do — 94.5% to 97.4% of linebackers are Muscular at every
        // weight. Measured: gating these positions costs six points.
        Assert.Equal("Muscular", Model.For("MLB", 73, 215));
        Assert.Equal("Muscular", Model.For("TE", 74, 210));
    }

    // ---- The builder's envelope, for everyone else --------------------------

    [Fact]
    public void ALightReceiverIsNeverMuscular()
    {
        // The whole point of reading height and weight: a 6'0" 175 lb receiver
        // cannot carry a Muscular build, whatever else is true of them.
        var build = Model.For("WR", 72, 175);
        Assert.NotEqual("Muscular", build);
        Assert.NotEqual("Heavy", build);
    }

    [Theory]
    [InlineData(69, 170, Lean)]        // 5'9"  under 175
    [InlineData(69, 190, "Standard")]  // 5'9"  over 175
    [InlineData(70, 175, Lean)]        // 5'10" under 180
    [InlineData(70, 200, "Standard")]  // 5'10" 180-219
    [InlineData(72, 180, Lean)]        // 6'0"  under 185
    [InlineData(72, 200, "Standard")]  // 6'0"  185-219
    [InlineData(74, 190, Lean)]        // 6'2"  under 195
    [InlineData(76, 200, Lean)]        // 6'4"  under 205
    [InlineData(76, 210, "Standard")]  // 6'4"  205-219
    public void TheBuilderTableDecidesForASkillPlayer(int height, int weight, string expected)
    {
        Assert.Equal(expected, Model.For("WR", height, weight));
    }

    [Theory]
    [InlineData(70, 225)]   // 5'10" 220+   -> Muscular only
    [InlineData(72, 225)]   // 6'0"  220-230
    [InlineData(74, 243)]   // 6'2"  240-245
    [InlineData(76, 250)]   // 6'4"  241-255
    public void ASkillPlayerHeavyForTheirHeightGetsAMuscularBuild(int height, int weight)
    {
        Assert.Equal("Muscular", Model.For("WR", height, weight));
    }

    [Fact]
    public void OffTheTopOfTheTableTheLightBuildsAreOutOfTheQuestion()
    {
        // The table stops at 255 lb. A 300 lb player at any listed position is
        // not Thin, whatever the position's own preference says.
        var build = Model.For("QB", 75, 300);
        Assert.Equal("Muscular", build);
    }

    [Fact]
    public void KickersAndPuntersPreferThin()
    {
        // 68.6% and 72.5% in the census, the only positions where Thin leads.
        Assert.Equal("Thin", Model.For("K", 73, 200));
        Assert.Equal("Thin", Model.For("P", 74, 210));
    }

    [Fact]
    public void EveryBuildTheModelCanProduceIsOneTheGameUses()
    {
        // A build the game has never held would be this tool authoring a value
        // rather than reassigning one, which is the line the whole project
        // holds. Swept across every position and the full physical range.
        foreach (var position in PlayerSchema.Positions)
        {
            for (var height = 60; height <= 84; height++)
            {
                for (var weight = 160; weight <= 400; weight += 5)
                {
                    var build = Model.For(position, height, weight);
                    Assert.NotNull(build);
                    Assert.True(Model.IsKnownBuild(build!),
                        $"{position} {height}\" {weight}lb produced '{build}', which no player carries.");
                }
            }
        }
    }

    [Fact]
    public void WithoutAHeightOrAWeightTheSlotKeepsWhatItHad()
    {
        // Guessing from position alone would be worse than the slot's own
        // value, which at least came from a player of some real size.
        Assert.Null(Model.For("WR", 0, 190));
        Assert.Null(Model.For("WR", 72, 0));
    }

    // ---- Through the pipeline -----------------------------------------------

    private static RosterGenerationResult Generate(TempDirectory folder, bool fill = true) =>
        new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            Team = "Florida State",
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            Ratings = RatingsMode.Generate,
            FillRoster = fill,
        });

    [Fact]
    public void EveryGeneratedPlayerEndsUpWithABuildThatFitsTheirBody()
    {
        using var folder = new TempDirectory();
        var result = Generate(folder);

        var table = CsvDocument.Load(result.OutputPath);
        var model = Model;
        var checkedRows = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.IsEmpty) == "true" ||
                table.GetCell(row, PlayerColumns.TeamIndex) != "27")
            {
                continue;
            }

            var position = table.GetCell(row, PlayerColumns.Position);
            var height = int.Parse(table.GetCell(row, PlayerColumns.Height));
            var weight = int.Parse(table.GetCell(row, PlayerColumns.Weight)) + 160;
            var expected = model.For(position, height, weight);
            if (expected is null)
            {
                continue;
            }

            Assert.Equal(expected, table.GetCell(row, PlayerColumns.CharacterBodyType));
            checkedRows++;
        }

        Assert.True(checkedRows >= 80, $"only {checkedRows} players were checked; the fixture changed.");
    }

    [Fact]
    public void AReceiverDoesNotKeepTheBuildOfTheLinemanWhoseSlotHeTook()
    {
        using var folder = new TempDirectory();
        var result = Generate(folder);
        var table = CsvDocument.Load(result.OutputPath);

        // The donor fixture is a real 85-man roster, so its heavy builds sit on
        // its heavy slots. What must not survive is a Heavy build on somebody
        // the roster made 180 lb.
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.IsEmpty) == "true" ||
                table.GetCell(row, PlayerColumns.TeamIndex) != "27")
            {
                continue;
            }

            var weight = int.Parse(table.GetCell(row, PlayerColumns.Weight)) + 160;
            var build = table.GetCell(row, PlayerColumns.CharacterBodyType);
            if (weight < 220)
            {
                Assert.NotEqual("Heavy", build);
            }
        }
    }

    [Fact]
    public void AFilledDepthSlotGetsOneToo()
    {
        using var folder = new TempDirectory();
        var result = Generate(folder);

        var filled = result.Teams.SelectMany(t => t.FilledSlots).Select(s => s.RowKey).ToHashSet();
        Assert.NotEmpty(filled);

        var table = CsvDocument.Load(result.OutputPath);
        var model = Model;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (!int.TryParse(table.GetCell(row, PlayerColumns.Row), out var key) || !filled.Contains(key))
            {
                continue;
            }

            var expected = model.For(
                table.GetCell(row, PlayerColumns.Position),
                int.Parse(table.GetCell(row, PlayerColumns.Height)),
                int.Parse(table.GetCell(row, PlayerColumns.Weight)) + 160);
            if (expected is not null)
            {
                Assert.Equal(expected, table.GetCell(row, PlayerColumns.CharacterBodyType));
            }
        }
    }

    [Fact]
    public void TheReportSaysHowTheBuildWasChosen()
    {
        using var folder = new TempDirectory();
        Generate(folder);

        var report = File.ReadAllText(folder.File("report.txt"));
        Assert.Contains("CharacterBodyType", report, StringComparison.Ordinal);
        Assert.Contains("Lean", report, StringComparison.Ordinal);
    }
}
