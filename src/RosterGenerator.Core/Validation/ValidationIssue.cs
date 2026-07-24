namespace RosterGenerator.Core.Validation;

/// <summary>Severity of a validation issue.</summary>
public enum ValidationSeverity
{
    /// <summary>Suspicious but not known to break the save; export proceeds.</summary>
    Warning,

    /// <summary>Known to be invalid; export is blocked by default.</summary>
    Error,
}

/// <summary>
/// One problem found by a validation rule, with enough context (rule name,
/// row key, column) for a user to locate and fix it.
/// </summary>
/// <param name="RuleName">Name of the rule that produced the issue.</param>
/// <param name="Severity">Whether the issue blocks export.</param>
/// <param name="RowKey">The affected player's <c>_row</c> key, or null for file-level issues.</param>
/// <param name="Column">The affected column, or null when the issue spans several.</param>
/// <param name="Message">Human-readable explanation of what is wrong and how to fix it.</param>
public sealed record ValidationIssue(
    string RuleName,
    ValidationSeverity Severity,
    int? RowKey,
    string? Column,
    string Message)
{
    /// <summary>"[Error] RuleName (_row=N, Column): message" for logs.</summary>
    public override string ToString()
    {
        var location = RowKey is null ? "" : $" (_row={RowKey}{(Column is null ? "" : $", {Column}")})";
        return $"[{Severity}] {RuleName}{location}: {Message}";
    }
}
