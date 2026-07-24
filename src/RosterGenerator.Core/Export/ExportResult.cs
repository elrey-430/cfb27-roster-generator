using RosterGenerator.Core.Validation;

namespace RosterGenerator.Core.Export;

/// <summary>
/// The outcome of a successful export: the validation report (which may
/// contain warnings) and, per changed row, exactly which columns differ from
/// the loaded file — the caller's proof that nothing else was touched.
/// </summary>
/// <param name="Report">The validation report produced before writing.</param>
/// <param name="ChangedColumnsByRowKey">
/// For each edited player (<c>_row</c> key), the CSV columns whose values
/// differ from the file as loaded. Rows with no changes are absent.
/// </param>
public sealed record ExportResult(
    ValidationReport Report,
    IReadOnlyDictionary<int, IReadOnlyList<string>> ChangedColumnsByRowKey);
