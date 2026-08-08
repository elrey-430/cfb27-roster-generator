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

    /// <summary>
    /// Which of several archetypes a set of real attribute values looks most
    /// like, at a given overall.
    ///
    /// <para>An imported player arrives with the numbers somebody gave him and
    /// no stat line, so the ordinary rules — 800 rushing yards makes a
    /// scrambler — have nothing to read. The attributes themselves say it
    /// better anyway: the game's own quarterbacks at overall 85 throw 91/89/87
    /// short-to-deep as field generals and 85/82/77 as pure scramblers, and
    /// which of those a player resembles is a measurement rather than a
    /// guess.</para>
    ///
    /// <para>Distance is counted in each attribute's own measured scatter, so
    /// an attribute the game spreads widely across an archetype counts for
    /// less than one it holds tightly — being 5 points off a value that varies
    /// by 8 says nothing, and being 5 off one that varies by 1 says a great
    /// deal.</para>
    /// </summary>
    /// <param name="candidates">Archetypes legal at the player's position.</param>
    /// <param name="ratings">Real 0-99 values, keyed by CFB27 rating column.</param>
    /// <param name="overall">The overall to compare at.</param>
    /// <param name="distance">Mean squared scatter-units from the winner.</param>
    /// <param name="compared">How many attributes the comparison could use.</param>
    /// <param name="splits">
    /// Source columns CFB27 holds as several, from
    /// <see cref="RatingModelSet.SourceRatingSplits"/>. One of these is
    /// compared against the average of what the archetype gives the several,
    /// never against a CFB27 column of the same name — the game keeps a
    /// general ThrowAccuracyRating that its own improvisers carry at 34 while
    /// throwing 86 short, and comparing a source's 88 against that would put
    /// every improviser three scatter-units from being an improviser.
    /// </param>
    /// <returns>The best match, or null when nothing could be compared.</returns>
    public string? BestMatch(
        IEnumerable<string> candidates, IReadOnlyDictionary<string, double> ratings, double overall,
        out double distance, out int compared,
        IReadOnlyDictionary<string, string[]>? splits = null)
    {
        string? best = null;
        distance = double.MaxValue;
        compared = 0;

        foreach (var candidate in candidates)
        {
            if (Find(candidate) is not ArchetypeProfile profile)
            {
                continue;
            }

            var total = 0.0;
            var counted = 0;
            foreach (var (attribute, value) in ratings)
            {
                double expected;
                double scatter;
                if (splits is not null && splits.TryGetValue(attribute, out var across))
                {
                    var parts = across
                        .Select(a => (Ok: profile.TryExpected(a, overall, out var v), Value: v, Part: a))
                        .Where(p => p.Ok)
                        .ToList();
                    if (parts.Count == 0)
                    {
                        continue;
                    }

                    expected = parts.Average(p => p.Value);
                    scatter = parts.Average(p => profile.Spread(p.Part));
                }
                else if (!profile.TryExpected(attribute, overall, out expected))
                {
                    continue;
                }
                else
                {
                    scatter = profile.Spread(attribute);
                }

                // A floor of one point keeps an attribute the game happens to
                // give every player of an archetype identically from deciding
                // the whole comparison on a rounding difference.
                var z = (value - expected) / Math.Max(scatter, 1.0);
                total += z * z;
                counted++;
            }

            if (counted == 0)
            {
                continue;
            }

            var mean = total / counted;
            if (mean < distance)
            {
                distance = mean;
                best = candidate;
                compared = counted;
            }
        }

        if (best is null)
        {
            distance = 0;
        }

        return best;
    }

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
