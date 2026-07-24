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

    private TeamMappingSet(IEnumerable<(int TeamId, IReadOnlyList<string> Names)> teams)
    {
        foreach (var (teamId, names) in teams)
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
            }
        }
    }

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
    public static TeamMappingSet Load(string path)
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

        return new TeamMappingSet(entries);
    }

    /// <summary>Lowercases and strips everything but letters and digits.</summary>
    internal static string Normalize(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
