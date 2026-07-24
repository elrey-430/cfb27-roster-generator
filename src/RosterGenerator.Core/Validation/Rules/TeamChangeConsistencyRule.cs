using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// A team change is a confirmed multi-field operation, not a single-field
/// edit. Diffing real transfer edits showed that whenever <c>TeamIndex</c>
/// changes, the save also sets <c>PrevTeamIndex</c> and
/// <c>PLYR_PREVTEAMID</c> to the old team and resets both NIL fields to 0.
/// This rule detects a <c>TeamIndex</c> change (versus the loaded file) whose
/// companion fields were not updated to match.
/// </summary>
public sealed class TeamChangeConsistencyRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "TeamChangeConsistency";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.ChangeDriven;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        foreach (var player in context.Roster.Players)
        {
            var originalTeam = context.Roster.GetOriginalValue(player.RowIndex, PlayerColumns.TeamIndex);
            var currentTeam = player.GetRaw(PlayerColumns.TeamIndex);
            if (string.Equals(originalTeam, currentTeam, StringComparison.Ordinal))
            {
                continue;
            }

            var prevTeamIndex = player.GetRaw(PlayerColumns.PrevTeamIndex);
            if (!string.Equals(prevTeamIndex, originalTeam, StringComparison.Ordinal))
            {
                yield return Issue(player.RowKey, PlayerColumns.PrevTeamIndex,
                    $"TeamIndex changed from {originalTeam} to {currentTeam}, but PrevTeamIndex is '{prevTeamIndex}' " +
                    $"instead of the old team '{originalTeam}'. A stale value (including the 255 sentinel) will " +
                    "leave incorrect team history in the save.");
            }

            var prevTeamId = player.GetRaw(PlayerColumns.PrevTeamId);
            if (!string.Equals(prevTeamId, originalTeam, StringComparison.Ordinal))
            {
                yield return Issue(player.RowKey, PlayerColumns.PrevTeamId,
                    $"TeamIndex changed from {originalTeam} to {currentTeam}, but PLYR_PREVTEAMID is '{prevTeamId}' " +
                    $"instead of the old team '{originalTeam}'. This legacy field must stay in sync with PrevTeamIndex.");
            }

            foreach (var nilColumn in new[] { PlayerColumns.BaseNilValue, PlayerColumns.CurrentNilCompensation })
            {
                var nil = player.GetRaw(nilColumn);
                if (!string.Equals(nil, "0", StringComparison.Ordinal))
                {
                    yield return Issue(player.RowKey, nilColumn,
                        $"TeamIndex changed from {originalTeam} to {currentTeam}, but {nilColumn} is '{nil}'. " +
                        "Real transfers reset NIL fields to 0; a stale value would carry over to the new team.");
                }
            }
        }
    }

    private ValidationIssue Issue(int rowKey, string column, string message) =>
        new(Name, ValidationSeverity.Error, rowKey, column, message);
}
