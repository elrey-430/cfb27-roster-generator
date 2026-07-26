namespace RosterGenerator.Core.Rating;

/// <summary>How well a player performed one role, and whether it was their job.</summary>
/// <param name="Role">Role name (passing, rushing, receiving, ...).</param>
/// <param name="Score">Production score on the overall scale.</param>
/// <param name="IsPrimary">False for a role the position is not chiefly judged on.</param>
/// <param name="Description">The statistics behind the score, for the report.</param>
public sealed record RoleProduction(string Role, double Score, bool IsPrimary, string Description);

/// <summary>
/// Moves the attributes a player's production was earned with.
///
/// The archetype decides what kind of player someone is, and it decides well —
/// the selector reads the same statistics a person would. What it cannot do is
/// separate two players of the same archetype, so a back who caught 37 passes
/// came out identical to one who caught none. This closes that gap: each role
/// a player produced in raises the attributes that role is performed with.
///
/// Two rules keep it honest.
///
/// The size of every nudge is <b>measured, not chosen</b>. It is expressed in
/// standard deviations of the spread the game itself shows among its own
/// players of that archetype at that overall
/// (<see cref="ArchetypeProfile.Spread"/>), so a nudge can never push a player
/// somewhere the game does not put players like them. Where the spread was not
/// measurable the nudge is zero rather than a guess.
///
/// It only ever moves <b>upward</b>. Historical rosters arrive with whatever
/// box scores survived, and a player must not be marked down for a statistic
/// nobody recorded in 1968. Shaping downward is the archetype's job.
/// </summary>
public sealed class ProductionEmphasis
{
    private readonly ProductionEmphasisModel _model;

    /// <summary>Creates an emphasis pass over a rating model.</summary>
    public ProductionEmphasis(RatingModelSet model)
    {
        _model = model.ProductionEmphasis;
    }

    /// <summary>True when the model file carries no emphasis configuration.</summary>
    public bool IsEmpty => _model.Roles.Count == 0 || _model.Groups.Count == 0;

    /// <summary>
    /// Scores every role a position group can be judged on, skipping the ones
    /// the player has no statistics for.
    /// </summary>
    public IReadOnlyList<RoleProduction> Score(string group, IReadOnlyDictionary<string, double> stats)
    {
        if (!_model.Groups.TryGetValue(group, out var use))
        {
            return Array.Empty<RoleProduction>();
        }

        var scored = new List<RoleProduction>();
        foreach (var (role, primary) in use.Primary.Select(r => (r, true))
                     .Concat(use.Secondary.Select(r => (r, false))))
        {
            if (_model.Roles.TryGetValue(role, out var definition) &&
                TryScore(definition, stats, out var score, out var description))
            {
                scored.Add(new RoleProduction(role, score, primary, description));
            }
        }

        return scored;
    }

    /// <summary>
    /// Overall points a secondary role earns. Roles do not stack — the best one
    /// sets the bonus — so a player is credited for what else they did without
    /// a long stat line becoming a second talent score.
    /// </summary>
    public double SecondaryOverallBonus(IEnumerable<RoleProduction> roles, out string? note)
    {
        note = null;
        double best = 0;
        RoleProduction? source = null;
        var span = _model.SecondaryOverallBonusCeiling - _model.Threshold;
        foreach (var role in roles.Where(r => !r.IsPrimary && r.Score > _model.Threshold))
        {
            var share = span > 0 ? Math.Clamp((role.Score - _model.Threshold) / span, 0, 1) : 0;
            var bonus = _model.SecondaryOverallBonusMax * share;
            if (bonus > best)
            {
                best = bonus;
                source = role;
            }
        }

        if (source is not null && Math.Round(best) >= 1)
        {
            note = $"{source.Role} production ({source.Description}) is not part of what the position is " +
                   $"chiefly rated on, so it was added on top.";
        }

        return best;
    }

    /// <summary>
    /// Applies each role's emphasis to the attribute set. A verified
    /// measurement is never overwritten, and an attribute two roles both claim
    /// takes the larger nudge rather than the sum of them.
    /// </summary>
    public void Apply(
        IReadOnlyList<RoleProduction> roles,
        ArchetypeProfile? profile,
        Dictionary<string, double> attributes,
        IReadOnlySet<string> locked,
        List<string> adjustments)
    {
        if (profile is null || roles.Count == 0)
        {
            return;
        }

        var sigmas = new Dictionary<string, double>(StringComparer.Ordinal);
        var applied = new List<string>();
        foreach (var role in roles)
        {
            if (role.Score <= _model.Threshold || !_model.Roles.TryGetValue(role.Role, out var definition))
            {
                continue;
            }

            var sigma = Math.Min(_model.MaxSigma, (role.Score - _model.Threshold) / _model.ScorePerSigma);
            foreach (var attribute in definition.Attributes)
            {
                if (!attributes.ContainsKey(attribute) || locked.Contains(attribute))
                {
                    continue;
                }

                if (!sigmas.TryGetValue(attribute, out var existing) || sigma > existing)
                {
                    sigmas[attribute] = sigma;
                }
            }

            applied.Add($"{role.Role} ({role.Description}) by {sigma:0.0}");
        }

        var moved = 0;
        foreach (var (attribute, sigma) in sigmas)
        {
            var delta = sigma * profile.Spread(attribute);
            if (delta <= 0)
            {
                continue;
            }

            attributes[attribute] += delta;
            moved++;
        }

        if (moved > 0)
        {
            adjustments.Add(
                $"Production emphasis raised {moved} attribute(s): {string.Join("; ", applied)} standard " +
                "deviation(s) of the spread the game shows among players of this archetype.");
        }
    }

    private bool TryScore(
        ProductionRoleModel role, IReadOnlyDictionary<string, double> stats, out double score, out string description)
    {
        double weighted = 0;
        double used = 0;
        var parts = new List<string>();
        foreach (var stat in role.Stats)
        {
            if (!stats.TryGetValue(stat.Stat, out var value))
            {
                continue;
            }

            weighted += RatingModelSet.Interpolate(stat.Curve, value) * stat.Weight;
            used += stat.Weight;
            parts.Add($"{Format(value)} {stat.Stat}");
        }

        if (used <= 0)
        {
            score = 0;
            description = "";
            return false;
        }

        score = weighted / used;
        description = string.Join(", ", parts);
        return true;
    }

    private static string Format(double value) =>
        Math.Abs(value - Math.Round(value)) < 1e-9 ? ((long)value).ToString() : value.ToString("0.0");
}
