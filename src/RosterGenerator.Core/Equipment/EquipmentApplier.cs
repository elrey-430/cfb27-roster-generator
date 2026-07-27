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
    /// <summary>
    /// The era applied, or null when the season matched none — and also null on
    /// a merged report whose teams fell in different eras, where
    /// <see cref="EraNames"/> lists them instead.
    /// </summary>
    public EquipmentEra? Era { get; init; }

    /// <summary>
    /// Names of every era applied. One entry for a single season; more only
    /// when a merged report spans seasons that land in different eras.
    /// </summary>
    public IReadOnlyList<string> EraNames { get; init; } = Array.Empty<string>();

    /// <summary>The season that selected the era.</summary>
    public int Season { get; init; }

    /// <summary>How many teams this report covers.</summary>
    public int TeamCount { get; init; } = 1;

    /// <summary>Players whose head gear was changed.</summary>
    public List<EquipmentChange> Changed { get; init; } = new();

    /// <summary>Players already wearing the era's helmet, so nothing was written.</summary>
    public int AlreadyCorrect { get; init; }

    /// <summary>Players whose jersey cut was changed to the era's.</summary>
    public int SleevesChanged { get; init; }

    /// <summary>Players whose shoulder pads were changed to the era's.</summary>
    public int ShoulderPadsChanged { get; init; }

    /// <summary>
    /// Players whose visuals row could not be found or carried no helmet.
    /// Left alone rather than guessed at.
    /// </summary>
    public List<string> Unresolved { get; init; } = new();

    /// <summary>True when an era matched and equipment was considered at all.</summary>
    public bool Applied => Era is not null || EraNames.Count > 0;

    /// <summary>
    /// Folds the per-team reports of a whole-season run into one, so the
    /// caller's summary counts every team rather than only the first.
    /// </summary>
    public static EquipmentReport Merge(IReadOnlyList<EquipmentReport> parts)
    {
        if (parts.Count == 1)
        {
            return parts[0];
        }

        var applied = parts.Where(p => p.Era is not null).ToList();
        var names = applied.SelectMany(p => p.EraNames.Count > 0
                ? p.EraNames
                : new[] { p.Era!.Name })
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new EquipmentReport
        {
            // One era across the whole file is the ordinary case and keeps the
            // rest of the summary — sleeves, pads — able to name what it did.
            Era = names.Count == 1 ? applied[0].Era : null,
            EraNames = names,
            Season = parts[0].Season,
            TeamCount = parts.Sum(p => p.TeamCount),
            Changed = parts.SelectMany(p => p.Changed).ToList(),
            AlreadyCorrect = parts.Sum(p => p.AlreadyCorrect),
            SleevesChanged = parts.Sum(p => p.SleevesChanged),
            ShoulderPadsChanged = parts.Sum(p => p.ShoulderPadsChanged),
            Unresolved = parts.SelectMany(p => p.Unresolved).ToList(),
        };
    }

    /// <summary>Plain-English summary for the generation report.</summary>
    public string Describe()
    {
        if (Era is null && EraNames.Count == 0)
        {
            return $"Equipment: left as it was — no era covers {Season}.";
        }

        var byHelmet = Changed
            .GroupBy(c => c.After.Helmet.Replace("GearHelmet_", ""))
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} x {g.Key}");

        var era = Era?.Name ?? string.Join(" + ", EraNames);
        var scope = TeamCount > 1 ? $" across {TeamCount} teams" : "";
        var text = $"Equipment: {era}{scope} — {Changed.Count} player(s) rehelmeted"
                   + (Changed.Count > 0 ? $" ({string.Join(", ", byHelmet)})" : "");
        if (AlreadyCorrect > 0)
        {
            text += $", {AlreadyCorrect} already wearing it";
        }

        if (Unresolved.Count > 0)
        {
            text += $", {Unresolved.Count} with no helmet to change";
        }

        text += ".";

        // Named only when one era is in play; a merged report spanning eras has
        // no single cut or pad size to name, so it just gives the count.
        if (SleevesChanged > 0)
        {
            var cut = Era?.Sleeves?.Replace("Gear_JerseyStyle_", "");
            text += cut is { Length: > 0 }
                ? $" Jersey cut: {cut} on {SleevesChanged} player(s)."
                : $" Jersey cut changed on {SleevesChanged} player(s).";
        }

        if (ShoulderPadsChanged > 0)
        {
            var pads = Era?.ShoulderPads?.Replace("_Pads", "");
            text += pads is { Length: > 0 }
                ? $" Shoulder pads: {pads} on {ShoulderPadsChanged} player(s)."
                : $" Shoulder pads changed on {ShoulderPadsChanged} player(s).";
        }

        return text;
    }
}

