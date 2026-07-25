using System.Text.Json;
using System.Text.Json.Serialization;

namespace RosterGenerator.Core.Equipment;

/// <summary>A helmet and the face mask moulded to fit it.</summary>
public sealed class HelmetOption
{
    /// <summary>Helmet asset name, e.g. <c>GearHelmet_RevolutionSpeed</c>.</summary>
    public string Helmet { get; init; } = "";

    /// <summary>Face mask asset name that fits that shell.</summary>
    public string FaceMask { get; init; } = "";

    /// <summary>Plain-English name for reports, e.g. "Riddell Revolution Speed".</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>True when neither field was filled in.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Helmet) && string.IsNullOrWhiteSpace(FaceMask);

    /// <summary>Converts to the pair written into the save.</summary>
    public HeadGear ToHeadGear() => new(Helmet, FaceMask);
}

/// <summary>A span of seasons and what each helmet brand wore during it.</summary>
public sealed class EquipmentEra
{
    /// <summary>Label for reports, e.g. "2010–2016".</summary>
    public string Name { get; init; } = "";

    /// <summary>First season the era covers, inclusive.</summary>
    public int FromSeason { get; init; }

    /// <summary>Last season the era covers, inclusive.</summary>
    public int ToSeason { get; init; }

    /// <summary>
    /// The era's helmet for each brand, keyed by brand name. A player keeps
    /// their manufacturer and moves to that manufacturer's model of the day —
    /// a Riddell wearer in 2014 wore a Revolution Speed, a Schutt wearer an
    /// Air XP.
    /// </summary>
    public Dictionary<string, HelmetOption> ByBrand { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a player gets when their brand did not exist in this era. Vicis
    /// shipped nothing until 2016 and Light little earlier, so a player in one
    /// of those has no same-brand equivalent to move to and takes the era's
    /// most common helmet instead.
    /// </summary>
    public HelmetOption Fallback { get; init; } = new();

    /// <summary>True when this era covers the given season.</summary>
    public bool Covers(int season) => season >= FromSeason && season <= ToSeason;

    /// <summary>
    /// The helmet a player in <paramref name="brand"/> should wear, or the
    /// fallback when the brand has no entry.
    /// </summary>
    public HelmetOption ForBrand(string? brand) =>
        brand is not null && ByBrand.TryGetValue(brand, out var option) && !option.IsEmpty
            ? option
            : Fallback;
}

/// <summary>
/// Maps the season being recreated to period-correct head gear
/// (<c>data/EquipmentEras.json</c>).
///
/// <para>A 2013 roster wearing 2027 helmets looks wrong in a way no rating
/// fixes, and the user has already told the generator which season they are
/// recreating — so the era follows from what they picked rather than being
/// another thing to fill in.</para>
///
/// <para><b>Brand carries over; the model changes.</b> Giving a whole team one
/// helmet would flatten a squad that was never uniform. Instead each player's
/// current helmet names a manufacturer, and they move to that manufacturer's
/// model for the era: Riddell to Riddell, Schutt to Schutt. Players in a brand
/// that did not exist yet — Vicis, Light — take the era's fallback, because
/// there is no same-brand answer to give them.</para>
///
/// <para><b>A season with no era leaves equipment alone.</b> Only helmets
/// confirmed to exist in the game are listed here: writing an asset name the
/// game does not carry would be a guess with a broken helmet at the end of it.
/// Adding an era is a data change, not a code change.</para>
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

    /// <summary>Every era, in file order.</summary>
    public List<EquipmentEra> Eras { get; init; } = new();

    /// <summary>Loads the era set from a JSON file.</summary>
    public static EquipmentEraSet Load(string path)
    {
        var set = JsonSerializer.Deserialize<EquipmentEraSet>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"'{path}' did not contain an equipment era set.");

        foreach (var era in set.Eras)
        {
            foreach (var option in era.ByBrand.Values.Append(era.Fallback))
            {
                if (option.IsEmpty)
                {
                    continue;
                }

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
                    $"Era '{era.Name}' in '{path}' has no fallback helmet. Brands that did not exist in " +
                    "the era — Vicis and Light, for the 2010s — have no same-brand model to move to and " +
                    "need one to fall back on.");
            }
        }

        return set;
    }

    /// <summary>The manufacturer of a helmet, or null when it is not listed.</summary>
    public string? BrandOf(string helmetAssetName) =>
        Brands.TryGetValue(helmetAssetName, out var brand) ? brand : null;

    /// <summary>
    /// The era covering <paramref name="season"/>, or null when none does —
    /// in which case equipment is left exactly as the save had it.
    /// </summary>
    public EquipmentEra? ForSeason(int season) => Eras.FirstOrDefault(e => e.Covers(season));

    /// <summary>The seasons covered by some era, for messages that offer help.</summary>
    public string CoveredSeasons() =>
        Eras.Count == 0
            ? "none"
            : string.Join(", ", Eras.Select(e => e.FromSeason == e.ToSeason
                ? e.FromSeason.ToString()
                : $"{e.FromSeason}–{e.ToSeason}"));
}
