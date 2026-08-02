using System.Text.Json;

namespace RosterGenerator.Core.Depth;

/// <summary>One depth-chart slot: how deep it runs and who may fill it.</summary>
/// <param name="Name">The slot's name in the save, e.g. <c>SLCB</c>.</param>
/// <param name="Depth">How many players the game lists there.</param>
/// <param name="From">Positions it draws on, most-used first.</param>
public sealed record DepthChartSlot(string Name, int Depth, IReadOnlyList<string> From);

/// <summary>
/// How the game fills its own depth charts, measured from a base save by
/// <c>tools/measure_depth_charts.py</c>.
///
/// <para>None of this is guessable. The specialist slots are not positions at
/// all — <c>GAD</c> is 59% halfbacks and 40% receivers, <c>LS</c> is 78% tight
/// ends, <c>SLCB</c> draws on corners and both safeties — and the depth is not
/// uniform either: six at receiver, five at corner, four at halfback, three
/// nearly everywhere else.</para>
///
/// <para><b>Mirrored pairs are one assignment, not two picks.</b> The same
/// player never heads both <c>LT</c> and <c>RT</c> — zero of 143 teams, and the
/// same for the guards, the ends and the outside linebackers — and the better
/// of the two goes to the left slot 87% to 92% of the time. So the eligible
/// pool is sorted once and dealt alternately, left first.</para>
/// </summary>
public sealed class DepthChartSlotModel
{
    private DepthChartSlotModel(
        int playerTableTag,
        IReadOnlyList<DepthChartSlot> slots,
        IReadOnlyList<(string Left, string Right)> mirrored)
    {
        PlayerTableTag = playerTableTag;
        Slots = slots;
        Mirrored = mirrored;
        ByName = slots.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The reference tag that marks a cell as pointing at the Player table.</summary>
    public int PlayerTableTag { get; }

    /// <summary>Every slot the model knows.</summary>
    public IReadOnlyList<DepthChartSlot> Slots { get; }

    /// <summary>Slot pairs filled as a single left-first assignment.</summary>
    public IReadOnlyList<(string Left, string Right)> Mirrored { get; }

    /// <summary>Slots by name.</summary>
    public IReadOnlyDictionary<string, DepthChartSlot> ByName { get; }

    /// <summary>The partner of a mirrored slot, or null when it stands alone.</summary>
    public string? PartnerOf(string slot)
    {
        foreach (var (left, right) in Mirrored)
        {
            if (string.Equals(left, slot, StringComparison.OrdinalIgnoreCase))
            {
                return right;
            }

            if (string.Equals(right, slot, StringComparison.OrdinalIgnoreCase))
            {
                return left;
            }
        }

        return null;
    }

    /// <summary>True when this slot is the left half of a mirrored pair.</summary>
    public bool IsLeftOfPair(string slot) =>
        Mirrored.Any(m => string.Equals(m.Left, slot, StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads <c>data/DepthChartSlots.json</c>.</summary>
    public static DepthChartSlotModel Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var slots = new List<DepthChartSlot>();
        foreach (var slot in root.GetProperty("slots").EnumerateObject())
        {
            slots.Add(new DepthChartSlot(
                slot.Name,
                slot.Value.GetProperty("depth").GetInt32(),
                slot.Value.GetProperty("from").EnumerateArray().Select(p => p.GetString()!).ToList()));
        }

        var mirrored = new List<(string, string)>();
        if (root.TryGetProperty("mirrored", out var pairs))
        {
            foreach (var pair in pairs.EnumerateArray())
            {
                var both = pair.EnumerateArray().Select(p => p.GetString()!).ToList();
                if (both.Count == 2)
                {
                    mirrored.Add((both[0], both[1]));
                }
            }
        }

        return new DepthChartSlotModel(root.GetProperty("playerTableTag").GetInt32(), slots, mirrored);
    }
}
