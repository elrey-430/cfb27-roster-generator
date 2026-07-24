using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// Every real (non-empty) player row must have the fields the game cannot do
/// without: a name, and parseable integers in the key numeric columns.
/// </summary>
public sealed class RequiredFieldsRule : IValidationRule
{
    private static readonly string[] RequiredIntegerColumns =
    {
        PlayerColumns.Row, PlayerColumns.JerseyNum, PlayerColumns.Height,
        PlayerColumns.TeamIndex, PlayerColumns.PrevTeamIndex, PlayerColumns.PrevTeamId,
        PlayerColumns.BaseNilValue, PlayerColumns.CurrentNilCompensation,
    };

    /// <inheritdoc />
    public string Name => "RequiredFields";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.State;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        foreach (var player in context.Roster.Players)
        {
            int? rowKey = int.TryParse(player.GetRaw(PlayerColumns.Row), out var key) ? key : null;

            if (string.IsNullOrWhiteSpace(player.FirstName))
            {
                yield return Issue(rowKey, PlayerColumns.FirstName, "First name is empty.");
            }

            if (string.IsNullOrWhiteSpace(player.LastName))
            {
                yield return Issue(rowKey, PlayerColumns.LastName, "Last name is empty.");
            }

            foreach (var column in RequiredIntegerColumns)
            {
                var raw = player.GetRaw(column);
                if (!int.TryParse(raw, out _))
                {
                    yield return Issue(rowKey, column, $"Value '{raw}' is not an integer.");
                }
            }
        }
    }

    private ValidationIssue Issue(int? rowKey, string column, string message) =>
        new(Name, ValidationSeverity.Error, rowKey, column, message);
}
