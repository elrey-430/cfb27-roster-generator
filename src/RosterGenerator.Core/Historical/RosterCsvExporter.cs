using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Depth;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Equipment;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Historical;

/// <summary>What an export wrote.</summary>
/// <param name="Path">The roster file.</param>
/// <param name="Teams">Teams written, in order.</param>
/// <param name="Players">Rows written.</param>
/// <param name="Starters">Players marked Starter from the dynasty's depth chart.</param>
/// <param name="RolesFromDepthChart">
/// True when roles came from the depth chart rather than being left blank.
/// </param>
public sealed record RosterCsvExportReport(
    string Path,
    IReadOnlyList<string> Teams,
    int Players,
    int Starters,
    bool RolesFromDepthChart);

/// <summary>
/// Writes a team out of a dynasty <em>as a roster file</em> — the same format
/// the generator reads.
///
/// <para><b>Why this is the other half of the tool.</b> Until now the tool could
/// read a roster file and not write one, and that asymmetry cost users twice.
/// Correcting one player in ten thousand meant retyping the roster or editing
/// the result in a third-party editor, where the correction was invisible to
/// this tool and lost on the next run. And a new project started from a blank
/// template rather than from what the dynasty already has.</para>
///
/// <para><b>What comes out.</b> Everything the save actually knows: name,
/// position, jersey, height, weight, class, hometown, previous school, skin
/// tone, and — when the dynasty carries a depth chart — each player's role,
/// read from where they sit on it.</para>
///
/// <para><b>What does not, and cannot.</b> The evidence columns are left empty:
/// statistics, awards, combine numbers and draft slots are facts about a
/// college season that a CFB27 save has never held. An exported file therefore
/// regenerates a player's <em>identity</em> exactly and their <em>ratings</em>
/// from scratch — which is the honest behaviour, since the ratings in the save
/// were not derived from evidence in the first place. Fill the evidence columns
/// in and the ratings become yours.</para>
/// </summary>
public static class RosterCsvExporter
{
    /// <summary>
    /// The columns written, in the template's own order. Evidence columns are
    /// written as empty cells rather than dropped, so the file opens as the
    /// template a user already knows.
    /// </summary>
    public static readonly IReadOnlyList<string> Columns = new[]
    {
        "FirstName", "LastName", "Position", "Number", "HeightInches", "Weight",
        "Class", "Role", "Team", "Season", "Hometown", "PreviousSchool", "Notes",
        "SkinTone", "StarRating", "Forty", "Bench", "Vertical", "Shuttle", "ThreeCone",
        "DraftRound", "DraftPick", "Awards", "AwardContender",
        "PassYards", "PassTD", "PassInt", "Completions", "Attempts",
        "RushYards", "RushTD", "RushAttempts", "RecYards", "RecTD", "Receptions",
        "Tackles", "Sacks", "TacklesForLoss", "Interceptions", "PassesDefended",
        "ForcedFumbles", "FieldGoalsMade", "FieldGoalsAttempted", "LongFieldGoal",
        "PuntAverage", "GamesPlayed", "GamesStarted",
    };

    /// <summary>
    /// Writes the roster file.
    /// </summary>
    /// <param name="export">The dynasty to read.</param>
    /// <param name="roster">Its player table.</param>
    /// <param name="path">Where to write.</param>
    /// <param name="teamIndices">
    /// Teams to write, or null for every team the dynasty carries — which is
    /// how a whole-season file is produced in one pass.
    /// </param>
    /// <param name="season">Season to stamp on every row, or null to leave blank.</param>
    /// <param name="visuals">CharacterVisuals, for skin tone. Optional.</param>
    /// <param name="depthCharts">The dynasty's depth charts, for roles. Optional.</param>
    /// <param name="slots">The measured slot model, needed with <paramref name="depthCharts"/>.</param>
    public static RosterCsvExportReport Write(
        DynastyExport export,
        PlayerRoster roster,
        string path,
        IReadOnlyList<int>? teamIndices = null,
        int? season = null,
        CharacterVisualsTable? visuals = null,
        DepthChartTable? depthCharts = null,
        DepthChartSlotModel? slots = null)
    {
        var nameByIndex = export.Teams.ToDictionary(t => t.TeamIndex, t => t.DisplayName);
        var schoolById = export.Teams
            .Where(t => t.OriginalId > 0)
            .GroupBy(t => t.OriginalId)
            .ToDictionary(g => g.Key, g => g.First().DisplayName);

        var wanted = teamIndices?.ToHashSet();
        var players = roster.Players
            .Where(p => !p.IsEmpty && nameByIndex.ContainsKey(p.TeamIndex))
            .Where(p => wanted is null || wanted.Contains(p.TeamIndex))
            .OrderBy(p => nameByIndex[p.TeamIndex], StringComparer.Ordinal)
            .ThenByDescending(p => p.OverallRating)
            .ThenBy(p => p.RowKey)
            .ToList();

        var roles = depthCharts is not null && slots is not null
            ? DepthChartRoles(players, depthCharts, slots)
            : null;

        var rows = new List<IReadOnlyList<string>>(players.Count);
        var starters = 0;
        foreach (var player in players)
        {
            var role = roles is not null && roles.TryGetValue(player.RowKey, out var found) ? found : "";
            if (string.Equals(role, "Starter", StringComparison.Ordinal))
            {
                starters++;
            }

            rows.Add(Columns.Select(column => column switch
            {
                "FirstName" => player.FirstName,
                "LastName" => player.LastName,
                "Position" => player.Position,
                "Number" => player.JerseyNumber.ToString(),
                "HeightInches" => player.HeightInches.ToString(),
                "Weight" => player.WeightPounds.ToString(),
                "Class" => ClassLabel(player),
                "Role" => role,
                "Team" => nameByIndex[player.TeamIndex],
                "Season" => season?.ToString() ?? "",
                "Hometown" => Hometown(player),
                "PreviousSchool" => PreviousSchool(player, schoolById),
                "SkinTone" => SkinTone(player, visuals),
                _ => "",
            }).ToList());
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is { Length: > 0 })
        {
            Directory.CreateDirectory(directory);
        }

        CsvDocument.FromRows(Columns, rows).Save(path);

        return new RosterCsvExportReport(
            path,
            players.Select(p => nameByIndex[p.TeamIndex]).Distinct(StringComparer.Ordinal).ToList(),
            players.Count,
            starters,
            roles is not null);
    }

