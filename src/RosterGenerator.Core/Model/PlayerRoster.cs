using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Model;

/// <summary>
/// The loaded CFB27 <c>Player</c> table: raw CSV document, typed player views,
/// and a snapshot of every cell as it was at load time. The snapshot is what
/// lets validation reason about <i>changes</i> (team moved, name changed)
/// rather than just final values, and lets the exporter report exactly which
/// cells an edit session touched.
/// </summary>
public sealed class PlayerRoster
{
    private readonly List<Player> _players;
    private readonly List<string[]> _originalRows;

    private PlayerRoster(CsvDocument document)
    {
        Document = document;

        foreach (var column in PlayerSchema.RequiredColumns)
        {
            if (!document.HasColumn(column))
            {
                throw new CsvSchemaException(
                    $"Required column '{column}' is missing — this file does not look like a CFB27 Player table export.");
            }
        }

        _players = new List<Player>(document.RowCount);
        _originalRows = new List<string[]>(document.RowCount);
        for (var i = 0; i < document.RowCount; i++)
        {
            _players.Add(new Player(document, i));
            _originalRows.Add(document.CopyRow(i));
        }
    }

    /// <summary>The underlying raw CSV document.</summary>
    public CsvDocument Document { get; }

    /// <summary>All rows, including empty pool slots, in file order.</summary>
    public IReadOnlyList<Player> AllRows => _players;

    /// <summary>Only real player records (<c>_isEmpty == false</c>).</summary>
    public IEnumerable<Player> Players => _players.Where(p => !p.IsEmpty);

    /// <summary>Finds a player by the stable <c>_row</c> key, or null.</summary>
    public Player? FindByRowKey(int rowKey) =>
        _players.FirstOrDefault(p => p.GetRaw(PlayerColumns.Row) == rowKey.ToString());

    /// <summary>The cell values a row had when the roster was loaded.</summary>
    public IReadOnlyList<string> GetOriginalRow(int rowIndex) => _originalRows[rowIndex];

    /// <summary>The value one cell had when the roster was loaded.</summary>
    public string GetOriginalValue(int rowIndex, string columnName) =>
        _originalRows[rowIndex][Document.GetColumnIndex(columnName)];

    /// <summary>Column names of every cell in a row that differs from load time.</summary>
    public IReadOnlyList<string> GetChangedColumns(int rowIndex)
    {
        var changed = new List<string>();
        var original = _originalRows[rowIndex];
        for (var c = 0; c < Document.Header.Count; c++)
        {
            if (!string.Equals(original[c], Document.GetCell(rowIndex, Document.Header[c]), StringComparison.Ordinal))
            {
                changed.Add(Document.Header[c]);
            }
        }

        return changed;
    }

    /// <summary>Loads a Player table CSV from disk.</summary>
    public static PlayerRoster Load(string path) => new(CsvDocument.Load(path));

    /// <summary>Parses a Player table CSV from text (used by tests).</summary>
    public static PlayerRoster Parse(string csvText) => new(CsvDocument.Parse(csvText));
}
