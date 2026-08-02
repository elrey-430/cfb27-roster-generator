using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Depth;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A recreated roster takes the field in the right order.
///
/// <para><b>Reported.</b> Generated rosters had their depth charts "way out of
/// alignment". A depth chart points at player <em>rows</em>, in the order the
/// donor's players ranked; recreating a roster replaces who lives in each row
/// and leaves the chart alone, so the slot the game believes is the starting
/// quarterback ends up holding whoever landed there. Nothing in the game
/// corrects it — which is also what proves the game honours the stored chart
/// rather than re-sorting on load.</para>
///
/// <para>Three tables carry it: <c>Team.DepthChart</c> names a chart row, the
/// chart's 35 slots each name a <c>Player[]</c> row, and that row lists up to
/// six players in order. Rebuilding rewrites only the last of the three.</para>
/// </summary>
public sealed class DepthChartTests
{
    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static DepthChartSlotModel Model =>
        DepthChartSlotModel.Load(Path.Combine(DataDirectory, "DepthChartSlots.json"));

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    // ---- The reference encoding ---------------------------------------------

    [Fact]
    public void AReferenceSurvivesADecodeAndEncodeUnchanged()
    {
        // Every link in the chain is one of these, so getting a bit wrong here
        // would point a depth chart at an arbitrary player.
        const string cell = "00100001001100000000000011000110";
        var decoded = TableReference.Decode(cell);

        Assert.NotNull(decoded);
        Assert.Equal(cell, TableReference.Encode(decoded!.Value.Tag, decoded.Value.Row));
    }

    [Fact]
    public void ACellPointingNowhereReadsAsNothing()
    {
        Assert.Null(TableReference.Decode(TableReference.Empty));
        Assert.Null(TableReference.Decode(""));
        Assert.Null(TableReference.Decode("not a reference"));
    }

