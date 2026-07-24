using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// Enum-valued columns must hold one of their known values: position,
/// school year, redshirt status, and the 0–99 jersey number range.
/// </summary>
public sealed class EnumFieldsRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "EnumFields";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.State;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        foreach (var player in context.Roster.Players)
        {
            if (!PlayerSchema.Positions.Contains(player.Position))
            {
                yield return Issue(player.RowKey, PlayerColumns.Position,
                    $"'{player.Position}' is not a valid position. Valid: {string.Join(", ", PlayerSchema.Positions)}.");
            }

            if (!PlayerSchema.SchoolYears.Contains(player.SchoolYear))
            {
                yield return Issue(player.RowKey, PlayerColumns.SchoolYear,
                    $"'{player.SchoolYear}' is not a valid school year. Valid: {string.Join(", ", PlayerSchema.SchoolYears)}.");
            }

            if (!PlayerSchema.RedshirtStatuses.Contains(player.RedshirtStatus))
            {
                yield return Issue(player.RowKey, PlayerColumns.RedshirtStatus,
                    $"'{player.RedshirtStatus}' is not a valid redshirt status. Valid: {string.Join(", ", PlayerSchema.RedshirtStatuses)}.");
            }

            var jerseyRaw = player.GetRaw(PlayerColumns.JerseyNum);
            if (int.TryParse(jerseyRaw, out var jersey) &&
                jersey is < PlayerSchema.JerseyNumMin or > PlayerSchema.JerseyNumMax)
            {
                yield return Issue(player.RowKey, PlayerColumns.JerseyNum,
                    $"Jersey number {jersey} is outside {PlayerSchema.JerseyNumMin}–{PlayerSchema.JerseyNumMax}.");
            }
        }
    }

    private ValidationIssue Issue(int rowKey, string column, string message) =>
        new(Name, ValidationSeverity.Error, rowKey, column, message);
}
