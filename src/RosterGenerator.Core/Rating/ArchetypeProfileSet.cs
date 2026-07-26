using System.Text.Json;

namespace RosterGenerator.Core.Rating;

/// <summary>
/// What the game itself gives one archetype, measured across every real
/// player of that archetype in a dynasty export.
///
/// Each attribute holds <c>[intercept, slope, residualSd]</c> for
/// <c>value = intercept + slope * overall</c>. The first two say where a
/// player of this archetype at this overall sits; the third says how far the
/// game's own players scatter around that line, which is the honest size of a
/// "this one was better at that" nudge.
/// </summary>
public sealed class ArchetypeProfile
{
    /// <summary>How many real players this profile was measured from.</summary>
    public int SampleSize { get; init; }

    /// <summary>Attribute → [intercept, slope, residualSd].</summary>
    public Dictionary<string, double[]> Fit { get; init; } = new();

    /// <summary>
    /// The value the game gives this archetype at <paramref name="overall"/>.
    /// False when the export carried too few players to measure it.
    /// </summary>
    public bool TryExpected(string attribute, double overall, out double value)
    {
        if (Fit.TryGetValue(attribute, out var line) && line.Length >= 2)
        {
            value = line[0] + line[1] * overall;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// How far the game's own players of this archetype scatter around the
    /// line for this attribute. Zero when unmeasured, which makes any nudge
    /// sized in these units fall to nothing rather than to a guess.
    /// </summary>
    public double Spread(string attribute) =>
        Fit.TryGetValue(attribute, out var line) && line.Length >= 3 ? line[2] : 0;

    /// <summary>Attributes this profile could measure.</summary>
    public IEnumerable<string> Attributes => Fit.Keys;
}

/// <summary>
/// The measured attribute shapes for every archetype, loaded from
/// <c>data/ArchetypeProfiles.json</c>.
///
/// This file is generated, never hand-written — <c>tools/build_archetype_profiles.py</c>
/// reads a real dynasty export and fits every archetype's every attribute
/// against overall. It exists because the alternative is somebody's opinion of
/// what a receiving back should be good at, and opinions do not scale to 59
/// archetypes across 56 attributes.
///
/// Seeding a generated player from their archetype's line is self-consistent:
/// feeding those values back through EA's own overall formula returns the
/// overall they were built for to within a third of a point for 56 of the 59
/// archetypes, so calibration only has to make a small correction rather than
/// haul a wrong shape into place.
/// </summary>
public sealed class ArchetypeProfileSet
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, ArchetypeProfile> _profiles;

    private ArchetypeProfileSet(Dictionary<string, ArchetypeProfile> profiles, int minimumSample)
    {
        _profiles = profiles;
        MinimumSample = minimumSample;
    }

    /// <summary>Fewest players an archetype needed before it was measured.</summary>
    public int MinimumSample { get; }

    /// <summary>Archetypes measured.</summary>
    public IReadOnlyCollection<string> Archetypes => _profiles.Keys;

    /// <summary>
    /// The profile for an archetype, or null when it was not measured (an
    /// archetype the export carried fewer than <see cref="MinimumSample"/>
    /// players of). Callers fall back to the hand-written position baseline.
    /// </summary>
    public ArchetypeProfile? Find(string? archetype) =>
        archetype is { Length: > 0 } name && _profiles.TryGetValue(name, out var profile) ? profile : null;

    /// <summary>Loads the generated profile file.</summary>
    public static ArchetypeProfileSet Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("archetypes", out var archetypes))
        {
            throw new InvalidDataException($"'{path}' has no 'archetypes' object.");
        }

        var minimum = root.TryGetProperty("minimumSample", out var sample) ? sample.GetInt32() : 0;
        var parsed = archetypes.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Deserialize<ArchetypeProfile>(JsonOptions)!,
                StringComparer.Ordinal);
        return new ArchetypeProfileSet(parsed, minimum);
    }
}
