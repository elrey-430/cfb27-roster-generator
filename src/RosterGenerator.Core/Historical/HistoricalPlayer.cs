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

    /// <summary>"First Last (POS #N)" for messages and reports.</summary>
    public override string ToString() =>
        $"{FirstName} {LastName} ({Position}{(JerseyNumber is null ? "" : $" #{JerseyNumber}")})";
}
