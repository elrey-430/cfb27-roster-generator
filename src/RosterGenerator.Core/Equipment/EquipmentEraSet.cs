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

    /// <summary>Converts to the pair written into the save.</summary>
    public HeadGear ToHeadGear() => new(Helmet, FaceMask);
}

/// <summary>A span of seasons and the head gear worn during it.</summary>
public sealed class EquipmentEra
{
    /// <summary>Label for reports, e.g. "2010–2016".</summary>
    public string Name { get; init; } = "";

    /// <summary>First season the era covers, inclusive.</summary>
    public int FromSeason { get; init; }

    /// <summary>Last season the era covers, inclusive.</summary>
    public int ToSeason { get; init; }

    /// <summary>The helmet given to every player on the team for this era.</summary>
    public HelmetOption Helmet { get; init; } = new();

    /// <summary>
    /// Other helmets confirmed to work in this era but not assigned
    /// automatically. Real teams were never uniform, so these are here to be
    /// promoted once there is evidence for how common each one was — inventing
    /// a distribution from a handful of demonstrations would be guessing.
    /// </summary>
    public List<HelmetOption> Alternates { get; init; } = new();

    /// <summary>True when this era covers the given season.</summary>
    public bool Covers(int season) => season >= FromSeason && season <= ToSeason;
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
/// <para><b>A season with no era leaves equipment alone.</b> Only helmets that
/// have been confirmed to exist in the game are listed here: writing an asset
/// name the game does not carry would be a guess with a broken helmet at the
/// end of it. Adding an era is a data change, not a code change.</para>
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

    /// <summary>Every era, in file order.</summary>
    public List<EquipmentEra> Eras { get; init; } = new();

    /// <summary>Loads the era set from a JSON file.</summary>
    public static EquipmentEraSet Load(string path)
    {
        var set = JsonSerializer.Deserialize<EquipmentEraSet>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"'{path}' did not contain an equipment era set.");

        foreach (var era in set.Eras)
        {
            if (string.IsNullOrWhiteSpace(era.Helmet.Helmet) ||
                string.IsNullOrWhiteSpace(era.Helmet.FaceMask))
            {
                throw new InvalidDataException(
                    $"Era '{era.Name}' in '{path}' must name both a helmet and a face mask: a helmet " +
                    "written without a mask that fits it leaves a mismatched combination in the save.");
            }
        }

        return set;
    }

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