    /// <summary>
    /// Reads each player's role off the dynasty's own depth chart.
    ///
    /// <para>Heading a slot named for a real position is what makes a starter —
    /// the specialist slots (<c>3DRB</c>, <c>KR</c>, <c>SLWR</c> and the rest)
    /// describe a package, not a starting job, so leading one of those does not
    /// make a third receiver a starter. Listed anywhere else is a backup;
    /// listed nowhere is a reserve.</para>
    /// </summary>
    private static Dictionary<int, string> DepthChartRoles(
        IReadOnlyList<Player> players, DepthChartTable charts, DepthChartSlotModel slots)
    {
        var roles = new Dictionary<int, string>();
        foreach (var team in players.Select(p => p.TeamIndex).Distinct())
        {
            var listing = charts.Listing(team);
            if (listing is null)
            {
                continue;
            }

            foreach (var (slot, ordered) in listing)
            {
                var startsHere = PlayerSchema.Positions.Contains(slot);
                for (var i = 0; i < ordered.Count; i++)
                {
                    var role = i == 0 && startsHere ? "Starter" : "Backup";
                    if (role == "Starter" || !roles.ContainsKey(ordered[i]))
                    {
                        roles[ordered[i]] = role;
                    }
                }
            }
        }

        foreach (var player in players)
        {
            roles.TryAdd(player.RowKey, "Reserve");
        }

        return roles;
    }

    /// <summary>The class label a user would write, redshirt prefix included.</summary>
    private static string ClassLabel(Player player)
    {
        var year = player.SchoolYear;
        if (year.Length == 0)
        {
            return "";
        }

        return string.Equals(player.RedshirtStatus, "Previous", StringComparison.Ordinal)
            ? $"Redshirt {year}"
            : year;
    }

    private static string Hometown(Player player)
    {
        if (!player.HasColumn(PlayerColumns.HomeTown) || !player.HasColumn(PlayerColumns.HomeState))
        {
            return "";
        }

        var town = player.GetRaw(PlayerColumns.HomeTown).Trim();
        var state = player.GetRaw(PlayerColumns.HomeState).Trim();
        if (town.Length == 0)
        {
            return "";
        }

        // Written the way the reader parses it, so a round trip does not lose
        // the state — and the save's PascalCase spelling is spaced out, since
        // that is what a user would type.
        return state.Length == 0 || string.Equals(state, PlayerSchema.NonUsHomeState, StringComparison.Ordinal)
            ? town
            : $"{town}, {Spaced(state)}";
    }

    /// <summary>Turns the save's <c>WestVirginia</c> back into <c>West Virginia</c>.</summary>
    private static string Spaced(string state)
    {
        var text = new System.Text.StringBuilder(state.Length + 4);
        for (var i = 0; i < state.Length; i++)
        {
            if (i > 0 && char.IsUpper(state[i]) && !char.IsUpper(state[i - 1]))
            {
                text.Append(' ');
            }

            text.Append(state[i]);
        }

        return text.ToString();
    }

    private static string PreviousSchool(Player player, IReadOnlyDictionary<int, string> schoolById)
    {
        if (!player.HasColumn(PlayerColumns.PrevTeamId) ||
            !int.TryParse(player.GetRaw(PlayerColumns.PrevTeamId), out var id) ||
            id == PlayerSchema.NoPrevTeamIdSentinel)
        {
            return "";
        }

        // A school the dynasty does not model has an id but no name. Blank
        // would come back as "never transferred", which is a different and
        // untrue thing, so the fact is written in the tool's own spelling.
        return schoolById.TryGetValue(id, out var name)
            ? name
            : PlayerSchema.PreviousSchoolNotInDynasty;
    }

    private static string SkinTone(Player player, CharacterVisualsTable? visuals)
    {
        if (visuals is null || !player.HasColumn(PlayerColumns.CharacterVisuals))
        {
            return "";
        }

        var rowId = CharacterVisualsReference.RowId(player.GetRaw(PlayerColumns.CharacterVisuals));
        return rowId is int row && visuals.GetSkinTone(row) is int tone ? tone.ToString() : "";
    }
}
