using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>
/// Spreads players who share a depth-chart role and nothing else across the
/// range of overalls the game itself gives that role.
///
/// <para><b>The defect.</b> A roster file usually names a role and a class year
/// and stops there — 64 of the 75 rows of the 2023 Florida State file carry no
/// stats, no award and no draft slot, and eleven of them are the same
/// "Reserve, redshirt freshman" with nothing to tell them apart. Every one of
/// those eleven blended to the same number, so a generated roster came out in
/// spikes where the game's is a smooth curve: 18 players at exactly 78 and 7 at
/// exactly 80, against EA's own Florida State which puts three to nine players
/// on each value from 69 to 84.</para>
///
/// <para><b>Why a role score cannot fix it.</b> The game spreads 14 points
/// inside its starters (73 at the 10th percentile to 87 at the 90th), 8 inside
/// its backups and reserves, 9 inside its walk-ons. No single number reproduces
/// that, and the spread is not explained by class year either — measured across
/// 11,730 players on 138 teams, class moves the median within a role band by
/// one point, four for starters. It is variation the roster file gives no
/// evidence about at all.</para>
///
/// <para><b>What this does.</b> It reproduces the game's <em>distribution</em>
/// without claiming to know which player is which: within one role, players are
/// ordered by what evidence they do have and laid along the measured percentile
/// curve for that role. This is the same thing <see cref="RosterFiller"/>
/// already does for slots no historical player fills, and for the same reason —
/// the shape of a roster is measurable even when the individuals are not.</para>
///
/// <para><b>It only ever moves a player the file says nothing about.</b> A
/// player with a stat line, an award or a draft slot has a number of their own
/// and keeps it; only those whose entire record is a role — and possibly a
/// recruiting rating — are laid out on the curve.</para>
/// </summary>
public static class RoleSpread
{
    /// <summary>Signals that do not, by themselves, individuate a player.</summary>
    private static readonly HashSet<string> ThinSignals =
        new(StringComparer.Ordinal) { "role", "recruiting" };

    /// <summary>Class years, most senior first, as the tie-break within a role.</summary>
    private static readonly Dictionary<string, int> Seniority =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Senior"] = 4, ["Junior"] = 3, ["Sophomore"] = 2, ["Freshman"] = 1,
        };

    /// <summary>
    /// True when the roster file gave nothing that separates this player from
    /// anyone else in their role.
    /// </summary>
    public static bool IsUndifferentiated(RatedPlayer player) =>
        // A source roster's own overall is the most individuating thing a
        // player can carry: somebody sat down and decided this one was an 84.
        // It reaches the engine as an outright replacement rather than as a
        // weighed signal, so it has to be named here — otherwise the spread
        // would lay a squad of real numbers back out along an average curve.
        player.Player.Evidence.SourceOverall is null &&
        player.Ratings.Talent.Signals.Count > 0 &&
        player.Ratings.Talent.Signals.All(s => ThinSignals.Contains(s.Name));

    /// <summary>
    /// Plans the spread for one team.
    /// </summary>
    /// <param name="rated">Every player rated for this team.</param>
    /// <param name="model">The rating model, for its measured role curves.</param>
    /// <returns>
    /// One entry per player who should be regenerated, with the overall to
    /// generate them at and the reason to report.
    /// </returns>
    public static IReadOnlyList<(RatedPlayer Player, int Overall, string Reason)> Plan(
        IReadOnlyList<RatedPlayer> rated, RatingModelSet model)
    {
        var moves = new List<(RatedPlayer, int, string)>();
        if (model.RoleSpread.Count == 0)
        {
            return moves;
        }

        foreach (var group in rated.Where(IsUndifferentiated)
                     .GroupBy(r => (r.Player.Evidence.Role ?? "").Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (!model.RoleSpread.TryGetValue(group.Key.ToLowerInvariant(), out var curve) || curve.Length == 0)
            {
                continue;
            }

            // One player in a role is not a pile, and moving them off their own
            // blended number would be losing information rather than adding it.
            var players = group.ToList();
            if (players.Count < 2)
            {
                continue;
            }

            // Ordered by the evidence they do have — the blended overall first,
            // then class seniority, then the roster slot, so the same file
            // always produces the same roster.
            var ordered = players
                .OrderByDescending(p => p.Ratings.Overall)
                .ThenByDescending(p => Seniority.GetValueOrDefault(p.Player.ClassYear ?? "", 0))
                .ThenBy(p => p.Player.LastName, StringComparer.Ordinal)
                .ThenBy(p => p.Player.FirstName, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                // Percentile of the middle of this player's share of the group,
                // so a group of one would sit at the median rather than an end.
                var percentile = 1.0 - ((i + 0.5) / ordered.Count);
                var overall = (int)Math.Round(RatingModelSet.Interpolate(curve, percentile));
                if (overall == ordered[i].Ratings.Overall)
                {
                    continue;
                }

                moves.Add((ordered[i], overall,
                    $"Spread across the {group.Key.ToLowerInvariant()} range the game itself carries: " +
                    $"{ordered.Count} players on this roster have a role and nothing else to tell them " +
                    $"apart, and this one sits at the {percentile:P0} mark of that group."));
            }
        }

        return moves;
    }
}
