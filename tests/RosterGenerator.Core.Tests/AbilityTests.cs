using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// How good a recreated player is in their ability slots.
///
/// <para>The save stores a physical ability as a <b>tier only</b>. Nothing on
/// the player says which ability slot 3 is — that comes from position and
/// archetype in the game's own data, which a save does not carry. So none of
/// this chooses abilities; it decides how many of a player's slots are filled,
/// which of them, and at what tier, all from the overall the rating engine
/// already produced.</para>
///
/// <para>The distribution is measured from a base save
/// (<c>data/AbilityModel.json</c>), so the tests that matter are the ones
/// checking the tool reproduces it rather than merely writing something.</para>
/// </summary>
public sealed class AbilityTests
{
    private static AbilityModel Model() =>
        AbilityModel.Load(TestFixtures.DataPath("AbilityModel.json"));

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

    private static double ShareWithAnyAbility(AbilityModel model, int overall, int sample = 4000)
    {
        var with = 0;
        for (var seed = 0; seed < sample; seed++)
        {
            if (model.For("DT_PurePower", "DT", overall, seed).PhysicalTiers.Count > 0)
            {
                with++;
            }
        }

        return with / (double)sample;
    }

    // ---- It reproduces what the game does ---------------------------------

    [Theory]
    // The measured share of players at each overall who have at least one
    // ability slot filled. A tolerance of 4 points is wider than the sampling
    // error and far narrower than the effect, which runs 12% to 99%.
    [InlineData(62, 0.117)]
    [InlineData(67, 0.183)]
    [InlineData(72, 0.276)]
    [InlineData(77, 0.441)]
    [InlineData(82, 0.744)]
    [InlineData(87, 0.974)]
    public void TheShareOfPlayersWithAnAbilityMatchesTheGame(int overall, double expected)
    {
        var actual = ShareWithAnyAbility(Model(), overall);

        Assert.True(Math.Abs(actual - expected) < 0.04,
            $"at OVR {overall} the tool gives {actual:P1} of players an ability; the game gives {expected:P1}.");
    }

    [Fact]
    public void BetterPlayersGetMoreAbilities()
    {
        // The single property the whole feature rests on, stated directly
        // rather than inferred from the bands above.
        var model = Model();
        var shares = new[] { 60, 70, 80, 90 }.Select(o => ShareWithAnyAbility(model, o)).ToList();

        Assert.True(shares.SequenceEqual(shares.OrderBy(s => s)),
            $"ability share does not rise with overall: {string.Join(", ", shares.Select(s => s.ToString("P1")))}");
    }

    [Fact]
    public void TheBestPlayersGetTheBestTiers()
    {
        var model = Model();

        double PlatinumShare(int overall) =>
            Enumerable.Range(0, 4000)
                .SelectMany(seed => model.For("DT_PurePower", "DT", overall, seed).PhysicalTiers.Values)
                .DefaultIfEmpty("None")
                .Count(t => t == "Platinum") / 4000.0;

        // Measured: platinum is 1.1% of filled slots at 65-69 and 28.5% at
        // 90-94. A gold-plated walk-on is the thing to avoid.
        Assert.True(PlatinumShare(92) > PlatinumShare(67) * 5,
            "elite players are not getting meaningfully better tiers than fringe ones.");
    }

    // ---- The slots belong to the archetype --------------------------------

    [Fact]
    public void AnArchetypeFillsItsOwnSlotsFirst()
    {
        var model = Model();

        // Measured: 600 of the 696 DT_PurePower players in a base save who have
        // exactly one ability have it in slot 4, and every KP_Power has slot 3.
        // Slot 4 on a nose tackle is not slot 4 on a kicker, so the ordering is
        // the only thing keeping a recreated player's abilities plausible.
        var tackle = FirstSlotUsed(model, "DT_PurePower", "DT");
        var kicker = FirstSlotUsed(model, "KP_Power", "K");

        Assert.Equal(4, tackle);
        Assert.Equal(3, kicker);
    }

    private static int FirstSlotUsed(AbilityModel model, string archetype, string position)
    {
        for (var seed = 0; seed < 4000; seed++)
        {
            var abilities = model.For(archetype, position, 78, seed);
            if (abilities.PhysicalTiers.Count == 1)
            {
                return abilities.PhysicalTiers.Keys.Single();
            }
        }

        throw new Xunit.Sdk.XunitException($"no single-slot {archetype} appeared in 4,000 draws.");
    }

    [Fact]
    public void AnUnknownArchetypeStillGetsSomethingPlausible()
    {
        // A dynasty carrying an archetype the measurement never saw must not
        // crash or silently give up; it falls back to slot order 1..5.
        var abilities = Model().For("NOT_AN_ARCHETYPE", "QB", 90, seed: 7);

        Assert.NotEmpty(abilities.PhysicalTiers);
        Assert.All(abilities.PhysicalTiers.Keys, slot => Assert.InRange(slot, 1, 5));
    }

    // ---- Mental abilities are rare, elite, and never invented --------------

    [Fact]
    public void MentalAbilitiesAreForTheEliteOnly()
    {
        var model = Model();

        double Share(int overall) =>
            Enumerable.Range(0, 4000).Count(s => model.For("QB_FieldGeneral", "QB", overall, s).Mental.Count > 0)
            / 4000.0;

        // Measured: 2.1% of a base save carry any, essentially none below 80.
        Assert.True(Share(70) < 0.02, $"fringe players are getting mental abilities ({Share(70):P1}).");
        Assert.True(Share(92) > 0.4, $"elite players are not getting them ({Share(92):P1}).");
    }

