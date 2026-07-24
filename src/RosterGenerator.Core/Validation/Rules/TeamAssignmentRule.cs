using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// <c>TeamIndex</c> must be an integer in 0–255, and — when the caller
/// supplies the save's known team indices — must refer to a team that
/// actually exists (255 is always accepted as the "no team" sentinel).
/// </summary>
public sealed class TeamAssignmentRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "TeamAssignment";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.State;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        foreach (var player in context.Roster.Players)
        {
            var raw = player.GetRaw(PlayerColumns.TeamIndex);
            if (!int.TryParse(raw, out var teamIndex))
            {
                yield return Issue(player.RowKey, $"TeamIndex '{raw}' is not an integer.");
                continue;
            }

            if (teamIndex is < 0 or > PlayerSchema.NoTeamSentinel)
            {
                yield return Issue(player.RowKey,
                    $"TeamIndex {teamIndex} is outside the valid range 0–{PlayerSchema.NoTeamSentinel}.");
                continue;
            }

            if (context.KnownTeamIndices is not null &&
                teamIndex != PlayerSchema.NoTeamSentinel &&
                !context.KnownTeamIndices.Contains(teamIndex))
            {
                yield return Issue(player.RowKey,
                    $"TeamIndex {teamIndex} does not match any team in this save.");
            }
        }
    }

    private ValidationIssue Issue(int rowKey, string message) =>
        new(Name, ValidationSeverity.Error, rowKey, PlayerColumns.TeamIndex, message);
}
