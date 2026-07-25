using RosterGenerator.Core.Appearance;
using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Replacing a player used to keep the roster slot's head, and most slots
/// carry a scan of a real person — 71 of the 85 in the donor fixture, and
/// 9,011 of 16,257 across a full base save. That put most of a recreated
/// roster in the recognisable faces of present-day players under other
/// people's names.
/// </summary>
public sealed class AppearanceTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static PositionMappingSet Positions() =>
        PositionMappingSet.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PositionMappings.json"));

    private static (PlayerRoster Donor, ConversionReport Report) Convert(bool replaceFaces = true)
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var report = new HistoricalTeamConverter(
                export.BuildTeamMappings(), Positions(),
                replaceRealPersonFaces: replaceFaces)
            .Convert(session, csv.Roster);
        return (donor, report);
    }

    private static IEnumerable<Player> TeamSlots(PlayerRoster donor) =>
        donor.Players.Where(p => p.TeamIndex == 27);

    private static HeadAsset HeadOf(Player p) =>
        HeadAsset.Parse(p.GetRaw(PlayerColumns.GenericHeadAssetName));

    [Theory]
    [InlineData("Unique_AbasiriJide_653", HeadAssetKind.RealPersonScan)]
    [InlineData("Generic_3759_P_T0178_D_2_4", HeadAssetKind.Generic)]
    [InlineData("Custom_Head_CAF", HeadAssetKind.Custom)]
    [InlineData("3_MorphHead", HeadAssetKind.Unknown)]
    [InlineData("", HeadAssetKind.Unknown)]
    public void HeadNamesAreClassified(string assetName, HeadAssetKind expected) =>
        Assert.Equal(expected, HeadAsset.Parse(assetName).Kind);

    [Fact]
    public void TheGenericNameCarriesItsOwnPortraitNumber()
    {
        // The two must be written together: a base save agrees on 7,243 of
        // 7,244 rows, so writing one without the other would desynchronise a
        // pairing the game maintains everywhere else.
        var head = HeadAsset.Parse("Generic_0303_P_T0015_D_7_4");
        Assert.Equal(303, head.Portrait);
    }

    [Fact]
    public void NoHistoricalPlayerIsLeftWearingARealPersonsFace()
    {
        var (donor, report) = Convert();
        var converted = report.Converted.Select(e => e.AssignedRowKey).OfType<int>().ToHashSet();

        var offenders = TeamSlots(donor)
            .Where(p => converted.Contains(p.RowKey) && HeadOf(p).IsRealPerson)
            .Select(p => $"{p.FirstName} {p.LastName}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheHeadAndPortraitStayConsistent()
    {
        var (donor, _) = Convert();

        foreach (var player in TeamSlots(donor))
        {
            var head = HeadOf(player);
            if (head.Kind != HeadAssetKind.Generic)
            {
                continue;
            }

            Assert.Equal(head.Portrait.ToString(), player.GetRaw(PlayerColumns.Portrait));
        }
    }

    [Fact]
    public void TheScansOwnAssetNameGoesWithIt()
    {
        // PLYR_ASSETNAME is set on all 9,011 scanned players and blank on
        // 4,100 generated ones, so clearing it is both attested in real data
        // and the thing that severs the last link to the real person.
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var wasAScan = export.LoadPlayerRoster().Players
            .Where(p => HeadOf(p).IsRealPerson)
            .Select(p => p.RowKey)
            .ToHashSet();

        var (donor, report) = Convert();
        var converted = report.Converted.Select(e => e.AssignedRowKey).OfType<int>().ToHashSet();

        var replaced = TeamSlots(donor)
            .Where(p => converted.Contains(p.RowKey) && wasAScan.Contains(p.RowKey))
            .ToList();
        Assert.NotEmpty(replaced);

        // Slots that already carried a generated face keep whatever asset name
        // they had — 3,144 players in a base save are in exactly that state, so
        // clearing theirs would be a change with no reason behind it.
        Assert.All(replaced, p => Assert.Equal("", p.GetRaw(PlayerColumns.AssetName)));
    }

    [Fact]
    public void SlotsThatAlreadyHadAGeneratedFaceAreNotChurned()
    {
        // Only the scans are a problem. Rewriting a face that was already
        // anonymous would be a diff with no reason behind it.
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var before = export.LoadPlayerRoster().Players
            .Where(p => p.TeamIndex == 27 && HeadOf(p).Kind == HeadAssetKind.Generic)
            .ToDictionary(p => p.RowKey, p => p.GetRaw(PlayerColumns.GenericHeadAssetName));

        var (donor, _) = Convert();

        foreach (var (rowKey, head) in before)
        {
            var after = donor.FindByRowKey(rowKey);
            Assert.NotNull(after);
            Assert.Equal(head, after!.GetRaw(PlayerColumns.GenericHeadAssetName));
        }
    }

    [Fact]
    public void SlotsNoHistoricalPlayerTookOverKeepTheirOwnIdentity()
    {
        // A leftover slot is still the game's own player, under their own
        // name. Their likeness is theirs and is left alone.
        var (donor, report) = Convert();
        var taken = report.Converted.Select(e => e.AssignedRowKey).OfType<int>().ToHashSet();
        var leftover = TeamSlots(donor).Select(p => p.RowKey).Where(k => !taken.Contains(k)).ToHashSet();
        Assert.NotEmpty(leftover);

        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        foreach (var original in export.LoadPlayerRoster().Players.Where(p => leftover.Contains(p.RowKey)))
        {
            var after = donor.FindByRowKey(original.RowKey);
            Assert.Equal(
                original.GetRaw(PlayerColumns.GenericHeadAssetName),
                after!.GetRaw(PlayerColumns.GenericHeadAssetName));
        }
    }

    [Fact]
    public void TheSameRosterAlwaysGetsTheSameFaces()
    {
        var first = Convert().Donor;
        var second = Convert().Donor;

        Assert.Equal(
            TeamSlots(first).Select(p => p.GetRaw(PlayerColumns.GenericHeadAssetName)).ToList(),
            TeamSlots(second).Select(p => p.GetRaw(PlayerColumns.GenericHeadAssetName)).ToList());
    }

    [Fact]
    public void EverySubstitutionIsReported()
    {
        var (_, report) = Convert();
        var noted = report.Converted.Count(e =>
            e.DefaultsUsed.Any(d => d.Contains("real player's likeness")));

        Assert.True(noted > 0, "no face substitution was reported");
    }

    [Fact]
    public void TurningItOffKeepsTheOldBehaviour()
    {
        var (donor, report) = Convert(replaceFaces: false);
        var converted = report.Converted.Select(e => e.AssignedRowKey).OfType<int>().ToHashSet();

        Assert.Contains(
            TeamSlots(donor).Where(p => converted.Contains(p.RowKey)),
            p => HeadOf(p).IsRealPerson);
    }

    [Fact]
    public void TheFacesUsedAreDrawnFromTheSaveItself()
    {
        // Nothing is invented: every face written is one this export already
        // carries, so it cannot be an asset the game does not have.
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var available = export.LoadPlayerRoster().Players
            .Select(p => p.GetRaw(PlayerColumns.GenericHeadAssetName))
            .ToHashSet(StringComparer.Ordinal);

        var (donor, _) = Convert();

        foreach (var player in TeamSlots(donor))
        {
            Assert.Contains(player.GetRaw(PlayerColumns.GenericHeadAssetName), available);
        }
    }

    [Fact]
    public void APoolWithNothingInItLeavesTheFaceAloneAndSaysSo()
    {
        // A dynasty whose players are all real scans has no anonymous face to
        // offer. Keeping the scan is wrong but honest; inventing a name would
        // be worse.
        var roster = PlayerRoster.Load(TestsPath("DonorDynasty", "0152_Player.csv"));
        foreach (var player in roster.Players)
        {
            player.SetRaw(PlayerColumns.GenericHeadAssetName, "Unique_Someone_1");
        }

        Assert.True(HeadAssetPool.Build(roster).IsEmpty);
        Assert.Null(HeadAssetPool.Build(roster).Draw(42));
    }

    [Fact]
    public void ThePoolSpreadsFacesRatherThanGivingEveryoneTheFirstOne()
    {
        var pool = HeadAssetPool.Build(
            PlayerRoster.Load(TestsPath("DonorDynasty", "0152_Player.csv")));
        Assert.True(pool.Count > 1, "fixture should carry several generated faces");

        var drawn = Enumerable.Range(1, 85)
            .Select(i => pool.Draw(i)!.Value.AssetName)
            .Distinct()
            .Count();

        Assert.True(drawn > 1, "every slot drew the same face");
    }
}
