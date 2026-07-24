namespace RosterGenerator.Core.Validation;

/// <summary>The combined result of running every validation rule.</summary>
public sealed class ValidationReport
{
    /// <summary>Creates a report from the issues found.</summary>
    public ValidationReport(IReadOnlyList<ValidationIssue> issues)
    {
        Issues = issues;
    }

    /// <summary>All issues found, in rule order.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>Issues with <see cref="ValidationSeverity.Error"/> severity.</summary>
    public IEnumerable<ValidationIssue> Errors =>
        Issues.Where(i => i.Severity == ValidationSeverity.Error);

    /// <summary>Issues with <see cref="ValidationSeverity.Warning"/> severity.</summary>
    public IEnumerable<ValidationIssue> Warnings =>
        Issues.Where(i => i.Severity == ValidationSeverity.Warning);

    /// <summary>True when no error-severity issues were found.</summary>
    public bool IsValid => !Errors.Any();
}
