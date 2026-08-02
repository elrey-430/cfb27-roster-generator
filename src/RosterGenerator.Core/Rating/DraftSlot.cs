namespace RosterGenerator.Core.Rating;

/// <summary>
/// What a player's <c>DraftRound</c> and <c>DraftPick</c> together mean.
///
/// <para><b>The defect this exists for.</b> <c>DraftPick</c> was read as an
/// overall pick number and <c>DraftRound</c> was used only when the pick was
/// missing. So a user writing down a second-round pick the way the draft is
/// actually announced — <em>round 2, pick 1</em> — got the first pick of the
/// entire draft, and a 33rd-overall selection came out at 97 or better instead
/// of 93.</para>
///
/// <para><b>Both spellings are now read.</b> A pick may be given as an overall
/// number or as a pick within its round, and which one the user meant is
/// decided by arithmetic rather than by a setting:</para>
///
/// <list type="bullet">
/// <item>A pick larger than a round holds cannot be a pick <em>within</em> a
/// round, so it is an overall number. <c>Round 2, pick 45</c> is the 45th
/// selection — which is indeed the 13th pick of round two.</item>
/// <item>Otherwise, with a round given, the pick is a position inside it.
/// <c>Round 2, pick 1</c> is the 33rd selection.</item>
/// <item>In round one the two readings agree, so nothing has to be decided.</item>
/// </list>
///
/// <para>A round on its own still means the middle of that round, and a pick on
/// its own is still an overall number — neither of those had another sensible
/// reading.</para>
/// </summary>
public static class DraftSlot
{
    /// <summary>How the two fields were read.</summary>
    public enum Reading
    {
        /// <summary>Neither field was given.</summary>
        None,

        /// <summary>The pick was already an overall number.</summary>
        Overall,

        /// <summary>The pick was a position within its round.</summary>
        WithinRound,

        /// <summary>Only a round was given; the middle of it was used.</summary>
        RoundMidpoint,
    }

    /// <summary>The resolved slot.</summary>
    /// <param name="OverallPick">The overall pick number, or null when nothing was given.</param>
    /// <param name="How">Which reading was applied.</param>
    /// <param name="Note">
    /// Set when the round and the pick disagree by more than a round's worth of
    /// compensatory selections — the pick is believed and this says so.
    /// </param>
    public readonly record struct Resolved(int? OverallPick, Reading How, string? Note)
    {
        /// <summary>How to describe the slot in a player's reasons.</summary>
        public string Describe(int? round) => How switch
        {
            Reading.WithinRound => $"Drafted #{OverallPick} overall (round {round}, pick {PickInRound(round)})",
            Reading.RoundMidpoint => $"Drafted #{OverallPick} overall (estimated from round {round})",
            _ => $"Drafted #{OverallPick} overall",
        };

        private int PickInRound(int? round) =>
            round is int r && OverallPick is int overall ? overall - ((r - 1) * PicksPerRound) : 0;
    }

    /// <summary>
    /// Selections in a round. The modern draft has 32 and compensatory picks
    /// push some rounds past it, which is why a disagreement of one round is
    /// tolerated below rather than reported.
    /// </summary>
    public const int PicksPerRound = 32;

    /// <summary>Resolves the two fields into one overall pick number.</summary>
    /// <param name="round">The <c>DraftRound</c> column, or null.</param>
    /// <param name="pick">The <c>DraftPick</c> column, or null.</param>
    public static Resolved Resolve(int? round, int? pick)
    {
        if (pick is not int given || given <= 0)
        {
            return round is int only && only > 0
                ? new Resolved(((only - 1) * PicksPerRound) + (PicksPerRound / 2), Reading.RoundMidpoint, null)
                : new Resolved(null, Reading.None, null);
        }

        if (round is not int inRound || inRound <= 0)
        {
            return new Resolved(given, Reading.Overall, null);
        }

        // Larger than a round holds, so it cannot be a position within one.
        if (given > PicksPerRound)
        {
            var implied = ((given - 1) / PicksPerRound) + 1;
            var note = Math.Abs(implied - inRound) > 1
                ? $"Round {inRound} and pick {given} disagree: pick {given} falls in round {implied}. " +
                  "The pick number is the more specific of the two and was used."
                : null;
            return new Resolved(given, Reading.Overall, note);
        }

        // Round one is the one place the two readings cannot differ.
        return inRound == 1
            ? new Resolved(given, Reading.Overall, null)
            : new Resolved(((inRound - 1) * PicksPerRound) + given, Reading.WithinRound, null);
    }
}
