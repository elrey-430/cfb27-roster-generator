using System.Text.RegularExpressions;

using RosterGenerator.Core.Csv;

namespace RosterGenerator.Core.Equipment;

/// <summary>What a player is wearing on their head.</summary>
/// <param name="Helmet">Helmet asset name, e.g. <c>GearHelmet_RevolutionSpeed</c>.</param>
/// <param name="FaceMask">Face mask asset name, e.g. <c>GearFaceMask_revospeed2bar</c>.</param>
public readonly record struct HeadGear(string Helmet, string FaceMask)
{
    /// <summary>"RevolutionSpeed + revospeed2bar", for reports.</summary>
    public override string ToString() =>
        $"{Helmet.Replace("GearHelmet_", "")} + {FaceMask.Replace("GearFaceMask_", "")}";
}

/// <summary>
/// The save's <c>CharacterVisuals</c> table — one row per character, each
/// holding a JSON blob describing everything they wear.
///
/// <para>Equipment lives here, <b>not</b> in the Player table. Diffing two
/// exports of the same dynasty that differ only in seven players' helmets
/// showed exactly one changed file out of 2,273, and it was this one.</para>
///
/// <para>Each blob carries three loadouts; the kit is in the one typed
/// <c>PlayerOnField</c>, which holds 32 slots covering the whole uniform.
/// The helmet is the element with <c>slotType: HeadWear</c> and the face mask
/// the one with <c>slotType: FaceMask</c>.</para>
///
/// <para><b>Edits are surgical string replacements, never a re-serialization.</b>
/// The blob is 479–3,367 characters of JSON whose meaning is mostly unknown,
/// so rewriting it would risk changing far more than was asked. Both
/// <c>GearHelmet_</c> and <c>GearFaceMask_</c> occur exactly once in every
/// populated row — verified across all 12,156 rows that carry a helmet — so
/// replacing those two values in place is unambiguous and leaves every other
/// byte untouched.</para>
/// </summary>
public sealed class CharacterVisualsTable
{
    /// <summary>The table's <c>_tableName</c> value.</summary>
    public const string TableName = "CharacterVisuals";

    private const string RawDataColumn = "RawData";
    private const string RowColumn = "_row";
    private const string IsEmptyColumn = "_isEmpty";

    // The single helmet and face mask assignments inside the blob. Anchored on
    // the "itemAssetName" key so a name appearing in some other field (an
    // instance tag, say) can never be mistaken for the assignment itself.
    private static readonly Regex HelmetPattern =
        new("\"itemAssetName\":\"(GearHelmet_[^\"]*)\"", RegexOptions.Compiled);

    private static readonly Regex FaceMaskPattern =
        new("\"itemAssetName\":\"(GearFaceMask_[^\"]*)\"", RegexOptions.Compiled);

    private readonly CsvDocument _document;
    private readonly Dictionary<int, int> _rowIndexByRowId;

    private CharacterVisualsTable(CsvDocument document, Dictionary<int, int> rowIndexByRowId)
    {
        _document = document;
        _rowIndexByRowId = rowIndexByRowId;
    }

    /// <summary>The underlying document, for writing the table back out.</summary>
    public CsvDocument Document => _document;

    /// <summary>Number of rows in the table, populated or not.</summary>
    public int RowCount => _document.RowCount;

    /// <summary>Loads a CharacterVisuals table from disk.</summary>
    public static CharacterVisualsTable Load(string path)
    {
        var document = CsvDocument.Parse(File.ReadAllText(path));
        if (!document.HasColumn(RawDataColumn) || !document.HasColumn(RowColumn))
        {
            throw new CsvSchemaException(
                $"'{path}' is not a CharacterVisuals table: it has no '{RawDataColumn}' column.");
        }

        // Indexed by the table's own _row value rather than by position, so a
        // trimmed export — or one the exporter wrote out of order — still
        // resolves the references the Player table holds.
        var byRowId = new Dictionary<int, int>(document.RowCount);
        for (var i = 0; i < document.RowCount; i++)
        {
            if (int.TryParse(document.GetCell(i, RowColumn), out var rowId))
            {
                byRowId.TryAdd(rowId, i);
            }
        }

        return new CharacterVisualsTable(document, byRowId);
    }

    /// <summary>True when the table carries a row with this id.</summary>
    public bool HasRow(int rowId) => _rowIndexByRowId.ContainsKey(rowId);

    /// <summary>
    /// Reads what the character in <paramref name="rowId"/> is wearing on
    /// their head, or null when the row is empty or carries no helmet — 430
    /// populated rows in a real export have neither, and they are left alone.
    /// </summary>
    public HeadGear? GetHeadGear(int rowId)
    {
        if (!_rowIndexByRowId.TryGetValue(rowId, out var index))
        {
            return null;
        }

        var raw = _document.GetCell(index, RawDataColumn);
        var helmet = HelmetPattern.Match(raw);
        var faceMask = FaceMaskPattern.Match(raw);
        return helmet.Success && faceMask.Success
            ? new HeadGear(helmet.Groups[1].Value, faceMask.Groups[1].Value)
            : null;
    }

    /// <summary>
    /// Replaces the helmet and face mask worn by the character in
    /// <paramref name="rowId"/>, leaving every other byte of the blob as it
    /// was. Returns false when the row has no head gear to replace.
    /// </summary>
    /// <remarks>
    /// The two are written together on purpose. Masks are moulded to a
    /// particular shell — the face mask changed alongside the helmet in every
    /// one of the eight demonstrated edits — so writing a helmet on its own
    /// would leave a mask that does not fit it.
    /// </remarks>
    public bool SetHeadGear(int rowId, HeadGear gear)
    {
        if (!_rowIndexByRowId.TryGetValue(rowId, out var index))
        {
            return false;
        }

        var raw = _document.GetCell(index, RawDataColumn);
        if (!HelmetPattern.IsMatch(raw) || !FaceMaskPattern.IsMatch(raw))
        {
            return false;
        }

        var updated = HelmetPattern.Replace(raw, $"\"itemAssetName\":\"{gear.Helmet}\"", 1);
        updated = FaceMaskPattern.Replace(updated, $"\"itemAssetName\":\"{gear.FaceMask}\"", 1);

        _document.SetCell(index, RawDataColumn, updated);
        return true;
    }

    /// <summary>Writes the table back out.</summary>
    public void Save(string path) => _document.Save(path);

    /// <summary>True when the row exists and is marked empty.</summary>
    public bool IsEmpty(int rowId) =>
        _rowIndexByRowId.TryGetValue(rowId, out var index)
        && _document.HasColumn(IsEmptyColumn)
        && _document.GetCell(index, IsEmptyColumn) == "true";
}
