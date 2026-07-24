namespace RosterGenerator.Core.Editing;

/// <summary>
/// One recorded edit operation: which row it targeted, the declared intent,
/// and a human-readable description for logs and reports.
/// </summary>
/// <param name="RowKey">The player's stable <c>_row</c> key.</param>
/// <param name="Intent">The declared purpose of the edit.</param>
/// <param name="Description">Human-readable summary of what was changed.</param>
public sealed record EditRecord(int RowKey, EditIntent Intent, string Description);
