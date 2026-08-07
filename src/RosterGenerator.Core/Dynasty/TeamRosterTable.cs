using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Depth;

namespace RosterGenerator.Core.Dynasty;

/// <summary>
/// Who is actually on a team, read from the team's own side of the link.
///
/// <para>A player carries a <c>TeamIndex</c>, and for the 136 FBS schools that
/// is the whole story. It breaks down for the five generic FCS teams the game
/// ships — <c>FCS East</c>, <c>FCS Midwest</c>, <c>FCS Northwest</c>,
/// <c>FCS Southeast</c>, <c>FCS West</c> — because all five carry
/// <c>TeamIndex</c> 255, and so do the 4,527 players in the recruiting pool.
/// Asking the player table for "team 255" returns all of them at once, which
/// is why a school that has left the FBS could not be recreated: there was no
/// way to name the eighty-five slots it should replace.</para>
///
/// <para>The teams themselves know. Every team row, FBS and FCS alike, has a
/// <c>Roster</c> reference into one shared table whose rows hold exactly 85
/// player references — the same 32-bit encoding and the same player tag the
/// depth chart uses. Following it names the right eighty-five players however
/// ambiguous their <c>TeamIndex</c> is.</para>
/// </summary>
public sealed class TeamRosterTable
{
    /// <summary>The Team column holding the roster reference.</summary>
    public const string TeamRosterColumn = "Roster";

    private readonly Dictionary<string, IReadOnlyList<int>> _byTeamName;

    private TeamRosterTable(Dictionary<string, IReadOnlyList<int>> byTeamName) =>
        _byTeamName = byTeamName;

    /// <summary>Every team name the table could resolve.</summary>
    public IReadOnlyCollection<string> TeamNames => _byTeamName.Keys;

    /// <summary>
    /// The player rows belonging to a team, by the name the save displays for
    /// it, or null when this dynasty does not carry that team.
    /// </summary>
    public IReadOnlyList<int>? MembersOf(string teamName) =>
        _byTeamName.TryGetValue(Normalize(teamName), out var rows) ? rows : null;

    /// <summary>
    /// Opens the roster table from an exported dynasty, or returns null when
    /// the dynasty does not carry one — a folder from the community export
    /// tool often holds only the few tables the generator used to need.
    /// </summary>
    public static TeamRosterTable? Open(string exportDirectory)
    {
        var teams = Largest(exportDirectory, "_Team.csv",
            d => d.HasColumn(TeamRosterColumn) && d.HasColumn("DisplayName"));

        // "Player[]" names about 170 tables in a save. The roster's own is the
        // one whose columns are all PlayerN and whose row count is the number
        // of teams -- the depth chart's entry lists are the same shape but far
        // narrower and far more numerous.
        var rosters = Largest(exportDirectory, "_Player[].csv",
            d => d.Header.Count(c => !c.StartsWith('_')) > 16 &&
                 d.Header.Where(c => !c.StartsWith('_'))
                     .All(c => c.StartsWith("Player", StringComparison.Ordinal)));
        if (teams is null || rosters is null)
        {
            return null;
        }

        var teamDocument = teams.Value.Document;
        var rosterDocument = rosters.Value.Document;
        var playerColumns = rosterDocument.Header.Where(c => !c.StartsWith('_')).ToList();

        var byTeamName = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        for (var row = 0; row < teamDocument.RowCount; row++)
        {
            var name = teamDocument.GetCell(row, "DisplayName");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (TableReference.Row(teamDocument.GetCell(row, TeamRosterColumn)) is not int rosterRow ||
                rosterRow >= rosterDocument.RowCount)
            {
                continue;
            }

            var members = new List<int>();
            foreach (var column in playerColumns)
            {
                if (TableReference.Row(rosterDocument.GetCell(rosterRow, column)) is int playerRow)
                {
                    members.Add(playerRow);
                }
            }

            if (members.Count > 0)
            {
                byTeamName[Normalize(name)] = members;
            }
        }

        return byTeamName.Count > 0 ? new TeamRosterTable(byTeamName) : null;
    }

    private static (CsvDocument Document, string Path)? Largest(
        string directory, string suffix, Func<CsvDocument, bool> accept)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        (CsvDocument Document, string Path)? best = null;
        foreach (var path in Directory.EnumerateFiles(directory, "*" + suffix))
        {
            CsvDocument document;
            try
            {
                document = CsvDocument.Parse(File.ReadAllText(path));
            }
            catch (CsvSchemaException)
            {
                continue;
            }

            if (accept(document) && (best is null || document.RowCount > best.Value.Document.RowCount))
            {
                best = (document, path);
            }
        }

        return best;
    }

    private static string Normalize(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
