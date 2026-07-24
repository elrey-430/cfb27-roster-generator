using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Model;

/// <summary>
/// A strongly typed view over one row of the CFB27 <c>Player</c> table.
///
/// The instance does not copy the row: reads and writes go straight to the
/// underlying <see cref="CsvDocument"/> cells, so every column that has no
/// typed property here still round-trips byte-for-byte. Typed setters are
/// provided only for the empirically confirmed Group 1 fields; team changes
/// and identity changes must go through <c>RosterEditSession</c> so their
/// multi-field side effects and intent are recorded.
/// </summary>
public sealed class Player
{
    private readonly CsvDocument _document;
    private readonly int _rowIndex;

    internal Player(CsvDocument document, int rowIndex)
    {
        _document = document;
        _rowIndex = rowIndex;
    }

    /// <summary>Zero-based index of this player's row within the CSV.</summary>
    public int RowIndex => _rowIndex;

    /// <summary>The <c>_row</c> key — the table's stable primary key.</summary>
    public int RowKey => GetInt(PlayerColumns.Row);

    /// <summary>True when this row is an unused pool slot (<c>_isEmpty</c>).</summary>
    public bool IsEmpty => string.Equals(GetRaw(PlayerColumns.IsEmpty), "true", StringComparison.Ordinal);

    /// <summary>Player first name.</summary>
    public string FirstName
    {
        get => GetRaw(PlayerColumns.FirstName);
        set => SetRaw(PlayerColumns.FirstName, value);
    }

    /// <summary>Player last name.</summary>
    public string LastName
    {
        get => GetRaw(PlayerColumns.LastName);
        set => SetRaw(PlayerColumns.LastName, value);
    }

    /// <summary>Jersey number (0–99).</summary>
    public int JerseyNumber
    {
        get => GetInt(PlayerColumns.JerseyNum);
        set => SetInt(PlayerColumns.JerseyNum, value);
    }

    /// <summary>Height in inches (raw value, no encoding).</summary>
    public int HeightInches
    {
        get => GetInt(PlayerColumns.Height);
        set => SetInt(PlayerColumns.Height, value);
    }

    /// <summary>Class standing (Freshman/Sophomore/Junior/Senior).</summary>
    public string SchoolYear
    {
        get => GetRaw(PlayerColumns.SchoolYear);
        set => SetRaw(PlayerColumns.SchoolYear, value);
    }

    /// <summary>Redshirt status (Eligible/Previous/Ineligible).</summary>
    public string RedshirtStatus
    {
        get => GetRaw(PlayerColumns.RedshirtStatus);
        set => SetRaw(PlayerColumns.RedshirtStatus, value);
    }

    /// <summary>Position abbreviation (QB, HB, WR, ...).</summary>
    public string Position
    {
        get => GetRaw(PlayerColumns.Position);
        set => SetRaw(PlayerColumns.Position, value);
    }

    /// <summary>Overall rating (0–99).</summary>
    public int OverallRating => GetInt(PlayerColumns.OverallRating);

    /// <summary>
    /// The raw <c>Weight</c> cell (stored value, not pounds). Kept for
    /// diagnostics; use <see cref="WeightPounds"/> for real-world weights.
    /// </summary>
    public string WeightRaw => GetRaw(PlayerColumns.Weight);

    /// <summary>
    /// Weight in real pounds. The save stores pounds − 160 (see Schema.md
    /// Group 2 for the confirming evidence); this property applies the
    /// offset both ways and rejects values outside the representable
    /// 160–400 lb range.
    /// </summary>
    public int WeightPounds
    {
        get => GetInt(PlayerColumns.Weight) + PlayerSchema.WeightOffsetPounds;
        set
        {
            if (value is < PlayerSchema.WeightPoundsMin or > PlayerSchema.WeightPoundsMax)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Weight {value} lb is outside the representable range " +
                    $"{PlayerSchema.WeightPoundsMin}–{PlayerSchema.WeightPoundsMax} lb.");
            }

            SetInt(PlayerColumns.Weight, value - PlayerSchema.WeightOffsetPounds);
        }
    }

    /// <summary>Current team index; 255 means no team.</summary>
    public int TeamIndex => GetInt(PlayerColumns.TeamIndex);

    /// <summary>Previous team index; 255 means no previous team.</summary>
    public int PrevTeamIndex => GetInt(PlayerColumns.PrevTeamIndex);

    /// <summary>Reads a numeric rating column by its CSV column name.</summary>
    public int GetRating(string ratingColumn)
    {
        if (!PlayerSchema.NumericRatingColumns.Contains(ratingColumn))
        {
            throw new ArgumentException($"'{ratingColumn}' is not a known numeric rating column.", nameof(ratingColumn));
        }

        return GetInt(ratingColumn);
    }

    /// <summary>Writes a numeric rating column by its CSV column name.</summary>
    public void SetRating(string ratingColumn, int value)
    {
        if (!PlayerSchema.NumericRatingColumns.Contains(ratingColumn))
        {
            throw new ArgumentException($"'{ratingColumn}' is not a known numeric rating column.", nameof(ratingColumn));
        }

        SetInt(ratingColumn, value);
    }

    /// <summary>Reads any column's raw cell value.</summary>
    public string GetRaw(string columnName) => _document.GetCell(_rowIndex, columnName);

    /// <summary>
    /// Writes any column's raw cell value. Prefer the typed setters and
    /// <c>RosterEditSession</c> operations; this escape hatch exists so
    /// callers are never blocked by the typed layer, but edits made through
    /// it are still caught by validation (e.g. undeclared identity changes).
    /// </summary>
    public void SetRaw(string columnName, string value) => _document.SetCell(_rowIndex, columnName, value);

    /// <summary>Reads a column as an integer with a descriptive error.</summary>
    public int GetInt(string columnName)
    {
        var raw = GetRaw(columnName);
        if (!int.TryParse(raw, out var value))
        {
            throw new FormatException(
                $"Player _row={GetRaw(PlayerColumns.Row)}: column '{columnName}' value '{raw}' is not an integer.");
        }

        return value;
    }

    private void SetInt(string columnName, int value) => SetRaw(columnName, value.ToString());

    /// <summary>"First Last (_row=N)" for messages and logs.</summary>
    public override string ToString() => $"{FirstName} {LastName} (_row={GetRaw(PlayerColumns.Row)})";
}
