using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A generated player carries no NIL deal.
///
/// <para>Requested: every generated player should default to <c>IsNIL</c>
/// false. Left alone the flag is inherited from whoever held the roster slot,
/// and it is not a harmless leftover — measured across the 16,257 players of a
/// base save it tracks standing, not money:</para>
///
/// <code>
///   OVR 40-49    1.7% carry a deal
///   OVR 60-69   42.4%
///   OVR 70-79   78.0%
///   OVR 90-99  100.0%   (all 114 of them)
/// </code>
///
/// <para>So the inheritance lands hardest exactly where it is most visible: a
/// recreated 1985 starting eleven, built on the modern save's best slots,
/// arrives with NIL deals signed some forty years before college athletes
/// could sign one.</para>
///
/// <para>The two NIL money fields are deliberately <em>not</em> moved with the
/// flag — 3,473 of the 7,246 players a base save marks false still hold a
/// non-zero <c>BaseNILValue</c>, so tying them together would be inventing a
/// rule the game does not follow.</para>
/// </summary>
public sealed class NilStatusTests
{
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

    /// <summary>
    /// The donor fixture holds false throughout, so a test that only asserted
    /// the output is false would pass without the tool doing anything. Every
    /// slot is flipped to true first; anything still true afterwards was
    /// inherited.
    /// </summary>
    private static string DonorWithNilOnEveryPlayer(TempDirectory folder)
    {
        var donor = folder.File("Donor");
        Directory.CreateDirectory(donor);
        foreach (var source in Directory.GetFiles(TestsPath("DonorDynasty")))
        {
            var destination = Path.Combine(donor, Path.GetFileName(source));
            File.Copy(source, destination);
            if (!Path.GetFileName(source).Contains("Player", StringComparison.Ordinal))
            {
                continue;
            }

            var table = CsvDocument.Load(destination);
            for (var row = 0; row < table.RowCount; row++)
            {
                table.SetCell(row, PlayerColumns.IsNil, "true");
            }

            table.Save(destination);
        }

        return donor;
    }

