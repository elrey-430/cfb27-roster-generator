namespace RosterGenerator.Core.Schema;

/// <summary>
/// Canonical CFB27 <c>Player</c> table column names used by the typed layer.
/// Only columns with empirically confirmed meaning get a constant here; the
/// remaining ~250 columns are carried through untouched as raw strings.
/// See <c>docs/Schema.md</c> for the evidence behind each field group.
/// </summary>
public static class PlayerColumns
{
    // -- Export bookkeeping columns added by the save-export tool ------------

    /// <summary>Table index of the source table in the save (always 152).</summary>
    public const string TableIndex = "_tableIndex";

    /// <summary>Table name of the source table (always "Player").</summary>
    public const string TableName = "_tableName";

    /// <summary>Row key. Unique, stable within one export; the primary key.</summary>
    public const string Row = "_row";

    /// <summary>"true" for unused pool slots, "false" for real records.</summary>
    public const string IsEmpty = "_isEmpty";

    // -- Group 1: core identity (safe to edit directly) ----------------------

    /// <summary>Player first name (plain text).</summary>
    public const string FirstName = "FirstName";

    /// <summary>Player last name (plain text).</summary>
    public const string LastName = "LastName";

    /// <summary>Jersey number, integer 0–99.</summary>
    public const string JerseyNum = "JerseyNum";

    /// <summary>Height in raw inches (no encoding).</summary>
    public const string Height = "Height";

    /// <summary>Class standing enum: Freshman/Sophomore/Junior/Senior.</summary>
    public const string SchoolYear = "SchoolYear";

    /// <summary>Redshirt enum: Eligible/Previous/Ineligible.</summary>
    public const string RedshirtStatus = "RedshirtStatus";

    /// <summary>Position enum (QB, HB, WR, ...).</summary>
    public const string Position = "Position";

    /// <summary>Overall rating, integer 0–99.</summary>
    public const string OverallRating = "OverallRating";

    // -- Group 2: offset-encoded fields (encoding confirmed) -----------------

    /// <summary>
    /// Weight stored as pounds minus 160 (stored 0–240 = 160–400 lb).
    /// Encoding confirmed by correlating a manually edited save's values
    /// against real listed weights and by league-wide position averages;
    /// use <c>Player.WeightPounds</c> rather than writing raw values.
    /// </summary>
    public const string Weight = "Weight";

    /// <summary>
    /// Player archetype (e.g. <c>HB_ElusiveBack</c>). Confirmed writable, but
    /// it selects which of EA's overall formulas applies, so
    /// <see cref="OverallRating"/> must be recomputed whenever it changes.
    /// </summary>
    public const string PlayerType = "PlayerType";

    /// <summary>Home town — free text (3,031 distinct values observed).</summary>
    public const string HomeTown = "PLYR_HOME_TOWN";

    /// <summary>
    /// Home state — a strict 51-value enum: the 50 US states in PascalCase
    /// plus <c>NonUS</c>. See <see cref="PlayerSchema.HomeStates"/>.
    /// </summary>
    public const string HomeState = "PLYR_HOME_STATE";

    // -- Group 3: identity-derived assets (depends on rename-vs-replace) -----

    /// <summary>Asset name derived from identity at generation time.</summary>
    public const string AssetName = "PLYR_ASSETNAME";

    /// <summary>Generic head model asset derived from identity.</summary>
    public const string GenericHeadAssetName = "GenericHeadAssetName";

    /// <summary>Portrait id tied to identity, not name text.</summary>
    public const string Portrait = "PLYR_PORTRAIT";

    /// <summary>
    /// Packed reference to this player's row in the <c>CharacterVisuals</c>
    /// table, where everything they wear is stored. Never written here — the
    /// equipment layer follows it and edits that other table instead.
    /// See <c>RosterGenerator.Core.Equipment.CharacterVisualsReference</c>.
    /// </summary>
    public const string CharacterVisuals = "CharacterVisuals";

