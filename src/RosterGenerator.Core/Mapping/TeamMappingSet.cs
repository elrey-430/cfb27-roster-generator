using System.Text.Json;

namespace RosterGenerator.Core.Mapping;

/// <summary>
/// External school-name → CFB27 <c>TeamIndex</c> mapping, loaded from
/// <c>data/TeamMappings.json</c>. Team ids are never hard-coded: the file is
/// generated from a save's own Team table and hand-editable (aliases like
/// "FSU" or "Florida State University" all resolve to the same id).
/// Lookups ignore case, whitespace and punctuation.
/// </summary>
public sealed class TeamMappingSet
{
    private readonly Dictionary<string, int> _byNormalizedName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _standInByName = new(StringComparer.Ordinal);

    private TeamMappingSet(IEnumerable<(int TeamId, IReadOnlyList<string> Names, string? StandIn)> teams)
    {
        foreach (var (teamId, names, standIn) in teams)
        {
            foreach (var name in names)
            {
                var key = Normalize(name);
                if (key.Length == 0)
                {
                    continue;
                }

                if (_byNormalizedName.TryGetValue(key, out var existing) && existing != teamId)
                {
                    throw new InvalidDataException(
                        $"Team alias '{name}' maps to both team {existing} and team {teamId}.");
                }

                _byNormalizedName[key] = teamId;
                if (standIn is { Length: > 0 })
                {
                    _standInByName[key] = standIn;
                }
            }
        }
    }

    /// <summary>
    /// The team in the save whose roster slots a school should be written
    /// onto, when that is not simply the school's own — or null for the
    /// ordinary case.
    ///
    /// <para>It exists for schools the game no longer carries. Idaho played
    /// FBS football for decades and CFB27 does not have them, so a 2004 Idaho
    /// roster had nowhere to go at all. Naming one of the game's five generic
    /// FCS teams gives it eighty-five slots to occupy.</para>
    ///
    /// <para>The redirect is by team NAME rather than by <c>TeamIndex</c>,
    /// because every FCS team shares index 255 with the whole recruiting pool
    /// — see <see cref="Dynasty.TeamRosterTable"/>.</para>
    /// </summary>
    public string? StandInTeam(string schoolName) =>
        _standInByName.TryGetValue(Normalize(schoolName), out var team) ? team : null;

    /// <summary>Resolves a school name/alias to its CFB27 team index.</summary>
    /// <exception cref="KeyNotFoundException">No alias matches the name.</exception>
    public int Resolve(string schoolName)
    {
        if (_byNormalizedName.TryGetValue(Normalize(schoolName), out var teamId))
        {
            return teamId;
        }

        throw new KeyNotFoundException(
            $"School '{schoolName}' is not in the team mapping file. Add it (or an alias) to TeamMappings.json.");
    }

    /// <summary>Tries to resolve a school name/alias to its team index.</summary>
    public bool TryResolve(string schoolName, out int teamId) =>
        _byNormalizedName.TryGetValue(Normalize(schoolName), out teamId);

    /// <summary>Loads the mapping file.</summary>
    public static TeamMappingSet Load(string path) => Build(LoadEntriesWithStandIns(path));

    /// <summary>Builds a mapping set from raw (teamId, names) entries.</summary>
    public static TeamMappingSet Build(IEnumerable<(int TeamId, IReadOnlyList<string> Names)> entries) =>
        new(entries.Select(e => (e.TeamId, e.Names, (string?)null)));

    /// <summary>Builds a mapping set including stand-in teams.</summary>
    public static TeamMappingSet Build(
        IEnumerable<(int TeamId, IReadOnlyList<string> Names, string? StandIn)> entries) => new(entries);

    /// <summary>Reads a mapping file's raw (teamId, names) entries.</summary>
    public static IReadOnlyList<(int TeamId, IReadOnlyList<string> Names)> LoadEntries(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("teams", out var teams))
        {
            throw new InvalidDataException($"'{path}' has no 'teams' array.");
        }

        var entries = new List<(int, IReadOnlyList<string>)>();
        foreach (var team in teams.EnumerateArray())
        {
            var teamId = team.GetProperty("teamId").GetInt32();
            var names = team.GetProperty("names").EnumerateArray()
                .Select(n => n.GetString() ?? "")
                .Where(n => n.Length > 0)
                .ToList();
            entries.Add((teamId, names));
        }

        return entries;
    }

    /// <summary>Reads a mapping file, keeping each entry's stand-in team.</summary>
    public static IReadOnlyList<(int TeamId, IReadOnlyList<string> Names, string? StandIn)>
        LoadEntriesWithStandIns(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("teams", out var teams))
        {
            throw new InvalidDataException($"'{path}' has no 'teams' array.");
        }

        var entries = new List<(int, IReadOnlyList<string>, string?)>();
        foreach (var team in teams.EnumerateArray())
        {
            var names = team.GetProperty("names").EnumerateArray()
                .Select(n => n.GetString() ?? "")
                .Where(n => n.Length > 0)
                .ToList();
            entries.Add((
                team.GetProperty("teamId").GetInt32(),
                names,
                team.TryGetProperty("standInTeam", out var standIn) ? standIn.GetString() : null));
        }

        return entries;
    }

    /// <summary>Lowercases and strips everything but letters and digits.</summary>
    internal static string Normalize(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
