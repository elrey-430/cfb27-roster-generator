using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Depth;

/// <summary>What rebuilding one team's depth chart changed.</summary>
/// <param name="TeamIndex">The team.</param>
/// <param name="SlotsWritten">Slots whose order changed.</param>
/// <param name="EntriesWritten">Individual entries changed across those slots.</param>
/// <param name="Starters">Slot name → the player now listed first.</param>
public sealed record DepthChartTeamReport(
    int TeamIndex,
    int SlotsWritten,
    int EntriesWritten,
    IReadOnlyDictionary<string, string> Starters);

/// <summary>
/// A dynasty's depth charts, and the one thing a generated roster gets wrong
/// about them.
///
/// <para><b>The problem.</b> A depth chart points at player <em>rows</em>, in
/// the order the donor's players ranked. Recreating a roster replaces who lives
/// in each row and leaves the chart alone, so the slot the game believes is the
/// starting quarterback now holds whoever happened to land in that row — often
/// a walk-on. Nothing in the game corrects it, which is why a generated roster
/// takes the field with its depth chart scrambled.</para>
///
/// <para><b>How it is stored.</b> Three tables:</para>
/// <code>
/// Team.DepthChart  ->  a DepthChart row, one per team, 35 position slots
///                  ->  each slot points at a Player[] row
///                  ->  which holds up to six player references, in depth order
/// </code>
///
/// <para>Every cell is the same 32-bit reference the CharacterVisuals link
/// uses: the high half tags the table, the low half is the row. Rebuilding a
/// chart therefore only ever rewrites <c>Player[]</c> — the chart's own
/// slot pointers do not move.</para>
/// </summary>
public sealed class DepthChartTable
{
    private readonly CsvDocument _charts;
    private readonly CsvDocument _entries;
    private readonly IReadOnlyList<string> _entryColumns;
    private readonly IReadOnlyList<string> _slotColumns;
    private readonly Dictionary<int, int> _chartRowByTeam;

    private DepthChartTable(
        CsvDocument charts,
        CsvDocument entries,
        string entriesPath,
        IReadOnlyList<string> entryColumns,
        IReadOnlyList<string> slotColumns,
        Dictionary<int, int> chartRowByTeam)
    {
        _charts = charts;
        _entries = entries;
        _entryColumns = entryColumns;
        _slotColumns = slotColumns;
        _chartRowByTeam = chartRowByTeam;
        EntriesPath = entriesPath;
    }

    /// <summary>Where the <c>Player[]</c> table was read from.</summary>
    public string EntriesPath { get; }

    /// <summary>Teams this dynasty carries a depth chart for.</summary>
    public IReadOnlyCollection<int> Teams => _chartRowByTeam.Keys;

    /// <summary>The column holding a team's link to its chart.</summary>
    private const string TeamDepthChartColumn = "DepthChart";

    /// <summary>Never rewritten: it points at the entries a user pinned.</summary>
    private const string LockedColumn = "LockedEntries";