    /// <summary>
    /// Index into the recorded commentary audio: what lets the announcers say
    /// a player's name out loud. 0 means they never say it, and 20% of an
    /// untouched save's players hold 0.
    ///
    /// <para>This was previously documented as changing "spontaneously on one
    /// observed rename with no clear trigger", and left alone on that basis.
    /// The trigger was the rename. Renaming players in the game and exporting
    /// again shows it being rewritten to the commentary id of the <b>new
    /// surname</b> every time — so a recreated player who keeps the replaced
    /// player's value is called by the replaced player's name.</para>
    ///
    /// <para>Written from <see cref="Mapping.CommentaryIdSet"/>, which is
    /// measured from saves the game generated rather than guessed at.</para>
    /// </summary>
    public const string Comment = "PLYR_COMMENT";

    // -- Abilities -----------------------------------------------------------

    /// <summary>
    /// The tier a player holds in physical ability slot <paramref name="slot"/>
    /// (1–5): <c>None</c>, <c>Bronze</c>, <c>Silver</c>, <c>Gold</c> or
    /// <c>Platinum</c>.
    ///
    /// <para><b>The slot's ability is not stored here, or anywhere in the
    /// save.</b> These columns are typed <c>AbilitiesRank</c> — the same type
    /// as the mental ability <em>ranks</em> — and hold only how good the player
    /// is. Which ability slot 3 represents comes from the game's own data,
    /// referenced by <c>PositionSignatureAbility</c> and
    /// <c>PositionAbilityTable</c>, neither of which resolves to anything
    /// inside a save. It depends on position and archetype: slot 4 on a nose
    /// tackle is not slot 4 on a receiver.</para>
    ///
    /// <para>So a slot cannot be pointed at a different archetype's ability.
    /// What changes a player's ability <em>set</em> is changing their
    /// archetype, which this tool already does.</para>
    /// </summary>
    public static string PhysicalAbility(int slot) => $"PhysicalAbility{slot}";

    /// <summary>How many physical ability slots a player has.</summary>
    public const int PhysicalAbilitySlots = 5;

    /// <summary>
    /// Mental ability <paramref name="slot"/> (1–3), which — unlike the
    /// physical ones — names the ability outright from a 20-value enum. Rare
    /// and elite: 2.1% of a base save carry any, and 244 of those 248 carry
    /// all three.
    /// </summary>
    public static string MentalAbility(int slot) => $"MentalAbility{slot}";

    /// <summary>The tier held in mental ability slot <paramref name="slot"/> (1–3).</summary>
    public static string MentalAbilityRank(int slot) => $"MentalAbilityRank{slot}";

    /// <summary>How many mental ability slots a player has.</summary>
    public const int MentalAbilitySlots = 3;

    /// <summary>The value both ability families use for an empty slot.</summary>
    public const string NoAbility = "None";

    // -- Group 4: team assignment and its companion fields -------------------

    /// <summary>Current team index (0–137 FBS in observed saves; 255 = none/FCS).</summary>
    public const string TeamIndex = "TeamIndex";

    /// <summary>Previous team index; 255 is the "no previous team" sentinel.</summary>
    public const string PrevTeamIndex = "PrevTeamIndex";

    /// <summary>
    /// The school a transfer came from, held as that school's
    /// <c>TEAM_ORIGID</c> from the Team table — a presentation-level school id
    /// covering more schools than the dynasty's own team list, and NOT a
    /// <see cref="TeamIndex"/>. <c>0</c> means the player never transferred.
    ///
    /// Confirmed against a base save: 133 of the 135 distinct non-zero values
    /// are a <c>TEAM_ORIGID</c> in the same save, and resolving Florida
    /// State's 20 non-zero players yields real, plausible schools (Auburn,
    /// Notre Dame, Texas A&amp;M, Duke...). Values below the Team table's
    /// range — <c>1009</c> above all, which appears 363 times — stand for a
    /// school the dynasty does not carry.
    ///
    /// This does NOT move with <see cref="PrevTeamIndex"/>, which is
    /// <c>255</c> for every player in an untouched save including those 20
    /// transfers. The two fields track different things.
    /// </summary>
    public const string PrevTeamId = "PLYR_PREVTEAMID";

    /// <summary>Base NIL value; observed reset to 0 on every team change.</summary>
    public const string BaseNilValue = "BaseNILValue";

    /// <summary>Current NIL compensation; observed reset to 0 on every team change.</summary>
    public const string CurrentNilCompensation = "CurrentNILCompensation";
}