    private static RosterGenerationResult Generate(
        TempDirectory folder, string donor, bool fill = true) =>
        new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = donor,
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = DataDirectory,
            Team = "Florida State",
            OutputPath = folder.File("roster.csv"),
            ReportPath = folder.File("report.txt"),
            Ratings = RatingsMode.Generate,
            FillRoster = fill,
        });

    // ---- The ask ------------------------------------------------------------

    [Fact]
    public void ARecreatedPlayerDoesNotInheritTheSlotsNilDeal()
    {
        using var folder = new TempDirectory();
        var donor = DonorWithNilOnEveryPlayer(folder);

        var result = Generate(folder, donor);

        var table = CsvDocument.Load(result.OutputPath);
        var stillNil = new List<string>();
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.IsEmpty) == "true")
            {
                continue;
            }

            if (table.GetCell(row, PlayerColumns.IsNil) != "false")
            {
                stillNil.Add(
                    $"{table.GetCell(row, PlayerColumns.FirstName)} " +
                    $"{table.GetCell(row, PlayerColumns.LastName)}");
            }
        }

        Assert.True(stillNil.Count == 0,
            $"{stillNil.Count} generated player(s) kept the slot's NIL deal: " +
            string.Join(", ", stillNil.Take(5)));
    }

    [Fact]
    public void AFilledDepthSlotIsClearedToo()
    {
        using var folder = new TempDirectory();
        var donor = DonorWithNilOnEveryPlayer(folder);

        var result = Generate(folder, donor);

        // The fill is the case that is easy to miss: those slots keep their
        // donor's name, so nothing about the output looks like a new player.
        Assert.NotEmpty(result.Teams.SelectMany(t => t.FilledSlots));
        var filled = result.Teams.SelectMany(t => t.FilledSlots)
            .Select(s => s.RowKey).ToHashSet();

        var table = CsvDocument.Load(result.OutputPath);
        for (var row = 0; row < table.RowCount; row++)
        {
            if (int.TryParse(table.GetCell(row, PlayerColumns.Row), out var key) && filled.Contains(key))
            {
                Assert.Equal("false", table.GetCell(row, PlayerColumns.IsNil));
            }
        }
    }

    [Fact]
    public void TheReportSaysSo()
    {
        using var folder = new TempDirectory();
        var donor = DonorWithNilOnEveryPlayer(folder);

        Generate(folder, donor);

        var report = File.ReadAllText(folder.File("report.txt"));
        Assert.Contains("IsNIL", report, StringComparison.Ordinal);
    }

    // ---- What is deliberately not done --------------------------------------

    [Fact]
    public void TheNilMoneyFieldsAreLeftAlone()
    {
        using var folder = new TempDirectory();
        var donor = DonorWithNilOnEveryPlayer(folder);
        var before = CsvDocument.Load(Directory.GetFiles(donor, "*Player*.csv").Single());

        var result = Generate(folder, donor);
        var after = CsvDocument.Load(result.OutputPath);

        // The donor fixture carries real NIL money on 28 of its 85 players. It
        // stays: the game's own data has 3,473 non-NIL players holding a
        // non-zero BaseNILValue, so the two fields do not move together and
        // zeroing them here would be this tool inventing a rule.
        var carried = 0;
        for (var row = 0; row < before.RowCount; row++)
        {
            var money = before.GetCell(row, PlayerColumns.BaseNilValue);
            Assert.Equal(money, after.GetCell(row, PlayerColumns.BaseNilValue));
            Assert.Equal(
                before.GetCell(row, PlayerColumns.CurrentNilCompensation),
                after.GetCell(row, PlayerColumns.CurrentNilCompensation));
            if (money is not ("0" or ""))
            {
                carried++;
            }
        }

        Assert.True(carried > 0, "the fixture no longer proves anything; nobody in it has NIL money.");
    }

    [Fact]
    public void ASlotNobodyWasGeneratedIntoIsNotTouched()
    {
        using var folder = new TempDirectory();
        var donor = DonorWithNilOnEveryPlayer(folder);

        // With filling off, the leftover slots keep their original fictional
        // players outright — the tool has not generated them, so it has no
        // business editing them either.
        var result = Generate(folder, donor, fill: false);

        var converted = result.Teams
            .SelectMany(t => t.Converted)
            .Select(e => e.AssignedRowKey!.Value)
            .ToHashSet();
        var table = CsvDocument.Load(result.OutputPath);

        var untouched = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.GetCell(row, PlayerColumns.IsEmpty) == "true" ||
                !int.TryParse(table.GetCell(row, PlayerColumns.Row), out var key) ||
                converted.Contains(key))
            {
                continue;
            }

            Assert.Equal("true", table.GetCell(row, PlayerColumns.IsNil));
            untouched++;
        }

        Assert.True(untouched > 0, "the fixture left no unconverted slot, so this proves nothing.");
    }

    // ---- The edit layer -----------------------------------------------------

    [Fact]
    public void TheEditIsRecordedLikeAnyOther()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        var player = roster.Players.First();
        player.SetRaw(PlayerColumns.IsNil, "true");

        session.SetNilStatus(player, false);

        Assert.Equal("false", player.GetRaw(PlayerColumns.IsNil));
        Assert.Contains(session.Edits, e => e.Description.Contains("NIL", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExportWithoutTheColumnIsStillUsable()
    {
        using var folder = new TempDirectory();
        var path = folder.File("player.csv");

        // An export that predates the column is still an export. Writing is
        // skipped rather than throwing, which is how every optional field in
        // the edit layer behaves.
        var header = string.Join(",", PlayerSchema.RequiredColumns);
        var row = string.Join(",", PlayerSchema.RequiredColumns.Select(c => c switch
        {
            PlayerColumns.Row => "0",
            PlayerColumns.IsEmpty => "false",
            PlayerColumns.FirstName => "Aa",
            PlayerColumns.LastName => "Alpha",
            PlayerColumns.Position => "QB",
            PlayerColumns.SchoolYear => "Junior",
            PlayerColumns.RedshirtStatus => "Eligible",
            _ => "0",
        }));
        File.WriteAllText(path, header + Environment.NewLine + row + Environment.NewLine);

        var roster = PlayerRoster.Load(path);
        var player = roster.Players.First();
        Assert.False(player.HasColumn(PlayerColumns.IsNil));

        var session = new RosterEditSession(roster);
        session.SetNilStatus(player, false);

        Assert.Empty(session.Edits);
    }
}
