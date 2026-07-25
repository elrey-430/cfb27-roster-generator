using System.Text.Json;
using System.Text.Json.Serialization;

namespace RosterGenerator.Core.Equipment;

/// <summary>
/// The kind of face mask a position wears. Derived from the game's own
/// convention: over a full base save, kickers wear a kicker cage 92–98% of the
/// time, linemen a cage or multi-bar, quarterbacks an open two-bar.
/// </summary>
public static class MaskRoles
{
    /// <summary>Quarterbacks — the most open mask on the field.</summary>
    public const string Quarterback = "Quarterback";

    /// <summary>Backs, receivers and defensive backs.</summary>
    public const string Skill = "Skill";

    /// <summary>Linebackers.</summary>
    public const string Linebacker = "Linebacker";

    /// <summary>Offensive and defensive linemen — cages and heavy bars.</summary>
    public const string Lineman = "Lineman";

    /// <summary>Kickers and punters.</summary>
    public const string Kicker = "Kicker";
}

/// <summary>A helmet, the mask it wears by default, and any per-role masks.</summary>
public sealed class HelmetOption
{
    /// <summary>Helmet asset name, e.g. <c>GearHelmet_RevolutionSpeed</c>.</summary>
    public string Helmet { get; init; } = "";

    /// <summary>
    /// The mask used when the player's role has no entry of its own. Every
    /// helmet needs one: a shell with no mask is not a valid combination.
    /// </summary>
    public string FaceMask { get; init; } = "";

    /// <summary>Plain-English name for reports, e.g. "Riddell Revolution Speed".</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Mask per <see cref="MaskRoles"/> value. Optional — a shell whose
    /// period-correct masks have not been confirmed yet simply uses
    /// <see cref="FaceMask"/> for everyone, which is what this did before
    /// masks were position-aware.
    /// </summary>
    public Dictionary<string, string> MasksByRole { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Masks to spread across linemen when the era wants variety rather than
    /// one cage for the whole line. Chosen deterministically from the player's
    /// own row, so the same roster always generates the same line.
    /// </summary>
    public List<string> LinemanMaskPool { get; init; } = new();

    /// <summary>True when neither the helmet nor the mask was filled in.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Helmet) && string.IsNullOrWhiteSpace(FaceMask);

    /// <summary>
    /// The head gear for a player in <paramref name="role"/>. The lineman pool
    /// is indexed by <paramref name="seed"/> — the player's visuals row — so
    /// the spread is varied across a line but stable across runs.
    /// </summary>
    public HeadGear ForRole(string role, int seed)
    {
        if (string.Equals(role, MaskRoles.Lineman, StringComparison.OrdinalIgnoreCase)
            && LinemanMaskPool.Count > 0)
        {
            return new HeadGear(Helmet, LinemanMaskPool[Math.Abs(seed) % LinemanMaskPool.Count]);
        }

        return new HeadGear(
            Helmet,
            MasksByRole.TryGetValue(role, out var mask) && !string.IsNullOrWhiteSpace(mask)
                ? mask
                : FaceMask);
    }
}

/// <summary>A span of seasons and what was worn during it.</summary>
public sealed class EquipmentEra
{
    /// <summary>Label for reports, e.g. "2010–2016".</summary>
    public string Name { get; init; } = "";

    /// <summary>First season the era covers, inclusive.</summary>
    public int FromSeason { get; init; }

    /// <summary>Last season the era covers, inclusive.</summary>
    public int ToSeason { get; init; }

