using RosterGenerator.Core.Legacy;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Writing a CFB27 team out into a PS2-era roster file.
///
/// <para>The guarantees worth having are about restraint: only the squad you
/// asked for changes, nobody moves position on the way across, and everyone
/// who could not come is named. A tool that silently reshuffles somebody's
/// roster file is worse than one that refuses.</para>
/// </summary>
public sealed class LegacyExportTests
{
    private static readonly LegacyRatingScale Scale =
        LegacyRatingScale.Load(TestFixtures.DataPath("LegacyRatingScale.json"));

    private sealed class Run : IDisposable
    {
        public Run()
        {
            Source = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Output = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllBytes(Source, LegacyRosterTests.LittleEndianSquadFixture());
            Result = LegacyRosterExporter.Export(
                Source, Output, TestFixtures.LoadSampleRoster(),
                teamIndex: 27, legacyTeamId: 9, teamName: "Florida State", Scale);
        }

        public string Source { get; }

        public string Output { get; }

        public LegacyExportResult Result { get; }

        public void Dispose()
        {
            File.Delete(Source);
            File.Delete(Output);
        }
    }

    [Fact]
    public void ThePlayersWrittenAreTheOnesCfb27Has()
    {
        using var run = new Run();

        // The sample's Florida State players are an RE, a QB and an SS, and
        // the fixture squad has a slot for each.
        Assert.Equal(
            new[] { "QB", "RE", "SS" },
            run.Result.Written.Select(w => w.Position).OrderBy(p => p, StringComparer.Ordinal));
        Assert.Contains(run.Result.Written, w => w.Name == "Tanner Applewhite" && w.Position == "QB");
        Assert.Contains(run.Result.Written, w => w.Name == "Ashlynd Barker" && w.Position == "SS");
    }

    [Fact]
    public void NobodyChangesPositionOnTheWayAcross()
    {
        using var run = new Run();
        var roster = LegacyRosterReader.Read(EaDbFile.Read(run.Output));
        var squad = roster.Teams.Single(t => t.TeamId == 9);

        foreach (var written in run.Result.Written)
        {
            var player = squad.Players.Single(p => $"{p.FirstName} {p.LastName}" == written.Name);
            Assert.Equal(written.Position, player.Position);
        }
    }

    [Fact]
    public void ASlotCfb27HasNobodyForKeepsThePlayerItHad()
    {
        // A left tackle is better than a safety standing where the left
        // tackle was, so an unfillable slot is left alone and reported.
        using var run = new Run();
        var squad = LegacyRosterReader.Read(EaDbFile.Read(run.Output)).Teams.Single(t => t.TeamId == 9);

        Assert.Contains(squad.Players, p => p.LastName == "Tackle" && p.Position == "LT");
        Assert.Contains(run.Result.Unfilled, u => u.StartsWith("LT", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDeepestManOnTheChartIsTheOneCut()
    {
        // Florida State has two ends in the sample but the fixture squad has
        // two RE slots, so nothing is cut there; the second end takes the
        // second slot. What matters is that a cut is named with its depth.
        using var run = new Run();
        Assert.All(run.Result.Cut, c => Assert.Contains("on the chart", c));
    }

    [Fact]
    public void OverallsComeOutOnTheScaleTheGameShows()
    {
        using var run = new Run();

        foreach (var written in run.Result.Written)
        {
            // Five bits is 32 steps over 0-99, so a rating can move by up to
            // half a step. It must never move further than that.
            Assert.InRange(written.WrittenOverall, written.Cfb27Overall - 2, written.Cfb27Overall + 2);
        }
    }

    [Fact]
    public void EveryOtherTeamInTheFileIsLeftByteForByteAlone()
    {
        // The whole design: this changes one squad's fields and nothing else.
        using var run = new Run();
        var before = EaDbFile.Parse(File.ReadAllBytes(run.Source));
        var after = EaDbFile.Parse(File.ReadAllBytes(run.Output));
        var touched = LegacyRosterReader.Read(before).Teams.Single(t => t.TeamId == 9)
            .Players.Select(p => p.PlayerId).ToHashSet();

        var play = before.Tables[LegacySchema.PlayerTable];
        var playAfter = after.Tables[LegacySchema.PlayerTable];
        for (var row = 0; row < play.Capacity; row++)
        {
            if (touched.Contains(play.Read(row, LegacySchema.PlayerId)))
            {
                continue;
            }

            foreach (var field in play.Fields.Where(f => f.Bits <= 32))
            {
                Assert.Equal(play.Read(row, field.Name), playAfter.Read(row, field.Name));
            }
        }
    }

    [Fact]
    public void ThePs3GenerationIsRefusedRatherThanGuessedAt()
    {
        // Its checksums are not worked out; writing one would produce a file
        // that may or may not load, with no way to tell which.
        var source = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(source, LegacyRosterTests.BigEndianFixture());
            var error = Assert.Throws<InvalidDataException>(() => LegacyRosterExporter.Export(
                source, source + ".out", TestFixtures.LoadSampleRoster(), 27, 3, "Alabama", Scale));
            Assert.Contains("PS3", error.Message);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public void ASchoolCfb27OnlyHasAStandInForIsNotAnExportSource()
    {
        // Idaho is the live case. CFB27 has no Idaho, so TeamMappings.json
        // points it at a generic FCS squad to be GENERATED INTO — and every
        // one of those shares team index 255 with the game's whole recruiting
        // pool. Reading OUT of 255 would hand the PS2 file 4,527 recruits
        // under Idaho's name, so the pairing has to refuse it and say why.
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllText(path, LegacyTeamIdsJson);
            var teams = TeamMappingSet.Build(new (int, IReadOnlyList<string>)[]
            {
                (3, new[] { "Alabama" }),
                (PlayerSchema.NoTeamSentinel, new[] { "Idaho" }),
            });

            var paired = LegacyTeamPairing.Pair(path, teams, out var unpaired);

            Assert.Equal(new[] { "Alabama" }, paired.Select(p => p.School));
            Assert.DoesNotContain(paired, p => p.Cfb27TeamIndex == PlayerSchema.NoTeamSentinel);
            Assert.Contains(unpaired, u => u.Contains("Idaho") && u.Contains("stand-in"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NamingSuchASchoolOutrightFindsNothingRatherThanTheRecruitPool()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllText(path, LegacyTeamIdsJson);
            var teams = TeamMappingSet.Build(new (int, IReadOnlyList<string>)[]
            {
                (3, new[] { "Alabama" }),
                (PlayerSchema.NoTeamSentinel, new[] { "Idaho" }),
            });

            Assert.Null(LegacyTeamPairing.Find(path, teams, "Idaho"));
            Assert.Equal(3, LegacyTeamPairing.Find(path, teams, "Alabama")!.Cfb27TeamIndex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const string LegacyTeamIdsJson =
        "{\"teams\":[{\"teamId\":3,\"school\":\"Alabama\"},{\"teamId\":41,\"school\":\"Idaho\"}]}";
}
