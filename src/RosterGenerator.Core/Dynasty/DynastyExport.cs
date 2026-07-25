using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Dynasty;

/// <summary>One team discovered inside a dynasty export.</summary>
/// <param name="TeamIndex">The save's team index.</param>
/// <param name="DisplayName">Full display name (e.g. "Florida State").</param>
/// <param name="ShortName">Short code (e.g. "FSU"), may be empty.</param>
/// <param name="OriginalId">
/// The school's <c>TEAM_ORIGID</c>, the id <c>PLYR_PREVTEAMID</c> uses to
/// name a transfer's previous school. 0 when the export has no such column.
/// </param>
public sealed record DynastyTeam(int TeamIndex, string DisplayName, string ShortName, int OriginalId = 0)
{
    /// <summary>"Florida State (FSU) — team 27" for listings.</summary>
    public override string ToString() =>
        $"{DisplayName}{(ShortName.Length > 0 ? $" ({ShortName})" : "")} — team {TeamIndex}";
}

/// <summary>
/// A user-provided dynasty export: the folder of per-table CSVs produced by
/// the community save-export tool. The export self-describes via the
/// <c>_tableName</c> bookkeeping column, so this class works with ANY
/// compatible dynasty file — nothing is keyed to a particular save:
/// the Player table and the main Team table are discovered by content, and
/// the list of available teams (names and ids) comes from the user's own
/// save rather than from anything shipped with the application.
/// </summary>
public sealed class DynastyExport
{
    private DynastyExport(string playerTablePath, IReadOnlyList<DynastyTeam> teams)
    {
        PlayerTablePath = playerTablePath;
        Teams = teams;
    }

    /// <summary>Path of the discovered Player table CSV.</summary>
    public string PlayerTablePath { get; }

    /// <summary>
    /// Teams discovered in the export's Team table, sorted by display name.
    /// Empty when no Team table was found (e.g. the caller pointed directly
    /// at a lone Player.csv).
    /// </summary>
    public IReadOnlyList<DynastyTeam> Teams { get; }

    /// <summary>Loads the discovered Player table as a roster.</summary>
    public PlayerRoster LoadPlayerRoster() => PlayerRoster.Load(PlayerTablePath);

    /// <summary>
    /// Builds the school-name lookup from this dynasty's own teams, merged
    /// with an optional alias overlay file (extra names like "Florida State
    /// University" mapping onto teams that exist in the save).
    /// </summary>
    public TeamMappingSet BuildTeamMappings(string? aliasOverlayPath = null)
    {
        var entries = Teams
            .Select(t => (TeamId: t.TeamIndex, Names: (IReadOnlyList<string>)new[] { t.DisplayName, t.ShortName }
                .Where(n => n.Length > 0)
                .ToList()))
            .ToList();

        if (aliasOverlayPath is not null && File.Exists(aliasOverlayPath))
        {
            var knownIds = Teams.Select(t => t.TeamIndex).ToHashSet();
            // Only overlay aliases for teams that exist in THIS dynasty, so
            // a stale mapping file can never introduce a phantom team.
            entries.AddRange(TeamMappingSet.LoadEntries(aliasOverlayPath)
                .Where(e => knownIds.Count == 0 || knownIds.Contains(e.TeamId)));
        }

        return TeamMappingSet.Build(entries);
    }

    /// <summary>
    /// Builds a school-name → <c>TEAM_ORIGID</c> lookup for writing a
    /// transfer's previous school into <c>PLYR_PREVTEAMID</c>.
    ///
    /// This is a different id space from the team index that
    /// <see cref="BuildTeamMappings"/> resolves, which is why it is a separate
    /// lookup rather than a second use of the same one. Returns null when the
    /// export's Team table carries no <c>TEAM_ORIGID</c> column, in which case
    /// previous schools cannot be written.
    /// </summary>
    public TeamMappingSet? BuildPreviousSchoolMappings(string? aliasOverlayPath = null)
    {
        var withOriginalId = Teams.Where(t => t.OriginalId > 0).ToList();
        if (withOriginalId.Count == 0)
        {
            return null;
        }

        var entries = withOriginalId
            .Select(t => (TeamId: t.OriginalId, Names: (IReadOnlyList<string>)new[] { t.DisplayName, t.ShortName }
                .Where(n => n.Length > 0)
                .ToList()))
            .ToList();

        // The save spells schools its own way — "Mississippi St", "W.
        // Michigan" — while a user writes them out in full. The alias overlay
        // already carries those spellings against the team index, so translate
        // it into this id space rather than duplicating the aliases.
        if (aliasOverlayPath is not null && File.Exists(aliasOverlayPath))
        {
            var originalIdByTeamIndex = withOriginalId.ToDictionary(t => t.TeamIndex, t => t.OriginalId);
            entries.AddRange(TeamMappingSet.LoadEntries(aliasOverlayPath)
                .Where(e => originalIdByTeamIndex.ContainsKey(e.TeamId))
                .Select(e => (TeamId: originalIdByTeamIndex[e.TeamId], e.Names)));
        }

        return TeamMappingSet.Build(entries);
    }

