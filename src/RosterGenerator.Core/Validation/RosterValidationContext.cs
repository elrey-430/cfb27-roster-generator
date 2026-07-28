using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Model;

namespace RosterGenerator.Core.Validation;

/// <summary>
/// Everything a validation rule can see: the roster (with its load-time
/// snapshot for change detection), the edit session's declared intents, and
/// the set of team indices that are valid in this save.
/// </summary>
public sealed class RosterValidationContext
{
    /// <summary>
    /// Creates a context.
    /// </summary>
    /// <param name="roster">The roster to validate.</param>
    /// <param name="editSession">
    /// The edit session whose intents explain the changes, or null when
    /// validating a file as-is (then any detected change is undeclared).
    /// </param>
    /// <param name="knownTeamIndices">
    /// Valid team indices for this save, ideally loaded from the save's Team
    /// table. Null skips membership checking (the 0–255 range and sentinel
    /// rules still apply). The observed base save uses 0–137 plus 255.
    /// </param>
    /// <param name="overallFormulas">
    /// EA's overall formulas. Supplying them enables the archetype
    /// consistency checks; without them that rule stays inert.
    /// </param>
    /// <param name="commentaryIds">
    /// The measured surname → commentary mapping. Supplying it enables the
    /// commentary check; without it that rule stays inert.
    /// </param>
    public RosterValidationContext(
        PlayerRoster roster,
        RosterEditSession? editSession = null,
        IReadOnlySet<int>? knownTeamIndices = null,
        Rating.OverallFormulaSet? overallFormulas = null,
        Mapping.CommentaryIdSet? commentaryIds = null)
    {
        Roster = roster;
        EditSession = editSession;
        KnownTeamIndices = knownTeamIndices;
        OverallFormulas = overallFormulas;
        CommentaryIds = commentaryIds;
    }

    /// <summary>The roster being validated.</summary>
    public PlayerRoster Roster { get; }

    /// <summary>Declared edit intents, or null when validating a raw file.</summary>
    public RosterEditSession? EditSession { get; }

    /// <summary>Valid team indices, or null to skip membership checks.</summary>
    public IReadOnlySet<int>? KnownTeamIndices { get; }

    /// <summary>EA's overall formulas, or null to skip archetype checks.</summary>
    public Rating.OverallFormulaSet? OverallFormulas { get; }

    /// <summary>
    /// The measured surname → commentary mapping, or null to skip the
    /// commentary check.
    /// </summary>
    public Mapping.CommentaryIdSet? CommentaryIds { get; }
}
