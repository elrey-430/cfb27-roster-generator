using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>One player's generated ratings, awaiting roster-level checks.</summary>
/// <param name="Player">The historical player.</param>
/// <param name="Position">The CFB27 position assigned.</param>
/// <param name="PlayerType">The archetype whose formula was used.</param>
/// <param name="Ratings">The generated ratings.</param>
public sealed record RatedPlayer(
    HistoricalPlayer Player,
    string Position,
    string PlayerType,
    GeneratedRatings Ratings);

/// <summary>
/// A roster-level sanity pass: a player listed as a backup or reserve must
/// not out-rate the established starter at their position group unless the
/// evidence justifies it.
///
/// "Justified" is deliberately narrow — High confidence backed by a draft
/// slot or a major award — because that is exactly the real case the rule
/// must not break: a future first-round pick genuinely can sit behind a
/// senior starter. Everyone else is pulled just below the starter, and the
/// change is reported rather than applied silently.
/// </summary>
public static class DepthConsistency
{
    /// <summary>Roles treated as "not the starter".</summary>
    private static readonly HashSet<string> BackupRoles =
        new(StringComparer.OrdinalIgnoreCase) { "backup", "reserve", "walk-on", "walkon" };

    /// <summary>
    /// Finds backups rated above their position's starter.
    /// </summary>
    /// <param name="rated">All rated players on the team.</param>
    /// <returns>
    /// One entry per violation: the player and the overall ceiling they
    /// should be regenerated under.
    /// </returns>
    public static IReadOnlyList<(RatedPlayer Player, int Ceiling, string Reason)> FindViolations(
        IReadOnlyList<RatedPlayer> rated)
    {
        var violations = new List<(RatedPlayer, int, string)>();

        foreach (var group in rated.GroupBy(r => r.Ratings.PositionGroup))
        {
            var starters = group
                .Where(r => string.Equals(r.Player.Evidence.Role, "Starter", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (starters.Count == 0)
            {
                continue;
            }

            var bestStarter = starters.MaxBy(r => r.Ratings.Overall)!;
            foreach (var candidate in group)
            {
                var role = candidate.Player.Evidence.Role;
                if (role is null || !BackupRoles.Contains(role.Trim()))
                {
                    continue;
                }

                if (candidate.Ratings.Overall <= bestStarter.Ratings.Overall)
                {
                    continue;
                }

                if (IsJustified(candidate))
                {
                    continue;
                }

                violations.Add((candidate, bestStarter.Ratings.Overall - 1,
                    $"Listed as {role.Trim()} but rated {candidate.Ratings.Overall}, above the " +
                    $"{group.Key} starter {bestStarter.Player.FirstName} {bestStarter.Player.LastName} " +
                    $"({bestStarter.Ratings.Overall}); capped at {bestStarter.Ratings.Overall - 1}."));
            }
        }

        return violations;
    }

    /// <summary>
    /// True when strong individual evidence explains a backup out-rating the
    /// starter (a drafted player or a major award winner behind a veteran).
    ///
    /// <para>A draft slot justifies it on its own, at any confidence. That is
    /// the case this rule's own description names — "a future first-round pick
    /// genuinely can sit behind a senior starter" — and a player whose roster
    /// row carries nothing but a draft pick reaches only Medium confidence, so
    /// requiring High would have caught exactly the player the exemption is
    /// for. It would also fight the drafted floor: the engine would raise them
    /// to it and this pass would pull them back under.</para>
    /// </summary>
    private static bool IsJustified(RatedPlayer candidate)
    {
        if (candidate.Player.Evidence.WasDrafted)
        {
            return true;
        }

        if (candidate.Ratings.Confidence != RatingConfidence.High)
        {
            return false;
        }

        return candidate.Ratings.Talent.Signals.Any(s => s.Name is "draft" or "awards");
    }
}
