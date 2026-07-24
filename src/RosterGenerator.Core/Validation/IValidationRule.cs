namespace RosterGenerator.Core.Validation;

/// <summary>
/// How a rule's findings relate to the loaded file, which decides whether a
/// finding can be downgraded when the offending value was already present in
/// the source. Real EA exports contain anomalies of their own (e.g. two live
/// rows with empty names in the observed base save), and a file the game
/// itself produced must always remain exportable — so pre-existing problems
/// become warnings, while the same problem introduced by an edit is an error.
/// </summary>
public enum ValidationRuleKind
{
    /// <summary>
    /// Checks current cell values. Findings whose cells are unchanged from
    /// the loaded file are downgraded to warnings by the validator.
    /// </summary>
    State,

    /// <summary>
    /// Fires only when something changed versus the loaded file (e.g. the
    /// team-change and identity-change consistency rules). Never downgraded:
    /// the flagged cell being unchanged is often exactly the problem.
    /// </summary>
    ChangeDriven,
}

/// <summary>
/// A single named validation rule. Rules are independent and side-effect
/// free; the <see cref="RosterValidator"/> runs them all and aggregates
/// their issues into one report.
/// </summary>
public interface IValidationRule
{
    /// <summary>Stable rule name shown in every issue it produces.</summary>
    string Name { get; }

    /// <summary>Whether findings target current state or detected changes.</summary>
    ValidationRuleKind Kind { get; }

    /// <summary>Examines the roster and yields any issues found.</summary>
    IEnumerable<ValidationIssue> Validate(RosterValidationContext context);
}
