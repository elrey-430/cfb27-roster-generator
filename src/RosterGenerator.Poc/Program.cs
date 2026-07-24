using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Validation;

// Milestone 1 proof of concept: load a CFB27 Player.csv, apply a controlled
// "plain rename + jersey number" edit to one player, validate, export, and
// prove the output differs from the input only in the three edited cells.
//
// Usage:
//   RosterGenerator.Poc <input Player.csv> <output Player.csv> [_row key]
//
// When no _row key is given, the first real player row is edited.

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: RosterGenerator.Poc <input Player.csv> <output Player.csv> [_row key]");
    return 1;
}

var inputPath = args[0];
var outputPath = args[1];

Console.WriteLine($"Loading roster: {inputPath}");
var roster = PlayerRoster.Load(inputPath);
Console.WriteLine($"  {roster.AllRows.Count} rows ({roster.Players.Count()} players, " +
                  $"{roster.AllRows.Count - roster.Players.Count()} empty slots), " +
                  $"{roster.Document.Header.Count} columns.");

var target = args.Length == 3
    ? roster.FindByRowKey(int.Parse(args[2]))
    : roster.Players.First();
if (target is null || target.IsEmpty)
{
    Console.Error.WriteLine($"No player found at _row={args[2]}.");
    return 1;
}

Console.WriteLine($"Target player: {target} — #{target.JerseyNumber} {target.Position}, " +
                  $"team {target.TeamIndex}, OVR {target.OverallRating}");

// The controlled edit: rename + new jersey number. RenamePlayer records the
// cosmetic-rename intent so validation knows the identity assets must stay.
var session = new RosterEditSession(roster);
session.RenamePlayer(target, "Charlie", "Ward");
session.SetJerseyNumber(target, 17);

Console.WriteLine("Applied edits:");
foreach (var edit in session.Edits)
{
    Console.WriteLine($"  - {edit.Description}");
}

// Validate and export. Export throws (and writes nothing) on errors.
var context = new RosterValidationContext(roster, session);
ExportResult result;
try
{
    result = new RosterExporter().Export(context, outputPath);
}
catch (RosterExportException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

Console.WriteLine($"Validation: {result.Report.Errors.Count()} errors, {result.Report.Warnings.Count()} warnings.");
foreach (var warning in result.Report.Warnings)
{
    Console.WriteLine($"  {warning}");
}

Console.WriteLine($"Exported: {outputPath}");
Console.WriteLine("Changed cells (proof the edit touched nothing else):");
foreach (var (rowKey, columns) in result.ChangedColumnsByRowKey)
{
    Console.WriteLine($"  _row={rowKey}: {string.Join(", ", columns)}");
}

// Independent verification: re-read both files and diff every cell.
var verifyInput = PlayerRoster.Load(inputPath);
var verifyOutput = PlayerRoster.Load(outputPath);
var diffRows = 0;
for (var i = 0; i < verifyInput.AllRows.Count; i++)
{
    var changed = new List<string>();
    foreach (var column in verifyInput.Document.Header)
    {
        if (verifyInput.Document.GetCell(i, column) != verifyOutput.Document.GetCell(i, column))
        {
            changed.Add(column);
        }
    }

    if (changed.Count > 0)
    {
        diffRows++;
        Console.WriteLine($"  verified file diff row {i}: {string.Join(", ", changed)}");
    }
}

Console.WriteLine($"File-level verification: {diffRows} row(s) differ between input and output.");
return 0;
