using System.Text.Json;

namespace RosterGenerator.Core.Rating;

/// <summary>The abilities decided for one player.</summary>
/// <param name="PhysicalTiers">
/// Slot number (1–5) → tier. Only the slots this player has appear; the rest
/// are left at <c>None</c>.
/// </param>
/// <param name="Mental">Mental ability name → rank, empty for almost everybody.</param>
public readonly record struct PlayerAbilities(
    IReadOnlyDictionary<int, string> PhysicalTiers,
    IReadOnlyDictionary<string, string> Mental)
{
    /// <summary>Nothing at all — the ordinary case.</summary>
    public static PlayerAbilities None { get; } = new(
        new Dictionary<int, string>(), new Dictionary<string, string>());

    /// <summary>True when this player got anything.</summary>
    public bool Any => PhysicalTiers.Count > 0 || Mental.Count > 0;

    /// <summary>"2 Gold, 4 Silver" for the report, or an empty string.</summary>
    public string Describe()
    {
        var physical = PhysicalTiers.OrderBy(p => p.Key).Select(p => $"slot {p.Key} {p.Value}");
        var mental = Mental.OrderBy(m => m.Key).Select(m => $"{m.Key} ({m.Value})");
        return string.Join(", ", physical.Concat(mental));
    }
}

/// <summary>
/// How good a recreated player is in the ability slots their archetype gives
/// them, measured from a base save (<c>data/AbilityModel.json</c>).
///
/// <para><b>What can and cannot be written.</b> The save stores a physical
/// ability as a <em>tier only</em> — <c>PhysicalAbility1..5</c> hold
/// None/Bronze/Silver/Gold/Platinum and nothing else. Which ability slot 3
/// actually <em>is</em> lives in the game's own data, referenced by tables the
/// save does not carry, and it depends on the player's position and archetype:
/// slot 4 on a nose tackle is not slot 4 on a receiver. So this cannot choose
/// a player's abilities, and does not try. It decides <b>how many</b> of their
/// slots are filled, <b>which</b> of them, and <b>at what tier</b> — and the
/// archetype the generator already chose does the rest.</para>
///
/// <para>Mental abilities are the opposite: <c>MentalAbility1..3</c> name the
/// ability outright. They are rare and elite — 2.1% of a base save, and of
/// those, 244 of 248 carry all three — so they are given to almost nobody, and
/// only from the pool the game has been seen giving that position.</para>
///
/// <para><b>Deterministic.</b> Everything is seeded from the player's own
/// roster slot, so a roster regenerates identically rather than rerolling its
/// abilities every run.</para>
/// </summary>
public sealed class AbilityModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly int _band;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<(int Count, double Share)>> _slotCount;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<(string Tier, double Share)>> _tier;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<int>> _slotOrder;
    private readonly IReadOnlyDictionary<int, double> _mentalShare;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _mentalByPosition;
    private readonly IReadOnlyList<(string Rank, double Share)> _mentalRank;

    private AbilityModel(
        int band,
        IReadOnlyDictionary<int, IReadOnlyList<(int, double)>> slotCount,
        IReadOnlyDictionary<int, IReadOnlyList<(string, double)>> tier,
        IReadOnlyDictionary<string, IReadOnlyList<int>> slotOrder,
        IReadOnlyDictionary<int, double> mentalShare,
        IReadOnlyDictionary<string, IReadOnlyList<string>> mentalByPosition,
        IReadOnlyList<(string, double)> mentalRank)
    {
        _band = band;
        _slotCount = slotCount;
        _tier = tier;
        _slotOrder = slotOrder;
        _mentalShare = mentalShare;
        _mentalByPosition = mentalByPosition;
        _mentalRank = mentalRank;
    }

    /// <summary>How many archetypes have a measured slot order.</summary>
    public int ArchetypeCount => _slotOrder.Count;

    /// <summary>A model that gives nobody anything, for when the file is absent.</summary>
    public static AbilityModel Empty { get; } = new(
        5,
        new Dictionary<int, IReadOnlyList<(int, double)>>(),
        new Dictionary<int, IReadOnlyList<(string, double)>>(),
        new Dictionary<string, IReadOnlyList<int>>(),
        new Dictionary<int, double>(),
        new Dictionary<string, IReadOnlyList<string>>(),
        Array.Empty<(string, double)>());

    /// <summary>
    /// Decides one player's abilities.
    /// </summary>
    /// <param name="archetype">The archetype the generator chose; it owns the slot order.</param>
    /// <param name="position">The CFB27 position, for the mental pool.</param>
    /// <param name="overall">The overall the rating engine produced.</param>
    /// <param name="seed">
    /// The player's roster slot key. The same player in the same slot always
    /// gets the same abilities.
    /// </param>
    public PlayerAbilities For(string archetype, string position, int overall, int seed)
    {
        if (_slotCount.Count == 0)
        {
            return PlayerAbilities.None;
        }

        // Two independent draws from one seed, so the number of slots a player
        // gets does not correlate with the tiers they land on.
        var counts = Nearest(_slotCount, overall);
        var slots = counts is null ? 0 : Pick(counts, Roll(seed, 1));
        if (slots <= 0)
        {
            return WithMental(position, overall, seed, new Dictionary<int, string>());
        }

        var order = _slotOrder.TryGetValue(archetype, out var measured) && measured.Count > 0
            ? measured
            : Enumerable.Range(1, 5).ToList();

        var tiers = Nearest(_tier, overall);
        var physical = new Dictionary<int, string>();
        foreach (var slot in order.Take(Math.Min(slots, order.Count)))
        {
            physical[slot] = tiers is null ? "Bronze" : Pick(tiers, Roll(seed, 2 + slot));
        }

        return WithMental(position, overall, seed, physical);
    }

    private PlayerAbilities WithMental(
        string position, int overall, int seed, Dictionary<int, string> physical)
    {
        var mental = new Dictionary<string, string>(StringComparer.Ordinal);
        var share = Nearest(_mentalShare, overall);
        if (share > 0 &&
            _mentalByPosition.TryGetValue(position, out var pool) &&
            pool.Count >= 3 &&
            Roll(seed, 9) < share)
        {
            // All three or none: 244 of the 248 players in a base save who have
            // any mental ability have the full set, so a partial one would be
            // the unusual case rather than the ordinary one.
            var chosen = Distinct(pool, seed, 3);
            for (var i = 0; i < chosen.Count; i++)
            {
                mental[chosen[i]] = _mentalRank.Count == 0
                    ? "Bronze"
                    : Pick(_mentalRank, Roll(seed, 12 + i));
            }
        }

        return new PlayerAbilities(physical, mental);
    }

    /// <summary>
    /// Picks <paramref name="wanted"/> different abilities out of a pool,
    /// deterministically.
    /// </summary>
    private static IReadOnlyList<string> Distinct(IReadOnlyList<string> pool, int seed, int wanted)
    {
        // Ordered by a seeded key rather than shuffled in place, so the result
        // depends only on the seed and the pool's own order.
        return pool
            .Select((name, index) => (name, key: Roll(seed, 20 + index)))
            .OrderBy(x => x.key)
            .ThenBy(x => x.name, StringComparer.Ordinal)
            .Take(Math.Min(wanted, pool.Count))
            .Select(x => x.name)
            .ToList();
    }

    /// <summary>
    /// A stable [0,1) value from a player's slot and a purpose. Splitting one
    /// seed into independent streams keeps "how many slots" from deciding
    /// "which tier" as a side effect.
    /// </summary>
    private static double Roll(int seed, int stream)
    {
        unchecked
        {
            var hash = (uint)seed * 2654435761u + (uint)stream * 2246822519u;
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (hash & 0xFFFFFF) / (double)0x1000000;
        }
    }

    private static T Pick<T>(IReadOnlyList<(T Value, double Share)> weighted, double roll)
    {
        var running = 0.0;
        foreach (var (value, share) in weighted)
        {
            running += share;
            if (roll < running)
            {
                return value;
            }
        }

        return weighted[^1].Value;
    }

    /// <summary>
    /// The measured band covering an overall, or the closest one. An overall
    /// outside everything measured takes the nearest band rather than nothing:
    /// the curve does not stop, the sample does.
    /// </summary>
    private static TValue? Nearest<TValue>(IReadOnlyDictionary<int, TValue> byBand, int overall)
        where TValue : class
    {
        if (byBand.Count == 0)
        {
            return null;
        }

        var band = byBand.Keys.OrderBy(b => Math.Abs(b - overall)).First();
        return byBand[band];
    }

    private static double Nearest(IReadOnlyDictionary<int, double> byBand, int overall) =>
        byBand.Count == 0 ? 0 : byBand[byBand.Keys.OrderBy(b => Math.Abs(b - overall)).First()];

    /// <summary>Loads the measured model.</summary>
    public static AbilityModel Load(string path)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = document.RootElement;
        var band = root.TryGetProperty("overallBand", out var b) ? b.GetInt32() : 5;

        var physical = root.GetProperty("physical");
        var slotCount = new Dictionary<int, IReadOnlyList<(int, double)>>();
        foreach (var entry in physical.GetProperty("slotCountByOverall").EnumerateObject())
        {
            slotCount[int.Parse(entry.Name)] = entry.Value.EnumerateObject()
                .Select(o => (int.Parse(o.Name), o.Value.GetDouble()))
                .OrderBy(x => x.Item1)
                .ToList();
        }

        var tier = new Dictionary<int, IReadOnlyList<(string, double)>>();
        foreach (var entry in physical.GetProperty("tierByOverall").EnumerateObject())
        {
            tier[int.Parse(entry.Name)] = entry.Value.EnumerateObject()
                .Select(o => (o.Name, o.Value.GetDouble()))
                .ToList();
        }

        var order = physical.GetProperty("slotOrderByArchetype").EnumerateObject()
            .ToDictionary(
                e => e.Name,
                e => (IReadOnlyList<int>)e.Value.EnumerateArray().Select(v => v.GetInt32()).ToList(),
                StringComparer.Ordinal);

        var mental = root.GetProperty("mental");
        var share = mental.GetProperty("shareByOverall").EnumerateObject()
            .ToDictionary(e => int.Parse(e.Name), e => e.Value.GetDouble());
        var byPosition = mental.GetProperty("byPosition").EnumerateObject()
            .ToDictionary(
                e => e.Name,
                e => (IReadOnlyList<string>)e.Value.EnumerateArray()
                    .Select(v => v.GetString() ?? "").Where(v => v.Length > 0).ToList(),
                StringComparer.OrdinalIgnoreCase);
        var rank = mental.GetProperty("rankMix").EnumerateObject()
            .Select(e => (e.Name, e.Value.GetDouble()))
            .ToList();

        return new AbilityModel(band, slotCount, tier, order, share, byPosition, rank);
    }
}
