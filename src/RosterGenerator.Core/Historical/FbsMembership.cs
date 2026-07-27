using System.Text.Json;

namespace RosterGenerator.Core.Historical;

/// <summary>One school's spell in the top division.</summary>
public sealed class FbsSchoolMembership
{
    /// <summary>First season in Division I-A / FBS, or null for "always".</summary>
    public int? FirstSeason { get; init; }

    /// <summary>Inclusive <c>[from, to]</c> season ranges the school did not play.</summary>
    public List<int[]> SkippedSeasons { get; init; } = new();

    /// <summary>Why the seasons were skipped, for the report.</summary>
    public string? Note { get; init; }
}

/// <summary>Why a school could not have fielded a team in a season.</summary>
/// <param name="School">The school, spelled as CFB27 spells it.</param>
/// <param name="Season">The season asked for.</param>
/// <param name="Reason">Plain-English explanation for the report.</param>
public sealed record FbsMembershipProblem(string School, int Season, string Reason)
{
    /// <summary>
    /// <see cref="Reason"/> with the leading school name removed, for a caller
    /// that has already named the school itself.
    /// </summary>
    public string Detail => Reason.StartsWith(School + " ", StringComparison.Ordinal)
        ? Reason[(School.Length + 1)..]
        : Reason;
}

/// <summary>
/// Which seasons each school actually fielded an FBS team, from
/// <c>data/FbsMembership.json</c>.
///
/// <para>CFB27 ships 138 teams as they stand today, which is not who was
/// playing in 1985. Sacramento State is in that 138 and did not reach the top
/// division until 2026; a 2010 roster that includes them is a mistake, and one
/// nobody wants to discover after filling in 85 rows.</para>
///
/// <para><b>Advisory, not a gate.</b> A school outside its seasons is reported
/// and still generated. The tool does not know every reclassification in
/// history, the file is the user's to edit, and refusing to build somebody's
/// roster over a date this project got wrong would be the worse failure.</para>
/// </summary>
public sealed class FbsMembership
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, FbsSchoolMembership> _schools;

    private FbsMembership(Dictionary<string, FbsSchoolMembership> schools, int defaultFirstSeason)
    {
        _schools = schools;
        DefaultFirstSeason = defaultFirstSeason;
    }

    /// <summary>
    /// The season from which an unlisted school is assumed to have been FBS.
    /// Every school CFB27 carries that is not listed has been in the top
    /// division since at least the 1978 Division I-A split.
    /// </summary>
    public int DefaultFirstSeason { get; }

    /// <summary>Schools with a recorded transition or gap.</summary>
    public IReadOnlyCollection<string> Schools => _schools.Keys;

    /// <summary>An empty set, which treats every school as always eligible.</summary>
    public static FbsMembership Empty { get; } =
        new(new Dictionary<string, FbsSchoolMembership>(StringComparer.OrdinalIgnoreCase), 0);

    /// <summary>
    /// Why this school could not have played in this season, or null when it
    /// could have — including when nothing is known about it.
    /// </summary>
    public FbsMembershipProblem? Check(string school, int season)
    {
        if (season <= 0 || !_schools.TryGetValue(school.Trim(), out var membership))
        {
            return null;
        }

        if (membership.FirstSeason is int first && season < first)
        {
            return new FbsMembershipProblem(school, season,
                $"{school} did not field an FBS team until {first}");
        }

        foreach (var range in membership.SkippedSeasons)
        {
            if (range.Length == 2 && season >= range[0] && season <= range[1])
            {
                var reason = membership.Note is { Length: > 0 } note
                    ? $"{school} did not play in {season}: {note}"
                    : $"{school} did not field a team in {season}";
                return new FbsMembershipProblem(school, season, reason);
            }
        }

        return null;
    }

    /// <summary>True when the school could have fielded a team that season.</summary>
    public bool Eligible(string school, int season) => Check(school, season) is null;

    /// <summary>
    /// Filters a list of schools down to those that could have played in a
    /// season, keeping the order given.
    /// </summary>
    public IReadOnlyList<string> EligibleIn(IEnumerable<string> schools, int season) =>
        schools.Where(s => Eligible(s, season)).ToList();

    /// <summary>Loads the membership file.</summary>
    public static FbsMembership Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("schools", out var schools))
        {
            throw new InvalidDataException($"'{path}' has no 'schools' object.");
        }

        var parsed = schools.EnumerateObject()
            .ToDictionary(
                p => p.Name,
                p => p.Value.Deserialize<FbsSchoolMembership>(JsonOptions)!,
                StringComparer.OrdinalIgnoreCase);
        var fallback = root.TryGetProperty("defaultFirstSeason", out var value) ? value.GetInt32() : 0;
        return new FbsMembership(parsed, fallback);
    }
}
