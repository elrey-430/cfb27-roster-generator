using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Conversion;

/// <summary>What to do with roster slots the historical roster did not fill.</summary>
public enum RosterFillMode
{
    /// <summary>
    /// Leave the original fictional players untouched. Honest but visibly
    /// wrong: some of them out-rate the historical roster and start.
    /// </summary>
    Leave,

    /// <summary>
    /// Re-rate the leftovers as end-of-roster walk-ons using the measured
    /// depth curve, holding every one of them below the historical players at
    /// their position.
    /// </summary>
    Fill,
}

/// <summary>One filled slot, for the generation report.</summary>
/// <param name="RowKey">The slot's row key.</param>
/// <param name="Name">The player's name, which the fill does not change.</param>
/// <param name="Position">Position played.</param>
/// <param name="Rank">Roster rank the depth curve was read at.</param>
/// <param name="PreviousOverall">Overall before the fill.</param>
/// <param name="Overall">Overall after the fill.</param>
/// <param name="ClassYear">Class year written.</param>
/// <param name="Reason">Why this overall, in one line.</param>
public sealed record FilledSlot(
    int RowKey,
    string Name,
    string Position,
    int Rank,
    int PreviousOverall,
    int Overall,
    string ClassYear,
    string Reason);

/// <summary>
/// Turns the roster slots a user did not supply players for into believable
/// end-of-roster depth.
///
/// A CFB27 team always carries exactly 85 players, but a historical roster a
/// user can actually research is the two-deep plus whoever else is
/// documented — the FSU 2023 file has 75. The remaining slots keep EA's
/// fictional players, and because the game builds its depth chart from
/// ratings alone (there is no depth-chart column on the player table), a
/// leftover 82-overall fictional quarterback simply takes the job from the
/// historical starter. Every rating decision made upstream is then invisible
/// at the position that matters most.
///
/// Rather than invent what a walk-on looks like, this reads it off the game:
/// <c>data/RosterDepth.json</c> holds the median overall at each roster rank
/// and the class-year mix at each depth, measured across 138 untouched FBS
/// rosters. A filler landing at rank 80 is given what the game itself puts at
/// rank 80, and is then held below the weakest historical player at its own
/// position so it can never appear ahead of one.
///
/// The fill deliberately keeps each slot's <b>name, jersey number and
/// portrait</b>. EA's generated names are already realistic, keeping them
/// avoids shipping a name pool with its own duplicate and era problems, and
/// the jersey numbers are already unique within the team. The defect being
/// fixed is the rating, not the identity.
/// </summary>
public sealed class RosterFiller
{
    private readonly RosterDepthModel _depth;
    private readonly RatingEngine _engine;

    /// <summary>Creates a filler over the measured depth model and the rating engine.</summary>
    public RosterFiller(RosterDepthModel depth, RatingEngine engine)
    {
        _depth = depth;
        _engine = engine;
    }

