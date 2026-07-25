namespace RosterGenerator.Core.Equipment;

/// <summary>
/// Decodes the Player table's <c>CharacterVisuals</c> column, which points a
/// player at their row in the CharacterVisuals table.
///
/// <para>The cell is a 32-character string of ASCII '0' and '1' — the save
/// exporter's spelling of a 32-bit value — such as
/// <c>00100001000001000000000000000000</c>. The low 16 bits are the row id in
/// the CharacterVisuals table; the high 16 bits are a constant tag identifying
/// which table is being pointed at.</para>
///
/// <para>Confirmed by taking the eight CharacterVisuals rows that differed
/// between two exports and decoding this column for all 16,500 players: the
/// eight rows resolved to exactly the eight Florida State cornerbacks whose
/// helmets had been changed, with no other matches.</para>
/// </summary>
public static class CharacterVisualsReference
{
    /// <summary>
    /// The high-half tag observed on every player reference into the
    /// CharacterVisuals table. A different tag means the cell points somewhere
    /// else and its low half is not a visuals row.
    /// </summary>
    public const int VisualsTableTag = 8452;

    /// <summary>
    /// Reads the CharacterVisuals row id from a player's raw reference cell,
    /// or null when the cell is blank, malformed, or tagged for another table.
    /// </summary>
    public static int? RowId(string? rawReference)
    {
        if (string.IsNullOrEmpty(rawReference) || rawReference.Length > 32)
        {
            return null;
        }

        var value = 0u;
        foreach (var c in rawReference)
        {
            if (c is not ('0' or '1'))
            {
                return null;
            }

            value = (value << 1) | (uint)(c - '0');
        }

        return (value >> 16) == VisualsTableTag ? (int)(value & 0xFFFF) : null;
    }
}
