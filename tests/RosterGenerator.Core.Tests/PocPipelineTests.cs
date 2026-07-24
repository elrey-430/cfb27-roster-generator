using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;
using RosterGenerator.Core.Validation;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The Milestone 1 proof-of-concept pipeline: load → rename one player and
/// change their jersey number → validate → export, then prove the exported
/// file differs from the input in exactly those three cells and nothing
/// else — matching the observed behavior of a real in-game rename.
/// </summary>
public sealed class PocPipelineTests
{
    [Fact]
    public void RenameAndJerseyEditTouchesOnlyThoseThreeCells()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var target = roster.Players.First();
        var targetRowKey = target.RowKey;

        var session = new RosterEditSession(roster);
        session.RenamePlayer(target, "Charlie", "Ward");
        session.SetJerseyNumber(target, 17);

        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            var result = new RosterExporter().Export(new RosterValidationContext(roster, session), outputPath);

            // The exporter's own change accounting: one row, three columns.
            var (rowKey, changedColumns) = Assert.Single(result.ChangedColumnsByRowKey);
            Assert.Equal(targetRowKey, rowKey);
            Assert.Equal(
                new[] { PlayerColumns.LastName, PlayerColumns.FirstName, PlayerColumns.JerseyNum }.Order(),
                changedColumns.Order());

            // Independent file-level proof: re-read both files and compare
            // every cell of every row.
            var input = PlayerRoster.Load(TestFixtures.PlayerSamplePath);
            var output = PlayerRoster.Load(outputPath);
            for (var row = 0; row < input.AllRows.Count; row++)
            {
                foreach (var column in input.Document.Header)
                {
                    var before = input.Document.GetCell(row, column);
                    var after = output.Document.GetCell(row, column);
                    var isEditedCell = output.AllRows[row].GetRaw(PlayerColumns.Row) == targetRowKey.ToString() &&
                                       column is PlayerColumns.FirstName or PlayerColumns.LastName or PlayerColumns.JerseyNum;
                    if (isEditedCell)
                    {
                        Assert.NotEqual(before, after);
                    }
                    else
                    {
                        Assert.Equal(before, after);
                    }
                }
            }

            Assert.Equal("Charlie", output.FindByRowKey(targetRowKey)!.FirstName);
            Assert.Equal("Ward", output.FindByRowKey(targetRowKey)!.LastName);
            Assert.Equal(17, output.FindByRowKey(targetRowKey)!.JerseyNumber);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void ExportWithNoEditsIsByteIdenticalToInput()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            var result = new RosterExporter().Export(new RosterValidationContext(roster), outputPath);

            Assert.Empty(result.ChangedColumnsByRowKey);
            Assert.Equal(TestFixtures.PlayerSampleText, File.ReadAllText(outputPath));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void ExportIsBlockedAndWritesNothingWhenValidationFails()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var target = roster.Players.First();
        target.SetRaw(PlayerColumns.Position, "NOT_A_POSITION");

        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");

        var ex = Assert.Throws<RosterExportException>(
            () => new RosterExporter().Export(new RosterValidationContext(roster), outputPath));

        Assert.Contains("NOT_A_POSITION", ex.Message);
        Assert.False(File.Exists(outputPath));
    }
}
