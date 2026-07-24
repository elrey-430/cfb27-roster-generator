namespace RosterGenerator.Core.Editing;

/// <summary>
/// The declared purpose of an edit to one player row. The distinction matters
/// because the correct handling of the identity-derived asset fields
/// (<c>PLYR_ASSETNAME</c>, <c>GenericHeadAssetName</c>, <c>PLYR_PORTRAIT</c>)
/// depends on it — a fact confirmed by diffing real save edits. The intent is
/// recorded explicitly rather than inferred, and validation checks that the
/// row's actual changes are consistent with it.
/// </summary>
public enum EditIntent
{
    /// <summary>
    /// Cosmetic rename of the same person: name text changes, identity assets
    /// (portrait/head model) must stay untouched. This matches observed
    /// in-game rename behavior.
    /// </summary>
    Rename,

    /// <summary>
    /// Substitute a different real person into this roster slot: identity
    /// assets must be updated deliberately (or the portrait/head model will
    /// belong to the old identity).
    /// </summary>
    ReplaceIdentity,

    /// <summary>
    /// Move the player to a different team. Requires the Group 4 companion
    /// updates (PrevTeamIndex, PLYR_PREVTEAMID, NIL fields zeroed).
    /// </summary>
    Transfer,

    /// <summary>
    /// In-place attribute change (jersey, height, class, ratings...). No
    /// identity or team side effects.
    /// </summary>
    AttributeChange,
}
