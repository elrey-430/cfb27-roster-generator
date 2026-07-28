using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// Checks that a player's commentary index matches the name they now carry.
///
/// <para>This rule replaced <c>OpaqueFieldGuard</c>, which forbade any change
/// to <c>PLYR_COMMENT</c> on the grounds that the field was not understood. It
/// is understood now — it selects the recorded audio the announcers use, and
/// the game rewrites it to match the surname whenever a player is renamed — so
/// a blanket ban would prevent the tool doing the very thing that stops a
/// recreated player being called by the previous occupant's name.</para>
///
/// <para>The lock is replaced rather than removed. A "do not touch" rule
/// becomes a "must be right" rule: the value written has to be either the id
/// the measured mapping gives for that player's current surname, or
/// <see cref="CommentaryIdSet.None"/>. Anything else means a stale or invented
/// index, which is the exact defect this whole change exists to fix.</para>
///
/// <para>Untouched players are not examined. A save arrives with its own
/// pairings and a handful are inconsistent for reasons that predate this tool;
/// flagging those would be reporting the user's save to them as an error.</para>
/// </summary>
public sealed class CommentaryConsistencyRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "CommentaryConsistency";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.ChangeDriven;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        if (context.CommentaryIds is not { } commentary)
        {
            yield break;
        }

        foreach (var player in context.Roster.Players)
        {
            var original = context.Roster.GetOriginalValue(player.RowIndex, PlayerColumns.Comment);
            var current = player.GetRaw(PlayerColumns.Comment);
            if (string.Equals(original, current, StringComparison.Ordinal))
            {
                continue;
            }

            if (!int.TryParse(current, out var written))
            {
                yield return new ValidationIssue(
                    Name, ValidationSeverity.Error, player.RowKey, PlayerColumns.Comment,
                    $"PLYR_COMMENT was set to '{current}', which is not a number.");
                continue;
            }

            var expected = commentary.ForLastName(player.LastName);
            if (written != expected && written != CommentaryIdSet.None)
            {
                yield return new ValidationIssue(
                    Name, ValidationSeverity.Error, player.RowKey, PlayerColumns.Comment,
                    $"PLYR_COMMENT was set to {written}, but the commentary index for " +
                    $"'{player.LastName}' is {expected}. Writing another player's index is what makes " +
                    "the announcers call this player by the wrong name.");
            }
        }
    }
}
