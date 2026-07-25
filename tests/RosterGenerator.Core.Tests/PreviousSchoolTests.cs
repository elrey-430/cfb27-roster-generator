using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// <c>PLYR_PREVTEAMID</c> holds the school a transfer came from, as that
/// school's <c>TEAM_ORIGID</c> — a presentation-level id covering more schools
/// than the dynasty's own team list, and not a team index.
///
/// Established by measurement rather than assumption: 133 of the 135 distinct
/// non-zero values in a base save are a <c>TEAM_ORIGID</c> in the same save,
/// and resolving Florida State's 20 non-zero players gives real, plausible
/// schools. The remaining values sit below the Team table's range and stand
/// for schools the dynasty does not carry.
/// </summary>
public sealed class PreviousSchoolTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    [Fact]
    public void TeamsExposeTheSchoolIdPreviousSchoolIsWrittenWith()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var florida = export.Teams.Single(t => t.DisplayName == "Florida State");

        // The two id spaces are different, which is the whole reason this is a
        // separate lookup: Florida State is team 27 and school 1132.
        Assert.Equal(27, florida.TeamIndex);
        Assert.Equal(1132, florida.OriginalId);
        Assert.All(export.Teams, t => Assert.NotEqual(t.TeamIndex, t.OriginalId));
    }

    [Fact]
    public void AliasesResolveTheSaveSOwnSpellings()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var schools = export.BuildPreviousSchoolMappings(FixturePath("TeamMappings.json"));
        Assert.NotNull(schools);

        // The save writes "Mississippi St" and "W. Michigan"; a user writes
        // them out in full. The alias overlay is shared with team resolution
        // and translated into the school-id space.
        Assert.True(schools!.TryResolve("Mississippi State", out var mississippiState));
        Assert.Equal(
            export.Teams.Single(t => t.DisplayName == "Mississippi St").OriginalId, mississippiState);

        Assert.True(schools.TryResolve("Western Michigan", out var westernMichigan));
        Assert.Equal(
            export.Teams.Single(t => t.DisplayName == "W. Michigan").OriginalId, westernMichigan);
    }

    [Fact]
    public void TransfersGetTheirRealSchoolAndEveryoneElseIsCleared()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);

        var report = new HistoricalTeamConverter(
                export.BuildTeamMappings(),
                PositionMappingSet.Load(FixturePath("PositionMappings.json")),
                previousSchoolMappings: export.BuildPreviousSchoolMappings(FixturePath("TeamMappings.json")))
            .Convert(session, csv.Roster);

        var placed = report.Converted.ToDictionary(
            e => e.AssignedRowKey!.Value, e => e.Player);
        var schoolIdByName = export.Teams.ToDictionary(t => t.DisplayName, t => t.OriginalId);

        var transfers = 0;
        var nonFbs = 0;
        foreach (var player in donor.Players.Where(p => placed.ContainsKey(p.RowKey)))
        {
            var historical = placed[player.RowKey];
            var written = player.GetInt(PlayerColumns.PrevTeamId);

            if (historical.PreviousSchool is not { Length: > 0 } school)
            {
                // A player who never transferred must not inherit the donor
                // player's transfer history.
                Assert.Equal(PlayerSchema.NoPrevTeamIdSentinel, written);
                continue;
            }

            if (written == PlayerSchema.PrevTeamIdNotInDynasty)
            {
                // Albany, East Tennessee State and Shorter are genuinely not
                // FBS teams, so there is no id for them.
                Assert.DoesNotContain(school, schoolIdByName.Keys);
                nonFbs++;
                continue;
            }

            Assert.Contains(written, schoolIdByName.Values);
            transfers++;
        }

        // 23 of the 75 players transferred in. All but three came from an FBS
        // school the dynasty carries; Albany, East Tennessee State and Shorter
        // do not exist in it.
        Assert.Equal(20, transfers);
        Assert.Equal(3, nonFbs);
    }

    [Fact]
    public void PrevTeamIndexIsNotTouched()
    {
        // The two previous-team fields do not move together: PrevTeamIndex
        // reads 255 for every player in an untouched save, including the 20
        // Florida State players who carry a real PLYR_PREVTEAMID.
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var donor = export.LoadPlayerRoster();
        Assert.All(donor.Players, p =>
            Assert.Equal(PlayerSchema.NoTeamSentinel, p.GetInt(PlayerColumns.PrevTeamIndex)));

        var withPreviousSchool = donor.Players
            .Count(p => p.GetInt(PlayerColumns.PrevTeamId) != PlayerSchema.NoPrevTeamIdSentinel);
        Assert.True(withPreviousSchool > 0, "the donor fixture should carry transfers");

        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        var session = new RosterEditSession(donor);
        new HistoricalTeamConverter(
                export.BuildTeamMappings(),
                PositionMappingSet.Load(FixturePath("PositionMappings.json")),
                previousSchoolMappings: export.BuildPreviousSchoolMappings(FixturePath("TeamMappings.json")))
            .Convert(session, csv.Roster);

        Assert.All(donor.Players, p =>
            Assert.Equal(PlayerSchema.NoTeamSentinel, p.GetInt(PlayerColumns.PrevTeamIndex)));
    }
}
