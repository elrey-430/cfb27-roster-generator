using System.Text.Json;

namespace RosterGenerator.Core.Legacy;

/// <summary>
/// What a PS2-era roster's five-bit ratings mean on the 0-99 scale the game
/// shows the player.
///
/// <para>That generation holds every rating column — overall, speed, strength,
/// awareness and the rest — in five bits, 0 to 31, and expands them through one
/// shared table on the way to the screen. Reading a roster never needed this,
/// because what crosses over from a PS2 file is the ORDER; writing one does,
/// because a CFB27 rating is on the 0-99 scale and has to be put back into
/// five bits.</para>
///
/// <para>Measured from in-game screenshots rather than assumed: see
/// <c>data/LegacyRatingScale.json</c> for how, and for the one reading that
/// disagrees.</para>
/// </summary>
public sealed class LegacyRatingScale
{
    private readonly int[] _displayed;

    private LegacyRatingScale(int[] displayed)
    {
        _displayed = displayed;
    }

    /// <summary>How many stored values the scale covers (32, for five bits).</summary>
    public int Steps => _displayed.Length;

    /// <summary>The 0-99 rating the game shows for a stored value.</summary>
    public int ToDisplayed(int stored) =>
        _displayed[Math.Clamp(stored, 0, _displayed.Length - 1)];

    /// <summary>
    /// The stored value whose displayed rating is nearest to a 0-99 one.
    ///
    /// <para>Nearest, not truncated: the scale is coarse at the bottom — four
    /// display points to a step — and rounding down there would cost a player
    /// most of a step for nothing. Ties go to the lower stored value, so a
    /// rating never rounds a player up into a band he was not in.</para>
    /// </summary>
    public int ToStored(double displayed)
    {
        var best = 0;
        var bestGap = double.MaxValue;
        for (var stored = 0; stored < _displayed.Length; stored++)
        {
            var gap = Math.Abs(_displayed[stored] - displayed);
            if (gap < bestGap)
            {
                bestGap = gap;
                best = stored;
            }
        }

        return best;
    }

    /// <summary>Loads the measured scale.</summary>
    public static LegacyRatingScale Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("displayedByStored", out var values))
        {
            throw new InvalidDataException($"'{path}' has no 'displayedByStored' array.");
        }

        var displayed = values.EnumerateArray().Select(v => v.GetInt32()).ToArray();
        if (displayed.Length != 32)
        {
            throw new InvalidDataException(
                $"'{path}' lists {displayed.Length} value(s); a five-bit rating has 32.");
        }

        return new LegacyRatingScale(displayed);
    }
}
