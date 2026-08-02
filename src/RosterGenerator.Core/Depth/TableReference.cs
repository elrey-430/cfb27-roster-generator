namespace RosterGenerator.Core.Depth;

/// <summary>
/// The save's way of pointing one table at another: a 32-character string of
/// ASCII '0' and '1', whose high half tags the table being pointed at and whose
/// low half is the row in it.
///
/// <para>The same encoding
/// <see cref="Equipment.CharacterVisualsReference"/> reads for a player's
/// visuals. It is spelled out here rather than shared because that class is
/// about one specific link and this is the general form — a depth chart uses it
/// twice over, team to chart and slot to entry list.</para>
/// </summary>
public static class TableReference
{
    /// <summary>How wide a reference cell is.</summary>
    public const int Width = 32;

    /// <summary>A cell pointing at nothing.</summary>
    public static readonly string Empty = new('0', Width);

    /// <summary>The row a cell points at, or null when it points nowhere.</summary>
    public static int? Row(string? cell) => Decode(cell)?.Row;

    /// <summary>The table tag and row a cell holds, or null when it is not a reference.</summary>
    public static (int Tag, int Row)? Decode(string? cell)
    {
        if (string.IsNullOrEmpty(cell) || cell.Length > Width)
        {
            return null;
        }

        var value = 0u;
        foreach (var character in cell)
        {
            if (character is not ('0' or '1'))
            {
                return null;
            }

            value = (value << 1) | (uint)(character - '0');
        }

        var tag = (int)(value >> 16);
        return tag == 0 ? null : (tag, (int)(value & 0xFFFF));
    }

    /// <summary>Builds a reference cell.</summary>
    public static string Encode(int tag, int row)
    {
        if (tag is < 0 or > 0xFFFF || row is < 0 or > 0xFFFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(row), $"A reference holds two 16-bit halves; got tag {tag}, row {row}.");
        }

        return Convert.ToString(((long)tag << 16) | (uint)row, 2).PadLeft(Width, '0');
    }
}
