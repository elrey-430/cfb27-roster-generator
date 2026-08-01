using System.Text.Json;

namespace RosterGenerator.Core.Appearance;

/// <summary>
/// Chooses a player's body build from their position, height and weight —
/// the three things the tool already has, so nothing is asked of the user.
///
/// <para>The build lives in the Player table's <c>CharacterBodyType</c> column
/// and takes one of five values. <b><c>Freshman</c> is the stored name for the
/// build the game's editor calls Lean</b>; the other four say what they mean.
/// Confirmed by a save in which five named Florida State players were each set
/// to a different build in-game and read back out — Lean wrote
/// <c>Freshman</c>, and the remaining four wrote <c>Thin</c>, <c>Standard</c>,
/// <c>Muscular</c> and <c>Heavy</c>.</para>
///
/// <para><b>Two sources, answering different questions.</b> EA's own player
/// builder says which builds a given height and weight can carry at all, and
/// that is what stops a 175 lb receiver being written as Muscular. The base
/// save's own census says what the game actually puts on each position. Where
/// a position is not in question — a 300 lb guard is Heavy, a defensive end is
/// Muscular — the position decides outright, and doing so reproduces the best
/// score any rule reading these three fields could achieve for that position.
/// Where it is a genuine choice among the light builds, the builder's envelope
/// decides which of them the player can carry.</para>
/// </summary>
public sealed class BodyTypeModel
{
    private sealed record Band(int To, IReadOnlyList<string> Allow);

    private sealed record PositionRule(string? Always, IReadOnlyList<string>? Prefer);

    private readonly Dictionary<int, IReadOnlyList<Band>> _builder;
    private readonly Dictionary<string, PositionRule> _positions;
    private readonly IReadOnlyList<string> _defaultPrefer;
    private readonly HashSet<string> _lightBuilds;
    private readonly string _aboveTheTable;
    private readonly int _shortest;
    private readonly int _tallest;

    private BodyTypeModel(
        Dictionary<int, IReadOnlyList<Band>> builder,
        Dictionary<string, PositionRule> positions,
        IReadOnlyList<string> defaultPrefer,
        IEnumerable<string> lightBuilds,
        string aboveTheTable,
        IReadOnlyList<string> values)
    {
        _builder = builder;
        _positions = positions;
        _defaultPrefer = defaultPrefer;
        _lightBuilds = new HashSet<string>(lightBuilds, StringComparer.Ordinal);
        _aboveTheTable = aboveTheTable;
        _shortest = builder.Keys.Min();
        _tallest = builder.Keys.Max();
        Values = values;
    }

    /// <summary>Every build the save is known to hold, for validation.</summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>True when this is a build the game itself uses.</summary>
    public bool IsKnownBuild(string bodyType) => Values.Contains(bodyType, StringComparer.Ordinal);

    /// <summary>Reads the rules from <c>data/BodyTypeRules.json</c>.</summary>
    public static BodyTypeModel Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var builder = new Dictionary<int, IReadOnlyList<Band>>();
        foreach (var height in root.GetProperty("builder").EnumerateObject())
        {
            if (!int.TryParse(height.Name, out var inches))
            {
                continue; // the "_comment" key
            }

            builder[inches] = height.Value.EnumerateArray()
                .Select(b => new Band(
                    b.GetProperty("to").GetInt32(),
                    b.GetProperty("allow").EnumerateArray().Select(a => a.GetString()!).ToList()))
                .ToList();
        }

        var positions = new Dictionary<string, PositionRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in root.GetProperty("positions").EnumerateObject())
        {
            positions[position.Name] = new PositionRule(
                position.Value.TryGetProperty("always", out var always) ? always.GetString() : null,
                position.Value.TryGetProperty("prefer", out var prefer)
                    ? prefer.EnumerateArray().Select(p => p.GetString()!).ToList()
                    : null);
        }

        return new BodyTypeModel(
            builder,
            positions,
            root.GetProperty("defaultPrefer").EnumerateArray().Select(p => p.GetString()!).ToList(),
            root.GetProperty("lightBuilds").EnumerateArray().Select(p => p.GetString()!),
            root.GetProperty("aboveTheTable").GetString()!,
            root.GetProperty("values").EnumerateArray().Select(p => p.GetString()!).ToList());
    }

    /// <summary>
    /// The build for a player, or null when height or weight is not usable —
    /// in which case the slot's existing build is left alone rather than
    /// guessed at from position alone.
    /// </summary>
    public string? For(string position, int heightInches, int weightPounds)
    {
        if (heightInches <= 0 || weightPounds <= 0)
        {
            return null;
        }

        var rule = _positions.TryGetValue(position, out var found)
            ? found
            : new PositionRule(null, _defaultPrefer);

        // A position whose build is not in question: ends and tackles are
        // Muscular, the interior line and defensive tackle are Heavy. The
        // builder's envelope is not consulted, and measurement is why — it
        // describes the light builds, and its "Standard/Thin only" band below
        // 220 lb would turn a 215 lb linebacker into a Standard build that the
        // game itself never uses. Applying it to these positions costs six
        // points of agreement and buys nothing.
        if (rule.Always is { } always)
        {
            return always;
        }

        var permitted = Permitted(heightInches, weightPounds);

        // Off the top of the builder's table: heavier than any band covers, so
        // the light builds are out of the question whatever the position.
        if (permitted is null)
        {
            return _aboveTheTable;
        }

        var light = permitted.Where(_lightBuilds.Contains).ToList();
        if (light.Count == 0)
        {
            return permitted.Contains(_aboveTheTable, StringComparer.Ordinal)
                ? _aboveTheTable
                : permitted[0];
        }

        foreach (var wanted in rule.Prefer ?? _defaultPrefer)
        {
            if (light.Contains(wanted, StringComparer.Ordinal))
            {
                return wanted;
            }
        }

        return light[0];
    }

    /// <summary>
    /// What EA's builder permits at this height and weight, or null when the
    /// weight is off the top of the table — which, for every height it covers,
    /// means "too heavy for anything but a big man's build".
    /// </summary>
    public IReadOnlyList<string>? Permitted(int heightInches, int weightPounds)
    {
        var height = Math.Clamp(heightInches, _shortest, _tallest);
        if (!_builder.TryGetValue(height, out var bands))
        {
            return null;
        }

        foreach (var band in bands)
        {
            if (weightPounds <= band.To)
            {
                return band.Allow;
            }
        }

        return null;
    }
}
