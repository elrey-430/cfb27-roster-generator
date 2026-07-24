using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// The <c>_row</c> key is the table's primary key and must be unique across
/// all rows (including empty pool slots). Duplicates indicate a corrupted or
/// hand-mangled export.
/// </summary>
public sealed class DuplicateRowKeyRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "DuplicateRowKey";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.State;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var player in context.Roster.AllRows)
        {
            var key = player.GetRaw(PlayerColumns.Row);
            if (seen.TryGetValue(key, out var firstRowIndex))
            {
                yield return new ValidationIssue(
                    Name, ValidationSeverity.Error,
                    int.TryParse(key, out var k) ? k : null, PlayerColumns.Row,
                    $"_row value '{key}' appears more than once (first at file row {firstRowIndex}, again at file row {player.RowIndex}).");
            }
            else
            {
                seen.Add(key, player.RowIndex);
            }
        }
    }
}