/// <summary>
/// Puts period-correct helmets on a team.
///
/// <para>The season the user is already recreating picks the era, so this
/// costs them nothing extra. Every player on the team is covered, including
/// the depth slots the generator filled — a roster where the starters are in
/// 2014 helmets and the walk-ons are in 2027 ones would look worse than
/// leaving it alone.</para>
///
/// <para><b>Each player is moved within their own brand.</b> Their current
/// helmet names a manufacturer and they take that manufacturer's model for
/// the era, so a squad stays as mixed as it started rather than collapsing
/// into 85 identical helmets. Only players whose brand did not exist yet fall
/// back to the era's common shell, because there is no same-brand answer to
/// give them.</para>
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
        PlayerRoster roster, CharacterVisualsTable visuals, int teamIndex, int season) =>
        Apply(roster, visuals, new[] { teamIndex }, season);

    /// <summary>
    /// Applies the era covering <paramref name="season"/> to every player on
    /// any of <paramref name="teamIndexes"/> — a whole season's worth of teams
    /// in one pass over the roster.
    /// </summary>
    public EquipmentReport Apply(
        PlayerRoster roster,
        CharacterVisualsTable visuals,
        IReadOnlyCollection<int> teamIndexes,
        int season)
    {
        var teams = teamIndexes.ToHashSet();
        var era = _eras.ForSeason(season);
        if (era is null)
        {
            return new EquipmentReport { Era = null, Season = season, TeamCount = teams.Count };
        }

        var changed = new List<EquipmentChange>();
        var unresolved = new List<string>();
        var alreadyCorrect = 0;
        var sleeves = 0;
        var pads = 0;

        foreach (var player in roster.Players.Where(p => teams.Contains(p.TeamIndex)))
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

            // The helmet they are wearing decides which model they move to --
            // by exact model where the era distinguishes them, else by brand,
            // else the fallback. The mask then follows their position.
            var option = era.For(before.Value.Helmet, _eras.BrandOf(before.Value.Helmet));
            var target = option.ForRole(_eras.RoleOf(player.Position), rowId.Value);

            if (before.Value == target)
            {
                alreadyCorrect++;
            }
            else if (visuals.SetHeadGear(rowId.Value, target))
            {
                changed.Add(new EquipmentChange(Name(player), before.Value, target));
            }
            else
            {
                unresolved.Add(Name(player));
            }

            // Jersey cut and pad size are era-wide rather than per player:
            // sleeves got tighter and pads smaller over time for everyone.
            if (era.Sleeves is { Length: > 0 } sleeveStyle
                && visuals.GetJerseyStyle(rowId.Value) != sleeveStyle
                && visuals.SetJerseyStyle(rowId.Value, sleeveStyle))
            {
                sleeves++;
            }

            if (era.ShoulderPads is { Length: > 0 } padSize
                && visuals.GetShoulderPads(rowId.Value) != padSize
                && visuals.SetShoulderPads(rowId.Value, padSize))
            {
                pads++;
            }
        }

        return new EquipmentReport
        {
            Era = era,
            EraNames = new[] { era.Name },
            Season = season,
            TeamCount = teams.Count,
            Changed = changed,
            AlreadyCorrect = alreadyCorrect,
            Unresolved = unresolved,
            SleevesChanged = sleeves,
            ShoulderPadsChanged = pads,
        };
    }

    private static string Name(Player player) => $"{player.FirstName} {player.LastName}".Trim();

    private static string? RawVisualsReference(Player player) =>
        player.HasColumn(PlayerColumns.CharacterVisuals)
            ? player.GetRaw(PlayerColumns.CharacterVisuals)
            : null;
}
