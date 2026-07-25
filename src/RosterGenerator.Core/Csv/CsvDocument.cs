namespace RosterGenerator.Core.Csv;

/// <summary>
/// An in-memory CSV table that preserves the source file's exact cell values.
///
/// The CFB27 save exporter writes plain ASCII CSV with CRLF line endings, no
/// BOM and no quoted fields. Because only a small fraction of the ~286 player
/// columns are understood, the highest-priority guarantee of this layer is
/// <b>round-trip fidelity</b>: a document that is loaded and written back
/// without edits must be byte-identical, and an edited document must differ
/// only in the cells that were deliberately changed. All typed access is
/// layered on top of the raw string cells kept here.
/// </summary>
public sealed class CsvDocument
{
    private readonly List<string> _header;
    private readonly List<string[]> _rows;
    private readonly Dictionary<string, int> _columnIndex;

    private CsvDocument(List<string> header, List<string[]> rows)
    {
        _header = header;
        _rows = rows;
        _columnIndex = new Dictionary<string, int>(header.Count, StringComparer.Ordinal);
        for (var i = 0; i < header.Count; i++)
        {
            // First occurrence wins; the Player table has no duplicate names.
            _columnIndex.TryAdd(header[i], i);
        }
    }

    /// <summary>Column names in file order.</summary>
    public IReadOnlyList<string> Header => _header;

    /// <summary>Number of data rows (excluding the header).</summary>
    public int RowCount => _rows.Count;

    /// <summary>Returns true if the document contains the named column.</summary>
    public bool HasColumn(string columnName) => _columnIndex.ContainsKey(columnName);

    /// <summary>
    /// Resolves a column name to its index, throwing a descriptive error when
    /// the column is missing (e.g. the file is not a Player table export).
    /// </summary>
    public int GetColumnIndex(string columnName)
    {
        if (!_columnIndex.TryGetValue(columnName, out var index))
        {
            throw new CsvSchemaException($"Column '{columnName}' was not found in the CSV header.");
        }

        return index;
    }

    /// <summary>Gets the raw string cell at (row, column name).</summary>
    public string GetCell(int rowIndex, string columnName) => _rows[rowIndex][GetColumnIndex(columnName)];

    /// <summary>Sets the raw string cell at (row, column name).</summary>
    public void SetCell(int rowIndex, string columnName, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _rows[rowIndex][GetColumnIndex(columnName)] = value;
    }

    /// <summary>Returns a defensive copy of a row's cells, in header order.</summary>
    public string[] CopyRow(int rowIndex) => (string[])_rows[rowIndex].Clone();

    /// <summary>
    /// Parses CSV text. Accepts RFC 4180-style quoted fields for robustness,
    /// although CFB27 exports observed so far never quote.
    /// </summary>
    public static CsvDocument Parse(string text) => Parse(text, RaggedRows.Reject);

    /// <summary>What to do with a row whose field count differs from the header's.</summary>
    public enum RaggedRows
    {
        /// <summary>
        /// Throw. Correct for the game's own table exports, where a row of the
        /// wrong width means the file is truncated or is not the table it
        /// claims to be.
        /// </summary>
        Reject,

        /// <summary>
        /// Pad short rows with empty cells and ignore cells past the header.
        /// Correct for a roster CSV a person typed: omitting the trailing
        /// columns on a row is ordinary in spreadsheets and hand-edited files,
        /// and it means "I have nothing for these", not "this file is corrupt".
        /// </summary>
        Pad,
    }

    /// <summary>Parses CSV text, choosing how strict to be about row width.</summary>
    public static CsvDocument Parse(string text, RaggedRows ragged)
    {
        var records = CsvFormat.ParseRecords(text);
        if (records.Count == 0)
        {
            throw new CsvSchemaException("The CSV file is empty.");
        }

        var header = records[0].ToList();
        var rows = new List<string[]>(records.Count - 1);
        for (var i = 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Length != header.Count)
            {
                if (ragged == RaggedRows.Reject)
                {
                    throw new CsvSchemaException(
                        $"Row {i} has {record.Length} fields but the header has {header.Count}. " +
                        "The file may be truncated or not a CFB27 table export.");
                }

                var padded = new string[header.Count];
                for (var column = 0; column < header.Count; column++)
                {
                    padded[column] = column < record.Length ? record[column] : "";
                }

                record = padded;
            }

            rows.Add(record);
        }

        return new CsvDocument(header, rows);
    }

    /// <summary>Loads a CSV document from disk.</summary>
    public static CsvDocument Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>
    /// Serializes the document using the CFB27 export conventions: CRLF line
    /// endings, a trailing newline after the last row, and unquoted fields
    /// (fields are only quoted if they contain a delimiter, quote or newline,
    /// which no observed CFB27 value does).
    /// </summary>
    public string ToCsvText()
    {
        var builder = new System.Text.StringBuilder();
        CsvFormat.WriteRecord(builder, _header);
        foreach (var row in _rows)
        {
            CsvFormat.WriteRecord(builder, row);
        }

        return builder.ToString();
    }

    /// <summary>Writes the document to disk using <see cref="ToCsvText"/>.</summary>
    public void Save(string path) => File.WriteAllText(path, ToCsvText());
}
