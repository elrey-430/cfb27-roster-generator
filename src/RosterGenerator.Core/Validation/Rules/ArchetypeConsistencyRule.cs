using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Validation.Rules;

/// <summary>
/// The archetype (<c>PlayerType</c>) selects which of EA's overall formulas
/// applies, so it carries two obligations that hand-editing tools miss:
///
/// <list type="bullet">
/// <item>The archetype must be one the position actually has. A manually
///       edited save was found carrying an LOLB with <c>MLB_PassCoverage</c>.</item>
/// <item>Changing it must be followed by recomputing <c>OverallRating</c>.
///       In that same save 35 of 85 players kept an overall that matches a
///       <i>different</i> archetype — the one they had before the edit —
///       against 99.3% agreement in the untouched base save.</item>
/// </list>
///
/// Both checks need EA's formulas, so the rule is inert unless the caller
/// supplies them on the validation context.
/// </summary>
public sealed class ArchetypeConsistencyRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "ArchetypeConsistency";

    /// <inheritdoc />
    public ValidationRuleKind Kind => ValidationRuleKind.State;

    /// <inheritdoc />
    public IEnumerable<ValidationIssue> Validate(RosterValidationContext context)
    {
        var formulas = context.OverallFormulas;
        if (formulas is null || !context.Roster.Document.HasColumn(PlayerColumns.PlayerType))
        {
            yield break;
        }

        foreach (var player in context.Roster.Players)
        {
            var archetype = player.GetRaw(PlayerColumns.PlayerType);
            if (archetype.Length == 0)
            {
                continue;
            }

            var legal = formulas.PlayerTypesFor(player.Position).ToList();
            if (legal.Count == 0)
            {
                continue;
            }

            if (!legal.Contains(archetype))
            {
                yield return new ValidationIssue(Name, ValidationSeverity.Error, player.RowKey,
                    PlayerColumns.PlayerType,
                    $"Archetype '{archetype}' is not valid for position {player.Position}. " +
                    $"Valid: {string.Join(", ", legal)}.");
                continue;
            }

            // Build the value set from every rating column present, not just
            // the current archetype's coefficients: archetypes weigh
            // different attributes, so a narrower set would make the
            // cross-archetype comparison below wrong.
            var formula = formulas.Resolve(player.Position, archetype);
            var attributes = PlayerSchema.NumericRatingColumns
                .Where(context.Roster.Document.HasColumn)
                .ToDictionary(a => a, a => (double)player.GetInt(a));
            var expected = formula.Compute(attributes);
            if (expected == player.OverallRating)
            {
                continue;
            }

            // Only complain when the stored overall belongs to a DIFFERENT
            // archetype at this position — that is the signature of an
            // archetype change without a recompute, as opposed to the small
            // formula noise the source data already has.
            var explains = legal
                .Where(t => !string.Equals(t, archetype, StringComparison.Ordinal))
                .Where(t => formulas.Resolve(player.Position, t).Compute(attributes) == player.OverallRating)
                .ToList();
            if (explains.Count > 0)
            {
                // Reported against PlayerType, not OverallRating: the
                // archetype change is what causes the mismatch, so keying the
                // issue to it makes an edit an error while an inconsistency
                // already present in a loaded file stays a warning.
                yield return new ValidationIssue(Name, ValidationSeverity.Error, player.RowKey,
                    PlayerColumns.PlayerType,
                    $"OverallRating {player.OverallRating} does not match archetype '{archetype}' " +
                    $"(which gives {expected}), but does match '{string.Join("' / '", explains)}'. " +
                    "The archetype was changed without recomputing the overall.");
            }
        }
    }
}
