using System.Text.Json;
using RosterGenerator.Core.Mapping;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Schools the game no longer carries.
///
/// <para>Idaho played FBS football for decades and CFB27 does not have them, so
/// a 2004 Idaho roster had nowhere at all to go. The game does ship five
/// generic FCS teams with real 85-man rosters, and a departed school is written
/// onto one of those — but not by <c>TeamIndex</c>, because all five share
/// index 255 with the several thousand players in the recruiting pool.</para>
/// </summary>
public class StandInTeamTests
{
    private static string MappingFile => Path.Combine(AppContext.BaseDirectory, "Fixtures", "TeamMappings.json");

    [Fact]
    public void IdahoIsWrittenOntoFcsEast()
    {
        var mappings = TeamMappingSet.Load(MappingFile);

        Assert.Equal("FCS East", mappings.StandInTeam("Idaho"));
        Assert.Equal("FCS East", mappings.StandInTeam("idaho"));
    }

    [Fact]
    public void AnOrdinarySchoolStandsInForNobody()
    {
        var mappings = TeamMappingSet.Load(MappingFile);

        Assert.Null(mappings.StandInTeam("Alabama"));
        Assert.Null(mappings.StandInTeam("USC"));
    }

    [Fact]
    public void EveryGenericFcsTeamIsReachableByName()
    {
        // All five share TeamIndex 255, so the id can never tell them apart —
        // only the stand-in name can, and each must name itself.
        var mappings = TeamMappingSet.Load(MappingFile);

        foreach (var team in new[]
                 { "FCS East", "FCS Midwest", "FCS Northwest", "FCS Southeast", "FCS West" })
        {
            Assert.Equal(team, mappings.StandInTeam(team));
        }
    }

    [Fact]
    public void StandInTeamsAllUseTheNoTeamSentinel()
    {
        // If one of them ever gained a real index this test should fail loudly,
        // because the roster-table lookup would then be unnecessary for it.
        using var document = JsonDocument.Parse(File.ReadAllText(MappingFile));
        var standIns = document.RootElement.GetProperty("teams").EnumerateArray()
            .Where(t => t.TryGetProperty("standInTeam", out _))
            .ToList();

        Assert.NotEmpty(standIns);
        Assert.All(standIns, t => Assert.Equal(
            RosterGenerator.Core.Schema.PlayerSchema.NoTeamSentinel,
            t.GetProperty("teamId").GetInt32()));
    }

    [Fact]
    public void ADepartedSchoolResolvesEvenThoughItHasNoTeamOfItsOwn()
    {
        // The dynasty's Team table deliberately drops rows carrying the no-team
        // sentinel, so an overlay entry for one is admitted on the strength of
        // its stand-in rather than its id. Without that, Idaho fails to resolve
        // at all and the whole roster is refused.
        var entries = TeamMappingSet.LoadEntriesWithStandIns(MappingFile);
        var idaho = entries.Single(e => e.Names.Contains("Idaho"));

        Assert.Equal(RosterGenerator.Core.Schema.PlayerSchema.NoTeamSentinel, idaho.TeamId);
        Assert.Equal("FCS East", idaho.StandIn);
    }
}