    /// <summary>
    /// The era's helmet for each brand. A player keeps their manufacturer and
    /// moves to that manufacturer's model of the day.
    /// </summary>
    public Dictionary<string, HelmetOption> ByBrand { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The era's helmet for each *model* a player might currently wear, which
    /// takes priority over <see cref="ByBrand"/>. Riddell's own line splits in
    /// the 2000s — an Axiom wearer belongs in a VSR-4 and a SpeedFlex wearer in
    /// a Revolution — so brand alone is not always specific enough.
    /// </summary>
    public Dictionary<string, HelmetOption> ByModel { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a player gets when neither their model nor their brand is listed —
    /// a brand that did not exist yet, most often.
    /// </summary>
    public HelmetOption Fallback { get; init; } = new();

    /// <summary>Jersey cut for the era, e.g. <c>Gear_JerseyStyle_SleeveTight</c>. Optional.</summary>
    public string? Sleeves { get; init; }

    /// <summary>Shoulder pad size for the era, e.g. <c>Medium_Pads</c>. Optional.</summary>
    public string? ShoulderPads { get; init; }

    /// <summary>True when this era covers the given season.</summary>
    public bool Covers(int season) => season >= FromSeason && season <= ToSeason;

    /// <summary>
    /// The helmet for a player currently in <paramref name="currentHelmet"/>:
    /// their exact model if the era names one, else their brand, else the
    /// fallback.
    /// </summary>
    public HelmetOption For(string currentHelmet, string? brand)
    {
        if (ByModel.TryGetValue(currentHelmet, out var byModel) && !byModel.IsEmpty)
        {
            return byModel;
        }

        return brand is not null && ByBrand.TryGetValue(brand, out var byBrand) && !byBrand.IsEmpty
            ? byBrand
            : Fallback;
    }
}

/// <summary>
/// Maps the season being recreated to period-correct equipment
/// (<c>data/EquipmentEras.json</c>).
///
/// <para>A 2013 roster wearing 2027 helmets looks wrong in a way no rating
/// fixes, and the user has already told the generator which season they are
/// recreating — so the era follows from what they picked rather than being
/// another thing to fill in.</para>
///
/// <para><b>Lineage, not uniformity.</b> Each player's current helmet decides
/// what they move to — by exact model where the era distinguishes them, else
/// by manufacturer — so a squad stays as mixed as it started. Face masks
/// follow the player's position, because the game's own rosters put kicker
/// cages on kickers and full cages on linemen.</para>
///
/// <para><b>A season with no era leaves equipment alone</b>, and only asset
/// names confirmed to exist in the game are ever written. Adding an era is a
/// data change, not a code change.</para>
/// </summary>
public sealed class EquipmentEraSet
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Which manufacturer makes each helmet in the game.</summary>
    public Dictionary<string, string> Brands { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Which mask role each CFB27 position belongs to.</summary>
    public Dictionary<string, string> PositionRoles { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every era, in file order.</summary>
    public List<EquipmentEra> Eras { get; init; } = new();

    /// <summary>Loads the era set from a JSON file.</summary>
    public static EquipmentEraSet Load(string path)
    {
        var set = JsonSerializer.Deserialize<EquipmentEraSet>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"'{path}' did not contain an equipment era set.");

        foreach (var era in set.Eras)
        {
            var options = era.ByBrand.Values
                .Concat(era.ByModel.Values)
                .Append(era.Fallback);

            foreach (var option in options.Where(o => !o.IsEmpty))
            {
                if (string.IsNullOrWhiteSpace(option.Helmet) ||
                    string.IsNullOrWhiteSpace(option.FaceMask))
                {
                    throw new InvalidDataException(
                        $"Era '{era.Name}' in '{path}' names a helmet without a face mask, or the other " +
                        "way round. Both are required: a mask is moulded to a shell, so writing one " +
                        "without the other leaves a mismatched combination in the save.");
                }
            }

            if (era.Fallback.IsEmpty)
            {
                throw new InvalidDataException(
                    $"Era '{era.Name}' in '{path}' has no fallback helmet. A player whose brand did not " +
                    "exist in the era has no same-brand model to move to and needs one to fall back on.");
            }
        }

        return set;
    }

    /// <summary>The manufacturer of a helmet, or null when it is not listed.</summary>
    public string? BrandOf(string helmetAssetName) =>
        Brands.TryGetValue(helmetAssetName, out var brand) ? brand : null;

    /// <summary>
    /// The mask role a CFB27 position belongs to, defaulting to
    /// <see cref="MaskRoles.Skill"/> for anything unlisted.
    /// </summary>
    public string RoleOf(string position) =>
        PositionRoles.TryGetValue(position, out var role) ? role : MaskRoles.Skill;

    /// <summary>
    /// The era covering <paramref name="season"/>, or null when none does —
    /// in which case equipment is left exactly as the save had it.
    /// </summary>
    public EquipmentEra? ForSeason(int season) => Eras.FirstOrDefault(e => e.Covers(season));

    /// <summary>The seasons covered by some era, for messages that offer help.</summary>
    public string CoveredSeasons() =>
        Eras.Count == 0
            ? "none"
            : string.Join(", ", Eras
                .OrderBy(e => e.FromSeason)
                .Select(e => e.FromSeason == e.ToSeason
                    ? e.FromSeason.ToString()
                    : $"{e.FromSeason}–{e.ToSeason}"));
}