    [Fact]
    public void APlayerWithMentalAbilitiesGetsAllThree()
    {
        // 244 of the 248 players in a base save who have any have the full set,
        // so a partial one would be the unusual case, not the ordinary one.
        var model = Model();
        var seen = 0;
        for (var seed = 0; seed < 2000 && seen < 20; seed++)
        {
            var mental = model.For("QB_FieldGeneral", "QB", 93, seed).Mental;
            if (mental.Count == 0)
            {
                continue;
            }

            Assert.Equal(3, mental.Count);
            Assert.Equal(3, mental.Keys.Distinct(StringComparer.Ordinal).Count());
            seen++;
        }

        Assert.True(seen > 0, "no elite quarterback drew a mental ability in 2,000 tries.");
    }

    [Fact]
    public void APositionOnlyGetsAbilitiesTheGameGivesThatPosition()
    {
        // Reassignment, never authoring: a kicker may be given ClutchKicker
        // because the game gives kickers ClutchKicker, and a quarterback may
        // not, because it never has.
        var model = Model();
        var quarterback = new HashSet<string>(StringComparer.Ordinal);
        var kicker = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 0; seed < 3000; seed++)
        {
            foreach (var name in model.For("QB_FieldGeneral", "QB", 93, seed).Mental.Keys)
            {
                quarterback.Add(name);
            }

            foreach (var name in model.For("KP_Power", "K", 93, seed).Mental.Keys)
            {
                kicker.Add(name);
            }
        }

        Assert.DoesNotContain("ClutchKicker", quarterback);
        Assert.DoesNotContain("OLRally", quarterback);
        Assert.DoesNotContain("FieldGeneral", kicker);
    }

    // ---- Determinism ------------------------------------------------------

    [Fact]
    public void TheSamePlayerInTheSameSlotAlwaysGetsTheSameAbilities()
    {
        var model = Model();
        var first = model.For("WR_DeepThreat", "WR", 88, seed: 4242);
        var again = model.For("WR_DeepThreat", "WR", 88, seed: 4242);

        Assert.Equal(first.PhysicalTiers, again.PhysicalTiers);
        Assert.Equal(first.Mental, again.Mental);
    }

    [Fact]
    public void HowManySlotsAPlayerGetsDoesNotDecideTheirTiers()
    {
        // Both are drawn from one seed, so they need independent streams. If
        // they shared one, every player with three slots would land on the same
        // tier and the roster would look banded.
        var model = Model();
        var tiers = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 0; seed < 500; seed++)
        {
            foreach (var tier in model.For("DT_PurePower", "DT", 88, seed).PhysicalTiers.Values)
            {
                tiers.Add(tier);
            }
        }

        Assert.True(tiers.Count >= 3, $"only {tiers.Count} distinct tier(s) across 500 players.");
    }

    // ---- End to end -------------------------------------------------------

    [Fact]
    public void ARecreatedPlayerNeverKeepsTheReplacedPlayersAbilities()
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

        var donor = CsvDocument.Load(TestsPath("DonorDynasty", "0152_Player.csv"));
        var donorBySlot = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var row = 0; row < donor.RowCount; row++)
        {
            donorBySlot[donor.GetCell(row, PlayerColumns.Row)] = Enumerable.Range(1, 5)
                .Select(s => donor.GetCell(row, PlayerColumns.PhysicalAbility(s))).ToArray();
        }

        // Every slot on the team is decided fresh — the ones a historical
        // player took over AND the ones filled as depth. A filled slot keeping
        // its old abilities was a real defect: a 63-overall walk-on was left
        // holding two Silvers from the player before him.
        var table = CsvDocument.Load(result.OutputPath);
        var checkedRows = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.TeamIndex) != "27")
            {
                continue;
            }

            var overall = int.Parse(table.GetCell(row, PlayerColumns.OverallRating));
            var written = Enumerable.Range(1, 5)
                .Select(s => table.GetCell(row, PlayerColumns.PhysicalAbility(s))).ToArray();
            var before = donorBySlot[table.GetCell(row, PlayerColumns.Row)];

            // A fringe player holding gold is the signature of an inherited
            // slot, and it is what this is really guarding.
            if (overall < 70 && written.SequenceEqual(before) && before.Any(t => t != "None"))
            {
                Assert.Fail(
                    $"row {table.GetCell(row, PlayerColumns.Row)} (OVR {overall}) kept the replaced " +
                    $"player's abilities: {string.Join(", ", before)}");
            }

            checkedRows++;
        }

        Assert.Equal(85, checkedRows);
    }

    [Fact]
    public void TheReportSaysWhatEachPlayerGotAndWhatItDoesNotDecide()
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

        var report = File.ReadAllText(result.ReportPath);

        Assert.Contains("Abilities:", report);
        // The limitation is stated where a user will read it, because "we set
        // your abilities" would be a claim this cannot make.
        Assert.Contains("what is set here is the tier", report);
    }

    [Fact]
    public void InheritedRatingsLeaveAbilitiesAlone()
    {
        using var temp = new TempDirectory();

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            OutputPath = temp.File("roster.csv"),
            ReportPath = temp.File("report.txt"),
            Ratings = RatingsMode.Inherit,
            SelectArchetypes = false,
            FillRoster = false,
        });

        // Abilities are read off a generated overall. With ratings inherited
        // there is no such number, so writing tiers would be inventing them.
        Assert.DoesNotContain("Abilities:", File.ReadAllText(result.ReportPath));
    }
}
