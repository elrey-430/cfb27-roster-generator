using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// Guards fields the game manages internally. Currently that is
/// <c>PLYR_COMMENT</c>, an internal flavor-text/comment-pool index that
/// changed spontaneously in one observed edit — any change to it versus the
/// loaded file is an error. (<c>Weight</c> was guarded here until its
/// encoding — stored pounds − 160 — was confirmed; it is now validated by
/// <see cref="WeightRangeRule"/> instead.)
/// </summary>
public sealed class OpaqueFieldGuardRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "OpaqueFieldGuard";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.ChangeDriven;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        foreach (var player in context.Roster.Players)
        {
            var originalComment = context.Roster.GetOriginalValue(player.RowIndex, PlayerColumns.Comment);
            var currentComment = player.GetRaw(PlayerColumns.Comment);
            if (!string.Equals(originalComment, currentComment, StringComparison.Ordinal))
            {
                yield return new ValidationIssue(Name, ValidationSeverity.Error, player.RowKey, PlayerColumns.Comment,
                    "PLYR_COMMENT was modified. This is an internal flavor-text/comment-pool index the game manages " +
                    "itself — leave it alone.");
            }
        }
    }
}
