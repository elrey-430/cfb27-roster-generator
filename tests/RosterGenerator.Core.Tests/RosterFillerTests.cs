using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A CFB27 team always carries 85 players, so any slot a user's roster does
/// not supply keeps its original fictional player. The game builds its depth
/// chart from ratings alone, so those leftovers start unless something holds
/// them down. These tests pin that behaviour.
/// </summary>
public sealed class RosterFillerTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private static RatingEngine Engine() =>
        TestFixtures.RatingEngine();

    private static RosterDepthModel Depth() => RosterDepthModel.Load(FixturePath("RosterDepth.json"));

    /// <summary>Runs the FSU conversion with the fill enabled.</summary>
    private static (ConversionReport Report, Core.Model.PlayerRoster Donor) ConvertWithFill()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var engine = Engine();
        var converter = new HistoricalTeamConverter(
            export.BuildTeamMappings(),
            PositionMappingSet.Load(FixturePath("PositionMappings.json")),
            engine,
            ArchetypeSelector.Load(FixturePath("ArchetypeRules.json")),
            new RosterFiller(Depth(), engine));
        return (converter.Convert(session, csv.Roster), donor);
    }

    [Fact]
    public void DepthModelIsTheMeasuredCurveAndDescendsMonotonically()
    {
        var depth = Depth();

        Assert.Equal(85, depth.RosterSize);
        Assert.Equal(85, depth.MedianOverallByRank.Count);

        // Measured from 138 untouched FBS rosters: the best player is far
        // better than the 85th, and the curve never rises going down a roster.
        Assert.True(depth.OverallAtRank(1) > depth.OverallAtRank(85));
        for (var rank = 2; rank <= 85; rank++)
        {
            Assert.True(
                depth.OverallAtRank(rank) <= depth.OverallAtRank(rank - 1),
                $"rank {rank} rates above rank {rank - 1}");
        }

        // Ranks past the measured curve reuse its end rather than running off
        // into nonsense.
        Assert.Equal(depth.OverallAtRank(85), depth.OverallAtRank(120));
    }

    [Fact]
    public void RosterTailSkewsYoungLikeTheRealGame()
    {
        var depth = Depth();

        var top = depth.ClassWeightsAtRank(10);
        var bottom = depth.ClassWeightsAtRank(84);

        // The bottom of a real roster is mostly freshmen; the top is not.
        Assert.True(bottom["Freshman"] > 0.5, $"tail freshman share was {bottom["Freshman"]}");
        Assert.True(bottom["Freshman"] > top["Freshman"] * 2);
        Assert.True(top["Senior"] > bottom["Senior"]);
    }

    [Fact]
    public void FillCoversEveryUnsuppliedSlot()
    {
        var (report, _) = ConvertWithFill();

        Assert.Equal(75, report.Converted.Count());
        Assert.Equal(10, report.FilledSlots.Count);

        // When the fill runs, nothing is left over — the two lists are
        // alternatives, not both.
        Assert.Empty(report.LeftoverDonorSlots);

        // Ranks continue straight on from the historical roster.
        Assert.Equal(Enumerable.Range(76, 10), report.FilledSlots.Select(s => s.Rank));
    }

    [Fact]
    public void NoFilledPlayerCanOutRateTheHistoricalRosterAtItsPosition()
    {
        var (report, donor) = ConvertWithFill();

        var filledKeys = report.FilledSlots.Select(s => s.RowKey).ToHashSet();
        var team = donor.Players.Where(p => p.TeamIndex == report.TeamId).ToList();
        Assert.Equal(85, team.Count);

        var weakestHistorical = team
            .Where(p => !filledKeys.Contains(p.RowKey))
            .GroupBy(p => p.Position, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Min(p => p.OverallRating), StringComparer.Ordinal);

        foreach (var slot in report.FilledSlots)
        {
            if (!weakestHistorical.TryGetValue(slot.Position, out var weakest))
            {
                // No historical player at this position, so there is nobody to
                // displace.
                continue;
            }

            Assert.True(
                slot.Overall < weakest,
                $"{slot.Name} ({slot.Position}) filled at {slot.Overall} but the weakest historical " +
                $"{slot.Position} is {weakest} — this player would take the job.");
        }
    }

    [Fact]
    public void FillLowersTheLeftoversRatherThanLeavingThemAsStarters()
    {
        var (report, _) = ConvertWithFill();

        // The whole point: every one of these was rated highly enough to
        // matter before, and none is afterwards.
        Assert.All(report.FilledSlots, s => Assert.True(
            s.Overall < s.PreviousOverall,
            $"{s.Name} went from {s.PreviousOverall} to {s.Overall}"));

        Assert.Contains(report.FilledSlots, s => s.PreviousOverall >= 75);
        Assert.DoesNotContain(report.FilledSlots, s => s.Overall >= 75);
    }

    [Fact]
    public void FillKeepsNameJerseyAndPortrait()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var before = export.LoadPlayerRoster();
        var original = before.Players.ToDictionary(
            p => p.RowKey,
            p => (p.FirstName, p.LastName, p.JerseyNumber, Portrait: p.GetRaw(PlayerColumns.Portrait)));

        var (report, donor) = ConvertWithFill();
        var after = donor.Players.ToDictionary(p => p.RowKey);

        foreach (var slot in report.FilledSlots)
        {
            var player = after[slot.RowKey];
            var was = original[slot.RowKey];

            // The defect being fixed is the rating, not the identity: EA's
            // generated names are already realistic and the jersey numbers are
            // already unique within the team.
            Assert.Equal(was.FirstName, player.FirstName);
            Assert.Equal(was.LastName, player.LastName);
            Assert.Equal(was.JerseyNumber, player.JerseyNumber);
            Assert.Equal(was.Portrait, player.GetRaw(PlayerColumns.Portrait));
        }
    }

    [Fact]
    public void FilledOverallAgreesWithTheAttributesActuallyWritten()
    {
        var (report, donor) = ConvertWithFill();
        var formulas = OverallFormulaSet.Load(FixturePath("OverallFormulas.json"));
        var after = donor.Players.ToDictionary(p => p.RowKey);

        foreach (var slot in report.FilledSlots)
        {
            var player = after[slot.RowKey];
            var attributes = PlayerSchema.NumericRatingColumns
                .Where(donor.Document.HasColumn)
                .ToDictionary(a => a, a => (double)player.GetInt(a));

            var recomputed = formulas
                .Resolve(player.Position, player.GetRaw(PlayerColumns.PlayerType))
                .Compute(attributes);

            // A filler is written through the same engine as everyone else, so
            // the same guarantee has to hold: EA's own formula over the written
            // attributes reproduces the written overall.
            Assert.Equal(player.OverallRating, recomputed);
            Assert.Equal(slot.Overall, player.OverallRating);
        }
    }

    [Fact]
    public void FillIsDeterministic()
    {
        var first = ConvertWithFill().Report;
        var second = ConvertWithFill().Report;

        // The FSU regression asserts byte-identical output, so nothing in the
        // fill may depend on iteration order or randomness.
        Assert.Equal(
            first.FilledSlots.Select(s => (s.RowKey, s.Overall, s.ClassYear)),
            second.FilledSlots.Select(s => (s.RowKey, s.Overall, s.ClassYear)));
    }

    [Fact]
    public void PositionCeilingBeatsTheDepthCurveWhenTheHistoricalRosterIsWeak()
    {
        // In the FSU run the measured curve already lands below the historical
        // roster, so the ceiling never binds. It has to bind for a user whose
        // roster is weak — otherwise the curve would hand a filler at rank 3
        // an overall of 83 and put it straight into the starting lineup.
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var filler = new RosterFiller(Depth(), Engine());

        var slots = donor.Players
            .Where(p => p.TeamIndex == 27 && p.Position == "QB")
            .ToList();
        Assert.NotEmpty(slots);

        var filled = filler.Fill(
            session,
            slots,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["QB"] = 58 },
            placedCount: 2);

        Assert.All(filled, f => Assert.True(
            f.Overall < 58,
            $"{f.Name} filled at {f.Overall} against a weakest historical QB of 58"));
        Assert.All(filled, f => Assert.Contains("held", f.Reason));
    }

    [Fact]
    public void FloorStopsTheCeilingProducingAbsurdlyWeakPlayers()
    {
        var depth = Depth();
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var filler = new RosterFiller(depth, Engine());

        var slots = donor.Players.Where(p => p.TeamIndex == 27 && p.Position == "QB").ToList();

        // A user who supplies one terrible player must not drag the entire
        // rest of the roster down with them.
        var filled = filler.Fill(
            session,
            slots,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["QB"] = 20 },
            placedCount: 1);

        Assert.All(filled, f => Assert.True(
            f.Overall >= depth.MinimumOverall,
            $"{f.Name} filled at {f.Overall}, below the {depth.MinimumOverall} floor"));
        Assert.All(filled, f => Assert.Contains("floor", f.Reason));
    }

    [Fact]
    public void WithoutAFillerTheOriginalPlayersStayAndAreReported()
    {
        var export = DynastyExport.Open(TestsPath("DonorDynasty"));
        var csv = HistoricalCsv.Read(TestsPath("2023_FSU_Input.csv"));
        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);
        var report = new HistoricalTeamConverter(
                export.BuildTeamMappings(),
                PositionMappingSet.Load(FixturePath("PositionMappings.json")))
            .Convert(session, csv.Roster);

        Assert.Empty(report.FilledSlots);
        Assert.Equal(10, report.LeftoverDonorSlots.Count);
        Assert.Contains(report.GlobalWarnings, w => w.Contains("were not replaced"));
    }
}
