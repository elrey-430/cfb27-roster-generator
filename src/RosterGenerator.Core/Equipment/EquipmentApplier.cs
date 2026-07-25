using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Equipment;

/// <summary>One player's equipment change, for the generation report.</summary>
/// <param name="PlayerName">"Jordan Travis".</param>
/// <param name="Before">What they were wearing.</param>
/// <param name="After">What they were given.</param>
public readonly record struct EquipmentChange(string PlayerName, HeadGear Before, HeadGear After);

/// <summary>What applying an era to a team did.</summary>
public sealed class EquipmentReport
{
    /// <summary>The era applied, or null when the season matched none.</summary>
    public EquipmentEra? Era { get; init; }

    /// <summary>The season that selected the era.</summary>
    public int Season { get; init; }

    /// <summary>Players whose head gear was changed.</summary>
    public List<EquipmentChange> Changed { get; init; } = new();

    /// <summary>Players already wearing the era's helmet, so nothing was written.</summary>
    public int AlreadyCorrect { get; init; }

    /// <summary>
    /// Players whose visuals row could not be found or carried no helmet.
    /// Left alone rather than guessed at.
    /// </summary>
    public List<string> Unresolved { get; init; } = new();

    /// <summary>True when an era matched and equipment was considered at all.</summary>
    public bool Applied => Era is not null;

    /// <summary>Plain-English summary for the generation report.</summary>
    public string Describe()
    {
        if (Era is null)
        {
            return $"Equipment: left as it was — no era covers {Season}.";
        }

        var text = $"Equipment: {Era.Name} — {Era.Helmet.DisplayName} on {Changed.Count} player(s)";
        if (AlreadyCorrect > 0)
        {
            text += $", {AlreadyCorrect} already wearing it";
        }

        if (Unresolved.Count > 0)
        {
            text += $", {Unresolved.Count} with no helmet to change";
        }

        return text + ".";
    }
}

/// <summary>
/// Puts period-correct helmets on a team.
///
/// <para>The season the user is already recreating picks the era, so this
/// costs them nothing extra. Every player on the team is treated the same,
/// including the depth slots the generator filled — a roster where the
/// starters are in 2014 helmets and the walk-ons are in 2027 ones would look
/// worse than leaving it alone.</para>
/// </summary>
public sealed class EquipmentApplier
{
    private readonly EquipmentEraSet _eras;

    /// <summary>Creates an applier over an era set.</summary>
    public EquipmentApplier(EquipmentEraSet eras)
    {
        _eras = eras;
    }

    /// <summary>
    /// Applies the era covering <paramref name="season"/> to every player on
    /// <paramref name="teamIndex"/>. When no era covers the season, nothing is
    /// written and the report says so.
    /// </summary>
    public EquipmentReport Apply(
        PlayerRoster roster, CharacterVisualsTable visuals, int teamIndex, int season)
    {
        var era = _eras.ForSeason(season);
        if (era is null)
        {
            return new EquipmentReport { Era = null, Season = season };
        }

        var target = era.Helmet.ToHeadGear();
        var changed = new List<EquipmentChange>();
        var unresolved = new List<string>();
        var alreadyCorrect = 0;

        foreach (var player in roster.Players.Where(p => p.TeamIndex == teamIndex))
        {
            var rowId = CharacterVisualsReference.RowId(RawVisualsReference(player));
            if (rowId is null)
            {
                unresolved.Add(Name(player));
                continue;
            }

            var before = visuals.GetHeadGear(rowId.Value);
            if (before is null)
            {
                unresolved.Add(Name(player));
                continue;
            }

            if (before.Value == target)
            {
                alreadyCorrect++;
                continue;
            }

            if (visuals.SetHeadGear(rowId.Value, target))
            {
                changed.Add(new EquipmentChange(Name(player), before.Value, target));
            }
            else
            {
                unresolved.Add(Name(player));
            }
        }

        return new EquipmentReport
        {
            Era = era,
            Season = season,
            Changed = changed,
            AlreadyCorrect = alreadyCorrect,
            Unresolved = unresolved,
        };
    }

    private static string Name(Player player) => $"{player.FirstName} {player.LastName}".Trim();

    private static string? RawVisualsReference(Player player) =>
        player.HasColumn(PlayerColumns.CharacterVisuals)
            ? player.GetRaw(PlayerColumns.CharacterVisuals)
            : null;
}
