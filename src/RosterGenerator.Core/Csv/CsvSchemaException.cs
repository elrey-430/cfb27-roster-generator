namespace RosterGenerator.Core.Csv;

/// <summary>
/// Raised when a CSV file cannot be parsed or does not have the shape the
/// caller requires (missing columns, ragged rows, empty file).
/// </summary>
public sealed class CsvSchemaException : Exception
{
    /// <summary>Creates the exception with a user-facing message.</summary>
    public CsvSchemaException(string message)
        : base(message)
    {
    }
}