    /// <summary>
    /// Opens the depth-chart tables from an extracted dynasty, or returns null
    /// when the export does not carry them.
    ///
    /// <para>Null is an ordinary answer. A folder from the community export
    /// tool holds whichever tables its user asked for, and a dynasty that has
    /// no depth chart is still a dynasty worth generating a roster into.</para>
    /// </summary>
    public static DepthChartTable? Open(string exportDirectory)
    {
        // "Player[]" names about 170 tables in a save, nearly all of them a
        // single row of something unrelated. The depth chart's own is the one
        // with thousands of rows and nothing but PlayerN columns.
        var charts = Largest(exportDirectory, "_DepthChart.csv", _ => true);
        var entries = Largest(exportDirectory, "_Player[].csv",
            d => d.Header.Count <= 16 &&
                 d.Header.Where(c => !c.StartsWith('_')).All(c => c.StartsWith("Player", StringComparison.Ordinal)));
        var teams = Largest(exportDirectory, "_Team.csv", d => d.HasColumn("TeamIndex"));
        if (charts is null || entries is null || teams is null)
        {
            return null;
        }

        var (chartDocument, _) = charts.Value;
        var (entryDocument, entryPath) = entries.Value;
        var (teamDocument, _) = teams.Value;

        var entryColumns = entryDocument.Header
            .Where(c => !c.StartsWith('_'))
            .ToList();
        var slotColumns = chartDocument.Header
            .Where(c => !c.StartsWith('_') && !string.Equals(c, LockedColumn, StringComparison.Ordinal))
            .ToList();
        if (entryColumns.Count == 0 || slotColumns.Count == 0 ||
            !teamDocument.HasColumn(TeamDepthChartColumn) || !teamDocument.HasColumn("TeamIndex"))
        {
            return null;
        }

        var byTeam = new Dictionary<int, int>();
        for (var row = 0; row < teamDocument.RowCount; row++)
        {
            if (!int.TryParse(teamDocument.GetCell(row, "TeamIndex"), out var teamIndex))
            {
                continue;
            }

            if (TableReference.Row(teamDocument.GetCell(row, TeamDepthChartColumn)) is int chartRow &&
                chartRow < chartDocument.RowCount)
            {
                byTeam[teamIndex] = chartRow;
            }
        }

        return byTeam.Count == 0
            ? null
            : new DepthChartTable(
                chartDocument, entryDocument, entryPath, entryColumns, slotColumns, byTeam);
    }

    /// <summary>The biggest table of a given name — the real one, not a sentinel.</summary>
    private static (CsvDocument Document, string Path)? Largest(
        string directory, string suffix, Func<CsvDocument, bool> usable)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        (CsvDocument Document, string Path)? best = null;
        foreach (var file in Directory.EnumerateFiles(directory, "*.csv", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var document = CsvDocument.Load(file);
                if (!usable(document))
                {
                    continue;
                }

                if (best is null || document.RowCount > best.Value.Document.RowCount)
                {
                    best = (document, file);
                }
            }
            catch (Csv.CsvSchemaException)
            {
                // A table this tool does not understand is not this tool's business.
            }
        }

