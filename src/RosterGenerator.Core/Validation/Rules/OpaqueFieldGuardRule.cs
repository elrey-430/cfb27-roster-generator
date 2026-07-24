using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// Guards the fields whose encoding is unresolved or that the game manages
/// internally: <c>Weight</c> (NOT pounds — believed to index a weight
/// curve/spline; open research item) and <c>PLYR_COMMENT</c> (internal
/// comment-pool index that changed spontaneously in one observed edit).
/// Any change to them versus the loaded file is an error until their
/// encodings are reverse-engineered in a later milestone.
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
            var originalWeight = context.Roster.GetOriginalValue(player.RowIndex, PlayerColumns.Weight);
            var currentWeight = player.GetRaw(PlayerColumns.Weight);
            if (!string.Equals(originalWeight, currentWeight, StringComparison.Ordinal))
            {
                yield return new ValidationIssue(Name, ValidationSeverity.Error, player.RowKey, PlayerColumns.Weight,
                    $"Weight changed from '{originalWeight}' to '{currentWeight}', but the Weight encoding is " +
                    "unresolved (values are NOT pounds; they appear to index a weight curve/spline). Writing " +
                    "real-world weights would corrupt the save. Leave Weight untouched until the encoding is known.");
            }

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
