using RosterGenerator.Core.Validation;

namespace RosterGenerator.Core.Export;

/// <summary>
/// Thrown when an export is blocked by validation errors. Carries the full
/// report so callers can show every issue, not just the first.
/// </summary>
public sealed class RosterExportException : Exception
{
    /// <summary>Creates the exception from the failing report.</summary>
    public RosterExportException(ValidationReport report)
        : base("Export blocked by validation errors:\n" +
               string.Join("\n", report.Errors.Select(e => "  " + e)))
    {
        Report = report;
    }

    /// <summary>The validation report that blocked the export.</summary>
    public ValidationReport Report { get; }
}
