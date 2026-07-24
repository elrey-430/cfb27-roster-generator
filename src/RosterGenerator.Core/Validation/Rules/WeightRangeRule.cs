using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// The stored <c>Weight</c> value must be an integer within the confirmed
/// encoding's range: stored = pounds − 160, with 0–240 stored (160–400 lb)
/// observed as the format's bounds across the full base save.
/// </summary>
public sealed class WeightRangeRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "WeightRange";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.State;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        var storedMin = PlayerSchema.WeightPoundsMin - PlayerSchema.WeightOffsetPounds;
        var storedMax = PlayerSchema.WeightPoundsMax - PlayerSchema.WeightOffsetPounds;
        foreach (var player in context.Roster.Players)
        {
            var raw = player.GetRaw(PlayerColumns.Weight);
            if (!int.TryParse(raw, out var stored))
            {
                yield return Issue(player.RowKey, $"Weight '{raw}' is not an integer.");
            }
            else if (stored < storedMin || stored > storedMax)
            {
                yield return Issue(player.RowKey,
                    $"Stored weight {stored} (= {stored + PlayerSchema.WeightOffsetPounds} lb) is outside the " +
                    $"valid stored range {storedMin}–{storedMax} ({PlayerSchema.WeightPoundsMin}–" +
                    $"{PlayerSchema.WeightPoundsMax} lb).");
            }
        }
    }

    private ValidationIssue Issue(int rowKey, string message) =>
        new(Name, ValidationSeverity.Error, rowKey, PlayerColumns.Weight, message);
}
