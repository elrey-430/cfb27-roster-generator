namespace RosterGenerator.Core.Historical;

/// <summary>
/// One real-world player as recorded in a historical roster dataset, before
/// any conversion into CFB27 fields. Platform-independent: positions and
/// class years are free-form historical labels (e.g. "Tailback",
/// "Redshirt Junior") resolved later via the external mapping files.
/// Every field except the name and position may be missing; the converter
/// substitutes defaults and reports each one it uses.
/// </summary>
public sealed record HistoricalPlayer
{
    /// <summary>Player first name. Required.</summary>
    public required string FirstName { get; init; }

    /// <summary>Player last name. Required.</summary>
    public required string LastName { get; init; }

    /// <summary>
    /// Historical position label (e.g. "QB", "Tailback", "Defensive Tackle").
    /// Resolved to a CFB27 position via <c>PositionMappings.json</c>. Required.
    /// </summary>
    public required string Position { get; init; }

    /// <summary>Jersey number, or null if unknown.</summary>
    public int? JerseyNumber { get; init; }

    /// <summary>Listed height in inches, or null if unknown.</summary>
    public int? HeightInches { get; init; }

    /// <summary>
    /// Listed weight in pounds, or null if unknown. Carried in the dataset
    /// but NOT written to CFB27 output while the save's Weight encoding
    /// remains unresolved (see Schema.md Group 2).
    /// </summary>
    public int? WeightPounds { get; init; }

    /// <summary>
    /// Class year label (e.g. "Freshman", "Redshirt Junior", "Graduate"),
    /// or null if unknown.
    /// </summary>
    public string? ClassYear { get; init; }

    /// <summary>Hometown ("City, ST"), or null. Optional, not exported yet.</summary>
    public string? Hometown { get; init; }

    /// <summary>Previous school for transfers, or null. Optional.</summary>
    public string? PreviousSchool { get; init; }

    /// <summary>Free-form notes (data caveats, roles). Optional.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// The season <em>this player</em> is being recreated from, when the roster
    /// gives one per row, or null to use the roster's own season.
    ///
    /// <para>It exists for all-time rosters, where every player comes from a
    /// different year. The roster carries one season, which decides the report
    /// heading and anything roster-wide; before this, it also decided the
    /// equipment for everybody, so an all-time squad spanning fifty years was
    /// issued one era's helmets — whichever year happened to be typed first.
    /// A player's own season now picks their own era.</para>
    /// </summary>
    public int? Season { get; init; }

    /// <summary>
    /// EA's skin tone, 1 (lightest) to 8 (darkest), or null to leave the
    /// player's appearance alone. Optional, and blank is the normal case.
    ///
    /// <para>This is supplied by the user and never inferred. The generator
    /// will not guess what a real person looked like from their name,
    /// hometown or position — a blank cell means "keep what the roster slot
    /// already had", not "work it out".</para>
    ///
    /// <para>It takes effect by choosing the player's generated face rather
    /// than by writing the visuals table: a generated head is only ever used
    /// at one tone, so the face and the tone are a single choice.</para>
    /// </summary>
    public int? SkinTone { get; init; }

    /// <summary>
    /// Performance evidence used to generate ratings (stats, awards, draft
    /// slot, combine numbers, depth-chart role). Empty when the user
    /// supplied none — the rating engine then falls back to position and
    /// class-year defaults and reports Low confidence.
    /// </summary>
    public RatingEvidence Evidence { get; init; } = RatingEvidence.Empty;

    /// <summary>"First Last (POS #N)" for messages and reports.</summary>
    public override string ToString() =>
        $"{FirstName} {LastName} ({Position}{(JerseyNumber is null ? "" : $" #{JerseyNumber}")})";
}
