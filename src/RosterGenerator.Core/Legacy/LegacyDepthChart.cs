using RosterGenerator.Core.Depth;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Legacy;

/// <summary>
/// The dynasty's own depth chart, in the shape
/// <see cref="LegacyRosterExporter"/> asks for.
///
/// <para>A PS2 squad holds around 69 players against CFB27's 85, so a sixth of
/// every roster has to be cut, and the only question worth arguing about is
/// which sixth. Overall is the obvious answer and the wrong one: a coach starts
/// a 78-overall junior over an 81-overall freshman often enough that ranking on
/// the number alone quietly rewrites who plays. The chart is the coach's own
/// answer, and it is sitting in the save.</para>
///
/// <para>Not every dynasty carries one — an export made from a partial set of
/// tables may have no chart at all — so this returns null rather than
/// inventing one, and the exporter falls back to overall and says so.</para>
/// </summary>
public static class LegacyDepthChart
{
    /// <summary>
    /// A lookup from CFB27 team index to that team's chart, or null when the
    /// dynasty carries no depth chart.
    /// </summary>
    /// <param name="export">The opened dynasty.</param>
    public static Func<int, IReadOnlyDictionary<string, IReadOnlyList<int>>?>? For(DynastyExport export)
    {
        DepthChartTable? charts;
        try
        {
            charts = DepthChartTable.Open(Path.GetDirectoryName(export.PlayerTablePath) ?? ".");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or
                                       Csv.CsvSchemaException or FormatException)
        {
            // A chart that will not parse is a reason to fall back to overall,
            // never a reason to refuse somebody's whole export.
            return null;
        }

        return charts is null ? null : teamIndex => Positions(charts.Listing(teamIndex));
    }

    /// <summary>
    /// The chart's real-position slots only.
    ///
    /// <para>A dynasty chart also carries slots that are jobs rather than
    /// positions — kick returner, third-down back, long snapper — and those
    /// list players whose position is something else. Keeping them would put a
    /// wide receiver at the front of the running backs because he returns
    /// kicks. Only slots named after one of the game's 21 positions are
    /// kept.</para>
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<int>>? Positions(
        IReadOnlyDictionary<string, IReadOnlyList<int>>? listing)
    {
        if (listing is null)
        {
            return null;
        }

        var kept = listing
            .Where(slot => PlayerSchema.Positions.Contains(slot.Key))
            .ToDictionary(slot => slot.Key, slot => slot.Value, StringComparer.Ordinal);
        return kept.Count > 0 ? kept : null;
    }
}