    /// <summary>
    /// Fills every leftover slot.
    /// </summary>
    /// <param name="session">Edit session the writes go through.</param>
    /// <param name="leftovers">Slots no historical player took.</param>
    /// <param name="placedOveralls">
    /// Overall of each historical player already placed, keyed by position —
    /// the ceiling that keeps fillers off the depth chart.
    /// </param>
    /// <param name="placedCount">How many historical players were placed.</param>
    public IReadOnlyList<FilledSlot> Fill(
        RosterEditSession session,
        IReadOnlyList<Player> leftovers,
        IReadOnlyDictionary<string, int> placedOveralls,
        int placedCount)
    {
        // Best leftover first, so the ranks handed to the depth curve run
        // downwards in the same order the game's own roster does. Sorting by
        // row key second keeps the result deterministic when overalls tie —
        // the FSU regression test depends on byte-identical output.
        var ordered = leftovers
            .OrderByDescending(p => p.OverallRating)
            .ThenBy(p => p.RowKey)
            .ToList();

        var classYears = AssignClassYears(ordered.Count, placedCount);
        var filled = new List<FilledSlot>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            var slot = ordered[i];
            var rank = placedCount + i + 1;
            var previousOverall = slot.OverallRating;
            var classYear = classYears[i];

            var curveTarget = _depth.OverallAtRank(rank);
            var target = curveTarget;
            var reason = $"roster rank {rank} rates {curveTarget} in a real save";

            // The invariant that actually fixes the depth chart: a filler may
            // never reach the weakest historical player at its own position.
            if (placedOveralls.TryGetValue(slot.Position, out var weakestHistorical))
            {
                var ceiling = weakestHistorical - _depth.MarginBelowHistorical;
                if (ceiling < target)
                {
                    target = ceiling;
                    reason = $"held {_depth.MarginBelowHistorical} below the weakest historical " +
                             $"{slot.Position} ({weakestHistorical})";
                }
            }

            if (target < _depth.MinimumOverall)
            {
                target = _depth.MinimumOverall;
                reason = $"raised to the {_depth.MinimumOverall} floor";
            }

            // Generate through the same engine as everyone else, with no
            // evidence: a walk-on's ratings come from position baselines and
            // class year, which is exactly what the engine does with an empty
            // evidence set. The slot's existing archetype is kept, so the
            // overall is recomputed with the formula that already applies to
            // it and the two stay in agreement.
            var synthetic = new HistoricalPlayer
            {
                FirstName = slot.FirstName,
                LastName = slot.LastName,
                Position = slot.Position,
                HeightInches = slot.HeightInches,
                WeightPounds = slot.WeightPounds,
                ClassYear = classYear,
            };

            var ratings = _engine.Generate(
                slot.Position,
                slot.GetRaw(PlayerColumns.PlayerType),
                synthetic,
                RatingEvidence.Empty,
                overallCeiling: target);

            session.SetGeneratedRatings(slot, ratings.Attributes, ratings.Overall);
            if (ClassYear.TryParse(classYear, out var schoolYear, out var redshirtStatus))
            {
                session.SetSchoolYear(slot, schoolYear);
                session.SetRedshirtStatus(slot, redshirtStatus);
            }

            filled.Add(new FilledSlot(
                slot.RowKey, $"{slot.FirstName} {slot.LastName}", slot.Position, rank,
                previousOverall, ratings.Overall, classYear, reason));
        }

        return filled;
    }

    /// <summary>
    /// Hands out class years to the fillers in the proportions the game uses
    /// at those roster ranks — the bottom ten of a real roster is 64%
    /// freshmen against a flat 25% across the top 75.
    ///
    /// Assignment is by largest remainder rather than by sampling, so the same
    /// input always produces the same roster; the youngest classes go to the
    /// lowest ranks, matching the observed shape.
    /// </summary>
    private List<string> AssignClassYears(int count, int placedCount)
    {
        var result = new List<string>(count);
        if (count == 0)
        {
            return result;
        }

        // Read the mix at the middle of the range being filled: one band
        // covers the whole fill unless it straddles a boundary, and taking the
        // midpoint avoids an abrupt switch part-way down.
        var midRank = placedCount + (count / 2) + 1;
        var weights = _depth.ClassWeightsAtRank(midRank);
        if (weights.Count == 0)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add("Freshman");
            }

            return result;
        }

        var quotas = LargestRemainder(weights, count);

        // Youngest first: the list is consumed in rank order, and the fillers
        // are ordered best-to-worst, so seniors land nearest the historical
        // roster and freshmen at the very bottom.
        foreach (var classYear in new[] { "Senior", "Junior", "Sophomore", "Freshman" })
        {
            if (!quotas.TryGetValue(classYear, out var quota))
            {
                continue;
            }

            for (var i = 0; i < quota; i++)
            {
                result.Add(classYear);
            }
        }

        // Any class year the model carries that is not one of the four above
        // still has to be placed, or the roster comes up short.
        foreach (var (classYear, quota) in quotas)
        {
            if (classYear is "Senior" or "Junior" or "Sophomore" or "Freshman")
            {
                continue;
            }

            for (var i = 0; i < quota; i++)
            {
                result.Add(classYear);
            }
        }

        return result;
    }

    /// <summary>
    /// Splits <paramref name="count"/> places among weighted keys so the
    /// totals match exactly, giving the leftover places to the largest
    /// fractional parts.
    /// </summary>
    private static Dictionary<string, int> LargestRemainder(
        IReadOnlyDictionary<string, double> weights, int count)
    {
        var total = weights.Values.Sum();
        if (total <= 0)
        {
            return new Dictionary<string, int>();
        }

        var exact = weights.ToDictionary(w => w.Key, w => w.Value / total * count);
        var quotas = exact.ToDictionary(e => e.Key, e => (int)Math.Floor(e.Value));

        var remaining = count - quotas.Values.Sum();
        foreach (var key in exact
                     .OrderByDescending(e => e.Value - Math.Floor(e.Value))
                     .ThenBy(e => e.Key, StringComparer.Ordinal)
                     .Select(e => e.Key))
        {
            if (remaining <= 0)
            {
                break;
            }

            quotas[key]++;
            remaining--;
        }

        return quotas;
    }
}
