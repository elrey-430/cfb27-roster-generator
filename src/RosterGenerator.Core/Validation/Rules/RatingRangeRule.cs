using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// All 57 numeric rating columns must hold integers within 0–99 for real
/// player rows (the full 0–99 span is observed in real exports).
/// </summary>
public sealed class RatingRangeRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "RatingRange";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.State;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        foreach (var player in context.Roster.Players)
        {
            foreach (var column in PlayerSchema.NumericRatingColumns)
            {
                var raw = player.GetRaw(column);
                if (!int.TryParse(raw, out var value))
                {
                    yield return Issue(player.RowKey, column, $"Value '{raw}' is not an integer.");
                }
                else if (value is < PlayerSchema.RatingMin or > PlayerSchema.RatingMax)
                {
                    yield return Issue(player.RowKey, column,
                        $"Rating {value} is outside the valid range {PlayerSchema.RatingMin}–{PlayerSchema.RatingMax}.");
                }
            }
        }
    }

    private ValidationIssue Issue(int rowKey, string column, string message) =>
        new(Name, ValidationSeverity.Error, rowKey, column, message);
}
