using System.Text.Json;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Mapping;

/// <summary>
/// External historical-position → CFB27 position mapping, loaded from
/// <c>data/PositionMappings.json</c> (e.g. Tailback → HB, Cornerback → CB).
/// Also carries interchangeability groups (LT/LG/C/RG/RT, LE/RE, ...) used
/// by slot assignment: a player mapped to LE may fill an RE roster slot
/// without a position change. Lookups ignore case and punctuation.
/// </summary>
public sealed class PositionMappingSet
{
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _groupByPosition = new(StringComparer.Ordinal);

    private PositionMappingSet(
        IEnumerable<KeyValuePair<string, string>> aliases,
        IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> groups)
    {
        foreach (var (alias, target) in aliases)
        {
            if (!PlayerSchema.Positions.Contains(target))
            {
                throw new InvalidDataException(
                    $"Position alias '{alias}' maps to '{target}', which is not a valid CFB27 position.");
            }

            _aliases[TeamMappingSet.Normalize(alias)] = target;
        }

        foreach (var (groupName, positions) in groups)
        {
            foreach (var position in positions)
            {
                _groupByPosition[position] = groupName;
            }
        }
    }

    /// <summary>Resolves a historical position label to a CFB27 position.</summary>
    /// <exception cref="KeyNotFoundException">No alias matches the label.</exception>
    public string Resolve(string historicalPosition)
    {
        if (TryResolve(historicalPosition, out var position))
        {
            return position;
        }

        throw new KeyNotFoundException(
            $"Position '{historicalPosition}' is not in the position mapping file. Add it to PositionMappings.json.");
    }

    /// <summary>Tries to resolve a historical position label.</summary>
    public bool TryResolve(string historicalPosition, out string cfb27Position)
    {
        if (_aliases.TryGetValue(TeamMappingSet.Normalize(historicalPosition), out var mapped))
        {
            cfb27Position = mapped;
            return true;
        }

        cfb27Position = "";
        return false;
    }

    /// <summary>
    /// True when two CFB27 positions are interchangeable for slot assignment
    /// (identical, or members of the same group such as LT/RT or LE/RE).
    /// </summary>
    public bool AreInterchangeable(string positionA, string positionB)
    {
        if (string.Equals(positionA, positionB, StringComparison.Ordinal))
        {
            return true;
        }

        return _groupByPosition.TryGetValue(positionA, out var groupA) &&
               _groupByPosition.TryGetValue(positionB, out var groupB) &&
               string.Equals(groupA, groupB, StringComparison.Ordinal);
    }

    /// <summary>Loads the mapping file.</summary>
    public static PositionMappingSet Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("aliases", out var aliasesElement))
        {
            throw new InvalidDataException($"'{path}' has no 'aliases' object.");
        }

        var aliases = aliasesElement.EnumerateObject()
            .Select(p => new KeyValuePair<string, string>(p.Name, p.Value.GetString() ?? ""))
            .ToList();

        var groups = new List<KeyValuePair<string, IReadOnlyList<string>>>();
        if (document.RootElement.TryGetProperty("groups", out var groupsElement))
        {
            foreach (var group in groupsElement.EnumerateObject())
            {
                var positions = group.Value.EnumerateArray()
                    .Select(p => p.GetString() ?? "")
                    .Where(p => p.Length > 0)
                    .ToList();
                groups.Add(new KeyValuePair<string, IReadOnlyList<string>>(group.Name, positions));
            }
        }

        return new PositionMappingSet(aliases, groups);
    }
}