        return best;
    }

    /// <summary>
    /// Rebuilds one team's chart from the roster as it now stands.
    /// </summary>
    /// <param name="teamIndex">The team to rebuild.</param>
    /// <param name="roster">The generated roster.</param>
    /// <param name="model">The measured slot model.</param>
    public DepthChartTeamReport? Rebuild(int teamIndex, PlayerRoster roster, DepthChartSlotModel model)
    {
        if (!_chartRowByTeam.TryGetValue(teamIndex, out var chartRow))
        {
            return null;
        }

        var squad = roster.Players
            .Where(p => p.TeamIndex == teamIndex && !p.IsEmpty)
            .ToList();
        if (squad.Count == 0)
        {
            return null;
        }

        var slotsWritten = 0;
        var entriesWritten = 0;
        var starters = new Dictionary<string, string>(StringComparer.Ordinal);
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slotName in _slotColumns)
        {
            if (handled.Contains(slotName) || !model.ByName.TryGetValue(slotName, out var slot))
            {
                continue;
            }

            // A mirrored pair is one assignment: sort the shared pool once and
            // deal it alternately, the better player to the left slot.
            var partner = model.PartnerOf(slotName);
            if (partner is not null && model.ByName.TryGetValue(partner, out var partnerSlot) &&
                _slotColumns.Contains(partner, StringComparer.OrdinalIgnoreCase))
            {
                var left = model.IsLeftOfPair(slotName) ? slot : partnerSlot;
                var right = model.IsLeftOfPair(slotName) ? partnerSlot : slot;
                var pool = Candidates(squad, left, model);

                Write(chartRow, left.Name, Deal(pool, 0, left.Depth), ref slotsWritten, ref entriesWritten,
                    starters, model);
                Write(chartRow, right.Name, Deal(pool, 1, right.Depth), ref slotsWritten, ref entriesWritten,
                    starters, model);
                handled.Add(left.Name);
                handled.Add(right.Name);
                continue;
            }

            Write(chartRow, slot.Name, Candidates(squad, slot, model).Take(slot.Depth).ToList(),
                ref slotsWritten, ref entriesWritten, starters, model);
            handled.Add(slot.Name);
        }

        return new DepthChartTeamReport(teamIndex, slotsWritten, entriesWritten, starters);
    }

    /// <summary>Every player eligible for a slot, best first.</summary>
    private static List<Player> Candidates(
        IReadOnlyList<Player> squad, DepthChartSlot slot, DepthChartSlotModel model)
    {
        var rank = slot.From
            .Select((position, index) => (position, index))
            .ToDictionary(p => p.position, p => p.index, StringComparer.OrdinalIgnoreCase);

        return squad
            .Where(p => rank.ContainsKey(p.Position))
            .OrderByDescending(p => p.OverallRating)
            // The listed order breaks ties only. Ranking by it first would put a
            // 60-overall right end above an 85-overall left end at RE, which is
            // not what the game does.
            .ThenBy(p => rank[p.Position])
            .ThenBy(p => p.RowKey)
            .ToList();
    }

    /// <summary>Deals every other player from a shared pool.</summary>
    private static List<Player> Deal(IReadOnlyList<Player> pool, int offset, int depth)
    {
        var dealt = new List<Player>();
        for (var i = offset; i < pool.Count && dealt.Count < depth; i += 2)
        {
            dealt.Add(pool[i]);
        }

        return dealt;
    }

    private void Write(
        int chartRow,
        string slotName,
        IReadOnlyList<Player> players,
        ref int slotsWritten,
        ref int entriesWritten,
        Dictionary<string, string> starters,
        DepthChartSlotModel model)
    {
        if (players.Count == 0 ||
            TableReference.Row(_charts.GetCell(chartRow, slotName)) is not int entryRow ||
            entryRow >= _entries.RowCount)
        {
            return;
        }

        var changed = 0;
        for (var i = 0; i < _entryColumns.Count; i++)
        {
            var column = _entryColumns[i];
            var wanted = i < players.Count
                ? TableReference.Encode(model.PlayerTableTag, players[i].RowKey)
                : TableReference.Empty;
            if (!string.Equals(_entries.GetCell(entryRow, column), wanted, StringComparison.Ordinal))
            {
                _entries.SetCell(entryRow, column, wanted);
                changed++;
            }
        }

        starters[slotName] = $"{players[0].FirstName} {players[0].LastName} " +
                             $"({players[0].Position} {players[0].OverallRating})";
        if (changed > 0)
        {
            slotsWritten++;
            entriesWritten += changed;
        }
    }

    /// <summary>
    /// Who a team lists at each slot, in depth order, as player row keys.
    /// Null when the dynasty has no chart for that team.
    ///
    /// <para>Read-only, and the other direction from <see cref="Rebuild"/> —
    /// this is how an exported roster file learns which players actually
    /// start.</para>
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>>? Listing(int teamIndex)
    {
        if (!_chartRowByTeam.TryGetValue(teamIndex, out var chartRow))
        {
            return null;
        }

        var listing = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        foreach (var slotName in _slotColumns)
        {
            if (TableReference.Row(_charts.GetCell(chartRow, slotName)) is not int entryRow ||
                entryRow >= _entries.RowCount)
            {
                continue;
            }

            var ordered = new List<int>();
            foreach (var column in _entryColumns)
            {
                if (TableReference.Decode(_entries.GetCell(entryRow, column)) is { } reference)
                {
                    ordered.Add(reference.Row);
                }
            }

            if (ordered.Count > 0)
            {
                listing[slotName] = ordered;
            }
        }

        return listing;
    }

    /// <summary>Writes the rebuilt <c>Player[]</c> table.</summary>
    public void Save(string path) => _entries.Save(path);
}
