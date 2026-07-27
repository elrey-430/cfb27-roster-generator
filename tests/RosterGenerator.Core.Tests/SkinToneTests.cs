using RosterGenerator.Core.Appearance;
using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Equipment;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Appearance rides along with the face.
///
/// <para>EA's <c>skinTone</c> is 1 (lightest) to 8 (darkest), and it is spelled
/// out twice: as a field inside the CharacterVisuals blob, and as the sixth
/// segment of a generated head's own name. Across 3,144 generic-headed players
/// in a base save the two agree 3,144 times and disagree none. Better still, a
/// given generated head is only ever used at one tone — 1,607 distinct heads,
/// none at two — so choosing a player's face IS choosing their tone, and the
/// visuals table never has to be written for this.</para>
///
/// <para>The tone is <b>supplied, never inferred.</b> The generator will not
/// guess what a real person looked like from their name, hometown or position.
/// A blank cell keeps the appearance the roster slot already had.</para>
/// </summary>
public sealed class SkinToneTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    [Theory]
    [InlineData("Generic_0877_P_T0042_H_6_3", 6, 877)]
    [InlineData("Generic_1983_P_T0096_M_1_4", 1, 1983)]
    [InlineData("Generic_3060_P_T0146_D_7_2", 7, 3060)]
    public void AGeneratedHeadSpellsOutItsOwnSkinTone(string assetName, int tone, int portrait)
    {
        var head = HeadAsset.Parse(assetName);
        Assert.Equal(HeadAssetKind.Generic, head.Kind);
        Assert.Equal(tone, head.SkinTone);
        Assert.Equal(portrait, head.Portrait);
        Assert.True(head.HasSkinTone);
    }

    [Fact]
    public void AHeadThatIsNotAGeneratedOneClaimsNoTone()
    {
        foreach (var name in new[] { "Unique_AllenJordan_1234", "", "something_else" })
        {
            Assert.False(HeadAsset.Parse(name).HasSkinTone);
        }
    }

    [Fact]
    public void EveryGeneratedHeadInTheFixtureAgreesWithItsVisualsRow()
    {
        // The claim the whole feature rests on, checked against real data
        // rather than asserted: the name and the blob never disagree.
        // EquipmentDynasty is the fixture that carries a visuals table.
        var export = DynastyExport.Open(TestsPath("EquipmentDynasty"));
        var visuals = export.LoadCharacterVisuals();
        Assert.NotNull(visuals);

        var roster = export.LoadPlayerRoster();
        var checkedHeads = 0;
        foreach (var player in roster.Players)
        {
            if (!player.HasColumn(PlayerColumns.CharacterVisuals))
            {
                break;
            }

            var head = HeadAsset.Parse(player.GetRaw(PlayerColumns.GenericHeadAssetName));
            if (!head.HasSkinTone)
            {
                continue;
            }

            var rowId = CharacterVisualsReference.RowId(player.GetRaw(PlayerColumns.CharacterVisuals));
            if (rowId is not int row || visuals!.GetSkinTone(row) is not int tone)
            {
                continue;
            }

            checkedHeads++;
            Assert.True(head.SkinTone == tone,
                $"{head.AssetName} names skin tone {head.SkinTone} but its visuals row says {tone}");
        }

        // The committed fixture is one team, so 14 of its 85 slots carry a
        // generated head. The full base save agrees on all 3,144 of them.
        Assert.True(checkedHeads >= 14, $"only {checkedHeads} heads were checked — the guard is idle");
    }

    [Fact]
    public void APoolDrawsAtTheToneAskedForWhenItHasOne()
    {
        var pool = HeadAssetPool.Build(DynastyExport.Open(TestsPath("DonorDynasty")).LoadPlayerRoster());
        Assert.NotEmpty(pool.AvailableSkinTones);

        foreach (var tone in pool.AvailableSkinTones)
        {
            for (var seed = 1; seed < 40; seed++)
            {
                var drawn = pool.Draw(seed, tone);
                Assert.NotNull(drawn);
                Assert.Equal(tone, drawn!.Value.SkinTone);
            }
        }
    }

    [Fact]
    public void AToneThePoolCannotSupplyFallsBackToTheNearestOne()
    {
        var pool = HeadAssetPool.Build(DynastyExport.Open(TestsPath("DonorDynasty")).LoadPlayerRoster());
        var available = pool.AvailableSkinTones.ToHashSet();

        // Ask for every tone in EA's range, including any this export has no
        // faces for. Nothing may fail, and the miss must be as small as it can
        // possibly be — an adjacent tone, since 1 is lightest and 8 darkest.
        for (var wanted = HeadAsset.MinimumSkinTone; wanted <= HeadAsset.MaximumSkinTone; wanted++)
        {
            var drawn = pool.Draw(seed: 7, preferredSkinTone: wanted);
            Assert.NotNull(drawn);

            var got = drawn!.Value.SkinTone;
            if (available.Contains(wanted))
            {
                Assert.Equal(wanted, got);
                continue;
            }

            var closest = available.Min(t => Math.Abs(t - wanted));
            Assert.Equal(closest, Math.Abs(got - wanted));
        }
    }

    [Fact]
    public void TheSameSeedAlwaysDrawsTheSameFace()
    {
        var pool = HeadAssetPool.Build(DynastyExport.Open(TestsPath("DonorDynasty")).LoadPlayerRoster());
        var tone = pool.AvailableSkinTones.First();
        for (var seed = 1; seed < 25; seed++)
        {
            Assert.Equal(pool.Draw(seed, tone), pool.Draw(seed, tone));
            Assert.Equal(pool.Draw(seed), pool.Draw(seed));
        }
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("9", false)]
    [InlineData("dark", false)]
    [InlineData("1", true)]
    [InlineData("8", true)]
    public void AnOutOfRangeSkinToneIsRefusedRatherThanClamped(string cell, bool accepted)
    {
        var path = Path.Combine(Path.GetTempPath(), $"skintone-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path,
            "FirstName,LastName,Position,Team,Season,SkinTone\n" +
            $"Test,Player,QB,Florida State,2014,{cell}\n");
        try
        {
            var read = HistoricalCsv.Read(path);
            var player = read.Roster.Players.Single();
            if (accepted)
            {
                Assert.Equal(int.Parse(cell), player.SkinTone);
                Assert.DoesNotContain(read.Warnings, w => w.Contains("SkinTone", StringComparison.Ordinal));
            }
            else
            {
                // Clamping "9" to 8 would hand back a player the user did not
                // ask for with nothing on screen to say so.
                Assert.Null(player.SkinTone);
                Assert.Contains(read.Warnings, w => w.Contains("SkinTone", StringComparison.Ordinal));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Converts one player onto the Florida State fixture.</summary>
    private static (PlayerConversionEntry Entry, Model.Player Slot) ConvertOne(
        int? skinTone, bool replaceFaces = true, string fixture = "DonorDynasty")
    {
        var export = DynastyExport.Open(TestsPath(fixture));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var player = new HistoricalPlayer
        {
            FirstName = "Appearance", LastName = "Test", Position = "QB", SkinTone = skinTone,
        };
        var roster = new HistoricalRoster
        {
            School = "Florida State", Season = 2014, Players = new List<HistoricalPlayer> { player },
        };

        var report = new HistoricalTeamConverter(
                export.BuildTeamMappings(),
                PositionMappingSet.Load(FixturePath("PositionMappings.json")),
                replaceRealPersonFaces: replaceFaces,
                characterVisuals: export.LoadCharacterVisuals())
            .Convert(session, roster);

        var entry = report.Converted.Single();
        var slot = donor.Players.Single(p => p.RowKey == entry.AssignedRowKey);
        return (entry, slot);
    }

    [Fact]
    public void ARequestedToneIsHonoured()
    {
        foreach (var wanted in new[] { 1, 4, 8 })
        {
            var (_, slot) = ConvertOne(wanted);
            var written = HeadAsset.Parse(slot.GetRaw(PlayerColumns.GenericHeadAssetName));
            Assert.Equal(HeadAssetKind.Generic, written.Kind);

            // Exact when the fixture has that tone, nearest otherwise — but
            // never simply ignored.
            var pool = HeadAssetPool.Build(
                DynastyExport.Open(TestsPath("DonorDynasty")).LoadPlayerRoster());
            var expected = pool.AvailableSkinTones.Contains(wanted)
                ? wanted
                : pool.AvailableSkinTones.OrderBy(t => Math.Abs(t - wanted)).ThenByDescending(t => t).First();
            Assert.Equal(expected, written.SkinTone);
        }
    }

    [Fact]
    public void ReplacingARealPersonsFaceKeepsTheSlotsOwnSkinTone()
    {
        // The point: swapping a scan for a generated face must not also change
        // how the player looks. Without this the tone jumps at random.
        var export = DynastyExport.Open(TestsPath("EquipmentDynasty"));
        var visuals = export.LoadCharacterVisuals();
        Assert.NotNull(visuals);

        var (_, slot) = ConvertOne(skinTone: null, fixture: "EquipmentDynasty");

        var written = HeadAsset.Parse(slot.GetRaw(PlayerColumns.GenericHeadAssetName));
        if (written.Kind != HeadAssetKind.Generic)
        {
            return; // the slot kept its own head; nothing to check
        }

        var rowId = CharacterVisualsReference.RowId(slot.GetRaw(PlayerColumns.CharacterVisuals));
        if (rowId is not int row || visuals!.GetSkinTone(row) is not int slotTone)
        {
            return; // no tone recorded for this slot, so there was nothing to keep
        }

        var pool = HeadAssetPool.Build(export.LoadPlayerRoster());
        var expected = pool.AvailableSkinTones.Contains(slotTone)
            ? slotTone
            : pool.AvailableSkinTones.OrderBy(t => Math.Abs(t - slotTone)).ThenByDescending(t => t).First();
        Assert.Equal(expected, written.SkinTone);
    }

    [Fact]
    public void InheritingFacesIgnoresTheColumnAndSaysSo()
    {
        // --faces inherit means inherit. Quietly changing a face anyway would
        // make the flag a lie.
        var (entry, slot) = ConvertOne(skinTone: 8, replaceFaces: false);
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var original = export.LoadPlayerRoster().Players.Single(p => p.RowKey == entry.AssignedRowKey);

        Assert.Equal(
            original.GetRaw(PlayerColumns.GenericHeadAssetName),
            slot.GetRaw(PlayerColumns.GenericHeadAssetName));
        Assert.Contains(entry.Warnings, w => w.Contains("SkinTone", StringComparison.Ordinal));
    }
}
