using RosterGenerator.Core.Validation;

namespace RosterGenerator.Core.Export;

/// <summary>
/// Validates a roster and writes it back out in the CFB27 export format.
/// The write path is intentionally dumb — it serializes the raw cells the
/// document already holds — so the only cells that can differ from the input
/// file are the ones an edit deliberately changed.
/// </summary>
public sealed class RosterExporter
{
    private readonly RosterValidator _validator;

    /// <summary>Creates an exporter using the default validation rules.</summary>
    public RosterExporter()
        : this(new RosterValidator())
    {
    }

    /// <summary>Creates an exporter using a custom validator.</summary>
    public RosterExporter(RosterValidator validator)
    {
        _validator = validator;
    }

    /// <summary>
    /// Validates and writes the roster to <paramref name="outputPath"/>.
    /// </summary>
    /// <exception cref="RosterExportException">
    /// The roster has error-severity validation issues; nothing is written.
    /// </exception>
    public ExportResult Export(RosterValidationContext context, string outputPath)
    {
        var report = _validator.Validate(context);
        if (!report.IsValid)
        {
            throw new RosterExportException(report);
        }

        var changedByRowKey = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var player in context.Roster.AllRows)
        {
            var changed = context.Roster.GetChangedColumns(player.RowIndex);
            if (changed.Count > 0)
            {
                changedByRowKey[player.RowKey] = changed;
            }
        }

        context.Roster.Document.Save(outputPath);
        return new ExportResult(report, changedByRowKey);
    }
}
