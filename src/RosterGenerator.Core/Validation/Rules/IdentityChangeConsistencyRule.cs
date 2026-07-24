using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// "Rename" and "replace with a different real player" are both legitimate
/// operations with different correct outcomes for the identity-derived asset
/// fields (<c>PLYR_ASSETNAME</c>, <c>GenericHeadAssetName</c>,
/// <c>PLYR_PORTRAIT</c>): a rename must leave them untouched (confirmed
/// in-game behavior), while a replace must update them or the portrait and
/// head model will belong to the old identity. Because intent cannot be
/// inferred from the data, this rule requires it to have been declared via
/// <see cref="RosterEditSession"/> and checks the actual changes against it.
/// </summary>
public sealed class IdentityChangeConsistencyRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "IdentityChangeConsistency";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.ChangeDriven;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        foreach (var player in context.Roster.Players)
        {
            var nameChanged =
                Changed(context, player.RowIndex, PlayerColumns.FirstName) ||
                Changed(context, player.RowIndex, PlayerColumns.LastName);
            var changedAssets = PlayerSchema.IdentityAssetColumns
                .Where(c => Changed(context, player.RowIndex, c))
                .ToList();

            if (!nameChanged && changedAssets.Count == 0)
            {
                continue;
            }

            var isRename = context.EditSession?.HasIntent(player.RowKey, EditIntent.Rename) ?? false;
            var isReplace = context.EditSession?.HasIntent(player.RowKey, EditIntent.ReplaceIdentity) ?? false;

            if (!isRename && !isReplace)
            {
                var what = nameChanged ? "name" : "identity asset fields";
                yield return Error(player.RowKey, null,
                    $"The player's {what} changed without a declared intent. Use RenamePlayer (cosmetic rename, " +
                    "assets untouched) or ReplacePlayerIdentity (different real player, assets updated) so the " +
                    "identity-derived fields are handled correctly.");
                continue;
            }

            if (isRename && changedAssets.Count > 0)
            {
                yield return Error(player.RowKey, string.Join(", ", changedAssets),
                    "A cosmetic rename must not modify identity asset fields " +
                    $"({string.Join(", ", changedAssets)} changed). The in-game rename behavior leaves them alone; " +
                    "if the goal is to substitute a different real player, use ReplacePlayerIdentity instead.");
            }

            if (isReplace)
            {
                var untouched = PlayerSchema.IdentityAssetColumns.Except(changedAssets).ToList();
                if (untouched.Count > 0)
                {
                    yield return new ValidationIssue(Name, ValidationSeverity.Warning, player.RowKey,
                        string.Join(", ", untouched),
                        "Replace-identity edit left these identity asset fields with their old values: " +
                        $"{string.Join(", ", untouched)}. The portrait/head model may mismatch the new identity.");
                }
            }
        }
    }

    private static bool Changed(RosterValidationContext context, int rowIndex, string column) =>
        !string.Equals(
            context.Roster.GetOriginalValue(rowIndex, column),
            context.Roster.Document.GetCell(rowIndex, column),
            StringComparison.Ordinal);

    private ValidationIssue Error(int rowKey, string? column, string message) =>
        new(Name, ValidationSeverity.Error, rowKey, column, message);
}