    [Fact]
    public void ARowOutsideSixteenBitsIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TableReference.Encode(8496, 70000));
    }

    // ---- The measured slot model --------------------------------------------

    [Fact]
    public void TheSpecialistSlotsAreNotPositions()
    {
        // These cannot be inferred from a position list — they were measured
        // off the game's own charts, and getting them wrong puts a lineman at
        // gunner.
        var model = Model;
        Assert.Equal(new[] { "HB", "WR" }, model.ByName["GAD"].From);
        Assert.Equal("TE", model.ByName["LS"].From[0]);
        Assert.Contains("SS", model.ByName["SLCB"].From);
        Assert.Equal(new[] { "DT" }, model.ByName["NT"].From);
    }

    [Fact]
    public void DepthIsNotUniform()
    {
        var model = Model;
        Assert.Equal(6, model.ByName["WR"].Depth);
        Assert.Equal(5, model.ByName["CB"].Depth);
        Assert.Equal(4, model.ByName["HB"].Depth);
        Assert.Equal(3, model.ByName["QB"].Depth);
    }

    [Fact]
    public void TheMirroredPairsKnowTheirPartners()
    {
        var model = Model;
        Assert.Equal("RT", model.PartnerOf("LT"));
        Assert.Equal("LT", model.PartnerOf("RT"));
        Assert.True(model.IsLeftOfPair("LT"));
        Assert.False(model.IsLeftOfPair("RT"));
        Assert.Null(model.PartnerOf("QB"));
    }

    // ---- Rebuilding ---------------------------------------------------------

    /// <summary>A dynasty with one team, one chart and a squad to sort.</summary>
    private static string BuildExport(TempDirectory folder, params (string Position, int Overall)[] squad)
    {
        var directory = folder.File("export");
        Directory.CreateDirectory(directory);
        var tag = Model.PlayerTableTag;

        var slots = new[] { "QB", "HB", "WR", "LT", "RT", "K" };
        File.WriteAllText(Path.Combine(directory, "0001_Team.csv"),
            "_row,TeamIndex,DepthChart\n0,7," + TableReference.Encode(99, 0) + "\n");

        File.WriteAllText(Path.Combine(directory, "0002_DepthChart.csv"),
            "_row,LockedEntries," + string.Join(",", slots) + "\n" +
            "0," + TableReference.Empty + "," +
            string.Join(",", slots.Select((_, i) => TableReference.Encode(98, i))) + "\n");

        var entries = new List<string> { "_row,Player0,Player1,Player2,Player3,Player4,Player5" };
        for (var row = 0; row < slots.Length; row++)
        {
            entries.Add(row + "," + string.Join(",", Enumerable.Repeat(TableReference.Empty, 6)));
        }

        File.WriteAllText(Path.Combine(directory, "0003_Player[].csv"),
            string.Join("\n", entries) + "\n");

        var columns = PlayerSchema.RequiredColumns
            .Append(PlayerColumns.OverallRating)
            .ToList();
        var rows = new List<string> { string.Join(",", columns) };
        for (var i = 0; i < squad.Length; i++)
        {
            var (position, overall) = squad[i];
            rows.Add(string.Join(",", columns.Select(c => c switch
            {
                PlayerColumns.OverallRating => overall.ToString(),
                PlayerColumns.Row => i.ToString(),
                PlayerColumns.IsEmpty => "false",
                PlayerColumns.FirstName => "P" + i,
                PlayerColumns.LastName => position + overall,
                PlayerColumns.Position => position,
                PlayerColumns.TeamIndex => "7",
                PlayerColumns.SchoolYear => "Senior",
                PlayerColumns.RedshirtStatus => "Eligible",
                _ => "0",
            })));
        }

        var playerPath = Path.Combine(directory, "0004_Player.csv");
        File.WriteAllText(playerPath, string.Join("\n", rows) + "\n");

        return directory;
    }

    private static List<string> Listed(string exportDirectory, string slot, int slotIndex)
    {
        var entries = CsvDocument.Load(Path.Combine(exportDirectory, "0003_Player[].csv"));
        var players = CsvDocument.Load(Path.Combine(exportDirectory, "0004_Player.csv"));
        var listed = new List<string>();
        foreach (var column in new[] { "Player0", "Player1", "Player2", "Player3", "Player4", "Player5" })
        {
            if (TableReference.Decode(entries.GetCell(slotIndex, column)) is not { } reference)
            {
                continue;
            }

            listed.Add(players.GetCell(reference.Row, PlayerColumns.LastName));
        }

        return listed;
    }

    [Fact]
    public void AnExportWithNoDepthChartIsNotAProblem()
    {
        using var folder = new TempDirectory();
        var directory = folder.File("bare");
        Directory.CreateDirectory(directory);

        // The community export tool writes whichever tables its user asked for,
        // and a dynasty without a depth chart is still worth generating into.
        Assert.Null(DepthChartTable.Open(directory));
    }

    [Fact]
    public void TheChartIsFoundThroughTheTeamsOwnLink()
    {
        using var folder = new TempDirectory();
        var directory = BuildExport(folder, ("QB", 70));

        var table = DepthChartTable.Open(directory);

        // Team row order is not team index — Florida State is row 38 and team
        // 27 — so the link has to be followed rather than assumed.
        Assert.NotNull(table);
        Assert.Contains(7, table!.Teams);
    }

    [Fact]
    public void TheBestPlayerAtAPositionStarts()
    {
        using var folder = new TempDirectory();
        var directory = BuildExport(folder,
            ("QB", 62), ("QB", 91), ("QB", 74),
            ("HB", 80), ("HB", 88));

        var table = DepthChartTable.Open(directory)!;
        var report = table.Rebuild(7, PlayerRoster.Load(Path.Combine(directory, "0004_Player.csv")), Model);
        table.Save(Path.Combine(directory, "0003_Player[].csv"));

        Assert.NotNull(report);
        Assert.Equal(new[] { "QB91", "QB74", "QB62" }, Listed(directory, "QB", 0));
        Assert.Equal(new[] { "HB88", "HB80" }, Listed(directory, "HB", 1));
    }

    [Fact]
    public void AMirroredPairIsDealtBetweenTheSidesWithTheBestOnTheLeft()
    {
        using var folder = new TempDirectory();
        var directory = BuildExport(folder,
            ("LT", 84), ("LT", 73), ("RT", 70), ("RT", 65));

        var table = DepthChartTable.Open(directory)!;
        table.Rebuild(7, PlayerRoster.Load(Path.Combine(directory, "0004_Player.csv")), Model);
        table.Save(Path.Combine(directory, "0003_Player[].csv"));

        // Measured on 143 teams: the same player never heads both slots, and
        // the better of the two is on the left 87% of the time.
        var left = Listed(directory, "LT", 3);
        var right = Listed(directory, "RT", 4);
        Assert.Equal("LT84", left[0]);
        Assert.Equal("LT73", right[0]);
        Assert.Empty(left.Intersect(right));
    }

    [Fact]
    public void ASlotDrawsOnEveryPositionTheGameDrawsOn()
    {
        using var folder = new TempDirectory();
        var directory = BuildExport(folder, ("K", 71), ("P", 88));

        var table = DepthChartTable.Open(directory)!;
        table.Rebuild(7, PlayerRoster.Load(Path.Combine(directory, "0004_Player.csv")), Model);
        table.Save(Path.Combine(directory, "0003_Player[].csv"));

        // The kicking slot takes kickers and punters — 68% and 31% in the
        // game's own charts — so the better leg kicks off whichever he is.
        Assert.Equal("P88", Listed(directory, "K", 5)[0]);
    }

    [Fact]
    public void ATeamTheRosterDoesNotCoverIsLeftAlone()
    {
        using var folder = new TempDirectory();
        var directory = BuildExport(folder, ("QB", 70));

        var table = DepthChartTable.Open(directory)!;

        Assert.Null(table.Rebuild(99, PlayerRoster.Load(Path.Combine(directory, "0004_Player.csv")), Model));
    }
}
