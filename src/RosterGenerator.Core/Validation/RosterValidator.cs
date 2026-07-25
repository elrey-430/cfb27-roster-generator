using RosterGenerator.Core.Schema;
using RosterGenerator.Core.Validation.Rules;

namespace RosterGenerator.Core.Validation;

/// <summary>
/// Runs a set of <see cref="IValidationRule"/>s over a roster and aggregates
/// their findings. The default rule set covers everything Milestone 1
/// requires; callers can pass a custom set to add or remove rules.
///
/// Findings from <see cref="ValidationRuleKind.State"/> rules whose offending
/// cells are untouched since load are downgraded to warnings: real EA exports
/// contain such anomalies (e.g. blank-named placeholder rows on team 255 in
/// the observed base save), and a file the game itself wrote must always
/// remain exportable. The same anomaly introduced by an edit stays an error.
/// </summary>
public sealed class RosterValidator
{
    private readonly IReadOnlyList<IValidationRule> _rules;

    /// <summary>Creates a validator with the default rule set.</summary>
    public RosterValidator()
        : this(DefaultRules())
    {
    }

    /// <summary>Creates a validator with a custom rule set.</summary>
    public RosterValidator(IReadOnlyList<IValidationRule> rules)
    {
        _rules = rules;
    }

    /// <summary>The default Milestone 1 rule set.</summary>
    public static IReadOnlyList<IValidationRule> DefaultRules() => new IValidationRule[]
    {
        new RequiredFieldsRule(),
        new DuplicateRowKeyRule(),
        new RatingRangeRule(),
        new EnumFieldsRule(),
        new TeamAssignmentRule(),
        new TeamChangeConsistencyRule(),
        new IdentityChangeConsistencyRule(),
        new OpaqueFieldGuardRule(),
        new WeightRangeRule(),
        new ArchetypeConsistencyRule(),
    };

    /// <summary>Runs every rule and returns the combined report.</summary>
    public ValidationReport Validate(RosterValidationContext context)
    {
        var issues = new List<ValidationIssue>();
        foreach (var rule in _rules)
        {
            foreach (var issue in rule.Validate(context))
            {
                var downgrade = rule.Kind == ValidationRuleKind.State &&
                                issue.Severity == ValidationSeverity.Error &&
                                IsPreExisting(context, issue);
                issues.Add(downgrade
                    ? issue with
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = issue.Message + " (Pre-existing in the source file, not introduced by this edit session.)",
                    }
                    : issue);
            }
        }

        return new ValidationReport(issues);
    }

    /// <summary>
    /// True when the issue's offending cell(s) hold the same value they had
    /// when the file was loaded, i.e. the anomaly came with the source file.
    /// </summary>
    private static bool IsPreExisting(RosterValidationContext context, ValidationIssue issue)
    {
        if (issue.RowKey is null || issue.Column is null)
        {
            return false;
        }

        var roster = context.Roster;
        var keyText = issue.RowKey.Value.ToString();
        var found = false;
        foreach (var player in roster.AllRows)
        {
            if (!string.Equals(player.GetRaw(PlayerColumns.Row), keyText, StringComparison.Ordinal))
            {
                continue;
            }

            found = true;
            var original = roster.GetOriginalValue(player.RowIndex, issue.Column);
            if (!string.Equals(original, player.GetRaw(issue.Column), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return found;
    }
}
