using System.Text.Json;
using RosterGenerator.Core.Csv;

namespace RosterGenerator.Core.Historical;

/// <summary>What writing a season's blank template produced.</summary>
/// <param name="Path">Where the CSV went.</param>
/// <param name="Teams">Teams the file carries.</param>
/// <param name="Rows">Player rows written.</param>
/// <param name="SlotsPerTeam">Roster slots given to each team.</param>
/// <param name="Excluded">Teams left out, and why.</param>
public sealed record SeasonTemplateResult(
    string Path,
    int Teams,
    int Rows,
    int SlotsPerTeam,
    IReadOnlyList<FbsMembershipProblem> Excluded);

/// <summary>
/// Writes a blank roster template for a whole season: every team that actually
/// played that year, each with its full complement of roster slots, with
/// <c>Team</c>, <c>Season</c> and <c>Position</c> already filled in.
///
/// <para><b>Why the tool writes this rather than the user.</b> Filling in a
/// season by hand means knowing which 130-odd teams existed that year and
/// typing 85 rows for each — over 11,000 rows before a single player's name is
/// researched. Worse, the part that is easy to get wrong is invisible: CFB27
/// carries the 138 teams of today, so a 2010 file assembled from that list
/// silently includes schools that were still in the FCS.</para>
///
/// <para>The position layout is <b>measured</b>: the league mean across a base
/// save's 138 teams, apportioned to exactly 85 by largest remainder. It is a
/// starting shape, not a rule — nothing stops a filled roster from looking
/// different, and unfilled slots are still completed as depth.</para>
/// </summary>
public sealed class SeasonTemplateWriter
{
    private readonly IReadOnlyList<string> _positionSlots;

    private SeasonTemplateWriter(IReadOnlyList<string> positionSlots)
    {
        _positionSlots = positionSlots;
    }

    /// <summary>Roster slots one team is given.</summary>
    public int SlotsPerTeam => _positionSlots.Count;

    /// <summary>Loads the measured roster shape.</summary>
    public static SeasonTemplateWriter Load(string skeletonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(skeletonPath));
        if (!document.RootElement.TryGetProperty("slotsByPosition", out var slots))
        {
            throw new InvalidDataException($"'{skeletonPath}' has no 'slotsByPosition' object.");
        }

        // Ordered as the file orders them — most-numerous position first —
        // so a team's rows read like a depth chart rather than a shuffle.
        var expanded = new List<string>();
        foreach (var position in slots.EnumerateObject())
        {
            for (var i = 0; i < position.Value.GetInt32(); i++)
            {
                expanded.Add(position.Name);
            }
        }

        if (expanded.Count == 0)
        {
            throw new InvalidDataException($"'{skeletonPath}' allocates no roster slots.");
        }

        return new SeasonTemplateWriter(expanded);
    }

    /// <summary>
    /// Writes the season's blank template.
    /// </summary>
    /// <param name="path">Destination CSV.</param>
    /// <param name="templateHeaderPath">
    /// The shipped roster template, whose header row is copied verbatim so the
    /// blank file and the documented format can never drift apart.
    /// </param>
    /// <param name="teams">Every team in the user's dynasty.</param>
    /// <param name="season">The season being recreated.</param>
    /// <param name="membership">
    /// Which schools had actually reached the FBS by then. Pass
    /// <see cref="FbsMembership.Empty"/> to include every team.
    /// </param>
    public SeasonTemplateResult Write(
        string path,
        string templateHeaderPath,
        IEnumerable<string> teams,
        int season,
        FbsMembership membership)
    {
        var header = CsvDocument.Load(templateHeaderPath).Header;
        var index = header
            .Select((name, i) => (name, i))
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);

        var excluded = new List<FbsMembershipProblem>();
        var included = new List<string>();
        foreach (var team in teams)
        {
            var problem = membership.Check(team, season);
            if (problem is null)
            {
                included.Add(team);
            }
            else
            {
                excluded.Add(problem);
            }
        }

        var body = new List<IReadOnlyList<string>>();
        foreach (var team in included)
        {
            foreach (var position in _positionSlots)
            {
                var cells = new string[header.Count];
                Array.Fill(cells, "");
                Set(cells, index, "Team", team);
                Set(cells, index, "Season", season.ToString());
                Set(cells, index, "Position", position);
                body.Add(cells);
            }
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is { Length: > 0 })
        {
            Directory.CreateDirectory(directory);
        }

        CsvDocument.FromRows(header, body).Save(path);
        return new SeasonTemplateResult(path, included.Count, body.Count, SlotsPerTeam, excluded);
    }

    private static void Set(string[] cells, IReadOnlyDictionary<string, int> index, string column, string value)
    {
        if (index.TryGetValue(column, out var position))
        {
            cells[position] = value;
        }
    }
}