    /// <summary>
    /// Opens a dynasty export. <paramref name="path"/> may be the export
    /// folder (searched recursively), or a Player table CSV directly (its
    /// folder is then searched for a Team table).
    /// </summary>
    public static DynastyExport Open(string path)
    {
        string? playerPath = null;
        string directory;
        if (File.Exists(path))
        {
            playerPath = path;
            directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        }
        else if (Directory.Exists(path))
        {
            directory = path;
        }
        else
        {
            throw new FileNotFoundException($"'{path}' does not exist.");
        }

        var teamCandidates = new List<(string Path, List<DynastyTeam> Teams)>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.csv", SearchOption.AllDirectories))
        {
            var tableName = PeekTableName(file);
            if (playerPath is null && tableName == "Player" && LooksLikePlayerTable(file))
            {
                playerPath = file;
            }
            else if (tableName == "Team")
            {
                var teams = ReadTeams(file);
                if (teams.Count > 0)
                {
                    teamCandidates.Add((file, teams));
                }
            }
        }

        if (playerPath is null)
        {
            throw new CsvSchemaException(
                $"No Player table found under '{path}'. Point at the folder of CSV files the community " +
                "export tool wrote out of your dynasty — one CSV per table, including the Player table " +
                "— or at that Player CSV itself. A save file cannot be read directly; export it first.");
        }

        // The main Team table is the one listing the most teams; the export
        // also contains several single-row Team tables (sentinels/practice
        // squads) that must not win.
        var mainTeams = teamCandidates
            .OrderByDescending(c => c.Teams.Count)
            .Select(c => c.Teams)
            .FirstOrDefault() ?? new List<DynastyTeam>();

        return new DynastyExport(playerPath, mainTeams
            .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    /// <summary>Reads a file's <c>_tableName</c> from its first data row.</summary>
    private static string? PeekTableName(string file)
    {
        using var reader = new StreamReader(file);
        var header = reader.ReadLine();
        var firstRow = reader.ReadLine();
        if (header is null || firstRow is null)
        {
            return null;
        }

        var columns = header.Split(',');
        var nameIndex = Array.IndexOf(columns, "_tableName");
        if (nameIndex < 0)
        {
            return null;
        }

        var cells = firstRow.Split(',');
        return nameIndex < cells.Length ? cells[nameIndex] : null;
    }

    private static bool LooksLikePlayerTable(string file)
    {
        using var reader = new StreamReader(file);
        var header = (reader.ReadLine() ?? "").Split(',');
        return PlayerSchema.RequiredColumns.All(header.Contains);
    }

    private static List<DynastyTeam> ReadTeams(string file)
    {
        var document = CsvDocument.Load(file);
        var teams = new List<DynastyTeam>();
        if (!document.HasColumn("TeamIndex") || !document.HasColumn("DisplayName"))
        {
            return teams;
        }

        for (var row = 0; row < document.RowCount; row++)
        {
            if (document.HasColumn("_isEmpty") &&
                document.GetCell(row, "_isEmpty") == "true")
            {
                continue;
            }

            var displayName = document.GetCell(row, "DisplayName").Trim();
            if (displayName.Length == 0 ||
                !int.TryParse(document.GetCell(row, "TeamIndex"), out var teamIndex) ||
                teamIndex == PlayerSchema.NoTeamSentinel)
            {
                continue;
            }

            var shortName = document.HasColumn("ShortName")
                ? document.GetCell(row, "ShortName").Trim()
                : "";
            var originalId = document.HasColumn("TEAM_ORIGID") &&
                             int.TryParse(document.GetCell(row, "TEAM_ORIGID"), out var origId)
                ? origId
                : 0;
            teams.Add(new DynastyTeam(teamIndex, displayName, shortName, originalId));
        }

        return teams;
    }
}
