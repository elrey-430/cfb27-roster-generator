using System.Text.Json;
using System.Text.Json.Serialization;
using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>One condition on a player's profile.</summary>
public sealed class ArchetypeCondition
{
    /// <summary>Stat name, or WeightPounds / HeightInches / FortyYardDash.</summary>
    public string Field { get; init; } = "";

    /// <summary>Inclusive lower bound, if any.</summary>
    public double? Min { get; init; }

    /// <summary>Inclusive upper bound, if any.</summary>
    public double? Max { get; init; }
}

/// <summary>One archetype rule: an archetype plus the conditions it needs.</summary>
public sealed class ArchetypeRule
{
    /// <summary>Archetype chosen when every condition holds.</summary>
    public string Archetype { get; init; } = "";

    /// <summary>Conditions that must all hold.</summary>
    public List<ArchetypeCondition> All { get; init; } = new();
}

/// <summary>The rules and fallback for one CFB27 position.</summary>
public sealed class PositionArchetypeRules
{
    /// <summary>Archetype used when no rule matches.</summary>
    public string Default { get; init; } = "";

    /// <summary>Rules in priority order; the first full match wins.</summary>
    public List<ArchetypeRule> Rules { get; init; } = new();

    /// <summary>Every archetype legal at this position.</summary>
    public List<string> Available { get; init; } = new();
}

/// <summary>The outcome of choosing an archetype.</summary>
/// <param name="Archetype">The chosen archetype.</param>
/// <param name="Reason">Why it was chosen, for the report.</param>
/// <param name="MatchedRule">False when the position default was used.</param>
public sealed record ArchetypeChoice(string Archetype, string Reason, bool MatchedRule);

/// <summary>
/// Chooses a player's CFB27 archetype (<c>PlayerType</c>) from their
/// historical profile, using the editable rules in
/// <c>data/ArchetypeRules.json</c>.
///
/// This matters for more than flavour: the archetype selects which of EA's
/// overall formulas applies, so a power back left in an elusive-back slot is
/// rated against the wrong weighting. Whenever the archetype changes the
/// caller must recompute the overall — a dependency the community editor
/// misses, which is why 35 of 85 players in a manually edited save carry an
/// overall belonging to their pre-edit archetype.
/// </summary>
public sealed class ArchetypeSelector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly Dictionary<string, PositionArchetypeRules> _positions;

    private ArchetypeSelector(Dictionary<string, PositionArchetypeRules> positions)
    {
        _positions = positions;
    }

    /// <summary>Positions the rules cover.</summary>
    public IReadOnlyCollection<string> Positions => _positions.Keys;

    /// <summary>True when the archetype is legal for the position.</summary>
    public bool IsValidFor(string position, string archetype) =>
        _positions.TryGetValue(position, out var rules) && rules.Available.Contains(archetype);

    /// <summary>Archetypes legal at a position.</summary>
    public IReadOnlyList<string> AvailableFor(string position) =>
        _positions.TryGetValue(position, out var rules) ? rules.Available : Array.Empty<string>();

    /// <summary>
    /// Chooses an archetype for a player. Conditions read the player's
    /// measurables and season stats; a condition whose field is absent never
    /// matches, so a player with no evidence falls through to the position
    /// default rather than being guessed at.
    /// </summary>
    public ArchetypeChoice Select(string position, HistoricalPlayer player, RatingEvidence evidence)
    {
        if (!_positions.TryGetValue(position, out var rules))
        {
            throw new KeyNotFoundException(
                $"No archetype rules for position '{position}' in ArchetypeRules.json.");
        }

        var profile = BuildProfile(player, evidence);
        foreach (var rule in rules.Rules)
        {
            if (rule.All.Count == 0 || !rule.All.All(c => Matches(c, profile)))
            {
                continue;
            }

            var because = string.Join(", ", rule.All.Select(c => Describe(c, profile)));
            return new ArchetypeChoice(rule.Archetype, $"{rule.Archetype} chosen because {because}", true);
        }

        return new ArchetypeChoice(rules.Default,
            $"{rules.Default} used as the {position} default (no archetype rule matched the available data)",
            false);
    }

    private static Dictionary<string, double> BuildProfile(HistoricalPlayer player, RatingEvidence evidence)
    {
        var profile = new Dictionary<string, double>(
            TalentScorer.WithDerivedStats(evidence.Stats), StringComparer.OrdinalIgnoreCase);

        if (player.WeightPounds is int weight)
        {
            profile["WeightPounds"] = weight;
        }

        if (player.HeightInches is int height)
        {
            profile["HeightInches"] = height;
        }

        if (evidence.FortyYardDash is double forty)
        {
            profile["FortyYardDash"] = forty;
        }

        // Yards per reception is useful for separating deep threats from
        // possession receivers, and is derivable when both parts are present.
        if (profile.TryGetValue(StatKeys.RecYards, out var recYards) &&
            profile.TryGetValue(StatKeys.Receptions, out var receptions) && receptions > 0)
        {
            profile["YardsPerReception"] = recYards / receptions;
        }

        return profile;
    }

    private static bool Matches(ArchetypeCondition condition, IReadOnlyDictionary<string, double> profile)
    {
        if (!profile.TryGetValue(condition.Field, out var value))
        {
            return false;
        }

        return (condition.Min is not double min || value >= min) &&
               (condition.Max is not double max || value <= max);
    }

    private static string Describe(ArchetypeCondition condition, IReadOnlyDictionary<string, double> profile)
    {
        var value = profile.TryGetValue(condition.Field, out var v) ? v : 0;
        var bound = condition.Min is double min
            ? $"at least {min:0.##}"
            : $"at most {condition.Max:0.##}";
        return $"{condition.Field} {value:0.##} is {bound}";
    }

    /// <summary>Loads the rules file.</summary>
    public static ArchetypeSelector Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("positions", out var positions))
        {
            throw new InvalidDataException($"'{path}' has no 'positions' object.");
        }

        var parsed = positions.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Deserialize<PositionArchetypeRules>(JsonOptions)!,
                StringComparer.Ordinal);
        return new ArchetypeSelector(parsed);
    }
}
