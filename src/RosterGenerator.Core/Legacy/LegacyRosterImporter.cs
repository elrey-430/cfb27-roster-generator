using System.Text.Json;
using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Legacy;

/// <summary>What an import produced.</summary>
/// <param name="Path">The roster CSV written.</param>
/// <param name="Teams">How many teams were written.</param>
/// <param name="Players">How many players were written.</param>
/// <param name="Skipped">Players dropped because the file never gave them a name.</param>
/// <param name="Notes">Everything the reader had to decide, for the report.</param>
public sealed record LegacyImportResult(
    string Path, int Teams, int Players, int Skipped, IReadOnlyList<string> Notes);

/// <summary>
/// Turns a PS2-era NCAA Football roster file into a roster CSV this tool can
/// generate from.
///
/// <para>Identity crosses over exactly — name, position, number, height,
/// weight, class year, skin tone, and the depth-chart role. The ratings do
/// not, and cannot: eighteen of CFB27's fifty-seven rating columns have any
/// counterpart in the older game, those eighteen carry only about half the
/// weight in the game's own overall formula, and the numbers themselves are
/// held in five or six bits on a scale nobody has anchored. Writing them
/// across would be inventing the other half and calling it history.</para>
///
/// <para>What does cross over is the <em>order</em>. Where a player stood on
/// his own squad becomes a talent signal, and where he stood among others at
/// his position becomes a nudge to the matching attribute, so the fastest
/// corner in the file stays the fastest corner. Both are ranks, and a rank
/// survives the trip between two games in a way a rating does not.</para>
/// </summary>
public static class LegacyRosterImporter
{
    /// <summary>Columns carrying a player's standing at his position, in write order.</summary>
    public static IReadOnlyList<string> ShapeColumns { get; } =
        LegacySchema.AttributeMap.Values.OrderBy(v => v, StringComparer.Ordinal)
            .Select(ColumnFor).ToList();

    /// <summary>The roster CSV column a rating's legacy standing is written to.</summary>
    public static string ColumnFor(string ratingColumn) =>
        "Legacy" + (ratingColumn.EndsWith("Rating", StringComparison.Ordinal)
            ? ratingColumn[..^"Rating".Length]
            : ratingColumn);

    /// <summary>Reads the team id map that names each squad.</summary>
    public static IReadOnlyDictionary<int, string> LoadTeamIds(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var map = new Dictionary<int, string>();
        if (!document.RootElement.TryGetProperty("teams", out var teams))
        {
            throw new InvalidDataException($"'{path}' has no 'teams' array.");
        }

        foreach (var team in teams.EnumerateArray())
        {
            map[team.GetProperty("teamId").GetInt32()] = team.GetProperty("school").GetString() ?? "";
        }

        return map;
    }

    /// <summary>
    /// Imports a legacy roster file, writing a roster CSV.
    /// </summary>
    /// <param name="rosterPath">The legacy roster file.</param>
    /// <param name="outputPath">Where to write the roster CSV.</param>
    /// <param name="teamIds">Team id to school, from <see cref="LoadTeamIds"/>.</param>
    /// <param name="season">The season to stamp on every row. The file records none.</param>
    /// <param name="school">One school to write, or null for every team in the file.</param>
    public static LegacyImportResult Import(
        string rosterPath, string outputPath, IReadOnlyDictionary<int, string> teamIds,
        int season, string? school = null)
    {
        var file = LegacyRosterReader.Read(rosterPath, teamIds);
        var notes = file.Notes.ToList();

        var teams = file.Teams;
        if (school is not null)
        {
            teams = teams
                .Where(t => string.Equals(t.School, school, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (teams.Count == 0)
            {
                throw new InvalidOperationException(
                    $"This roster file has no team called '{school}'. " +
                    $"It carries {file.Teams.Count} team(s); run without a team to write them all.");
            }
        }

        var header = new List<string>
        {
            "FirstName", "LastName", "Position", "Number", "HeightInches", "Weight", "Class",
            "Team", "Season", "SkinTone", "Role", "LegacyRank",
        };
        header.AddRange(ShapeColumns);

        var rows = new List<IReadOnlyList<string>> { header };
        var written = 0;
        var skipped = 0;

        foreach (var team in teams)
        {
            // The order is what carries across, so it is computed per squad
            // and per position inside that squad -- never across the file,
            // where a walk-on at a strong programme would outrank a starter
            // at a weak one on nothing but the company he kept.
            var named = team.Players.Where(p => !p.IsNameless).ToList();
            skipped += team.Players.Count - named.Count;
            var rank = Percentiles(named.Select(p => (object)p), p => ((LegacyPlayer)p).RawOverall);
            var shape = LegacySchema.AttributeMap.Values.ToDictionary(
                column => column,
                column => named
                    .GroupBy(p => p.Position)
                    .SelectMany(g => Percentiles(
                        g.Select(p => (object)p), p => ((LegacyPlayer)p).RawAttributes.GetValueOrDefault(column)))
                    .ToDictionary(x => x.Key, x => x.Value),
                StringComparer.Ordinal);

            foreach (var player in named)
            {
                var row = new List<string>
                {
                    player.FirstName,
                    player.LastName,
                    player.Position,
                    player.JerseyNumber?.ToString() ?? "",
                    player.HeightInches?.ToString() ?? "",
                    player.WeightPounds?.ToString() ?? "",
                    player.ClassYear ?? "",
                    team.School ?? $"Team {team.TeamId}",
                    season.ToString(),
                    player.SkinTone?.ToString() ?? "",
                    player.Role ?? "",
                    Format(rank.GetValueOrDefault(player.PlayerId)),
                };

                foreach (var column in LegacySchema.AttributeMap.Values.OrderBy(v => v, StringComparer.Ordinal))
                {
                    row.Add(shape[column].TryGetValue(player.PlayerId, out var value) ? Format(value) : "");
                }

                rows.Add(row);
                written++;
            }
        }

        var text = new System.Text.StringBuilder();
        foreach (var row in rows)
        {
            CsvFormat.WriteRecord(text, row);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, text.ToString());

        if (skipped > 0)
        {
            notes.Add(
                $"{skipped} player(s) had no name in the source file and were left out. The older games " +
                "shipped whole divisions unnamed, and a roster row with nobody on it is not a player.");
        }

        return new LegacyImportResult(outputPath, teams.Count, written, skipped, notes);
    }

    /// <summary>
    /// Turns values into places in an order: 0 for the best, 100 for the last.
    /// Ties share the average of the places they span, so three players on the
    /// same rating are not silently ordered by whoever the file listed first.
    /// A group of one sits at 50 — the middle, which moves nothing.
    /// </summary>
    private static Dictionary<int, double> Percentiles(
        IEnumerable<object> players, Func<object, int> value)
    {
        var list = players.Cast<LegacyPlayer>().ToList();
        var result = new Dictionary<int, double>();
        if (list.Count == 0)
        {
            return result;
        }

        if (list.Count == 1)
        {
            result[list[0].PlayerId] = 50;
            return result;
        }

        var ordered = list.OrderByDescending(p => value(p)).ToList();
        var index = 0;
        while (index < ordered.Count)
        {
            var tie = index;
            while (tie + 1 < ordered.Count && value(ordered[tie + 1]) == value(ordered[index]))
            {
                tie++;
            }

            var place = (index + tie) / 2.0 / (ordered.Count - 1) * 100;
            for (var i = index; i <= tie; i++)
            {
                result[ordered[i].PlayerId] = place;
            }

            index = tie + 1;
        }

        return result;
    }

    private static string Format(double value) =>
        value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
