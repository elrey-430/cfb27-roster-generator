using System.Text.Json;
using System.Text.Json.Serialization;

using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Rating;

/// <summary>
/// One EA overall-rating formula, for a (position, playerType) archetype.
/// </summary>
public sealed class OverallFormula
{
    /// <summary>CFB27 position this formula applies to (e.g. "QB", "LT").</summary>
    public string Position { get; init; } = "";

    /// <summary>Archetype this formula applies to (e.g. "QB_FieldGeneral").</summary>
    public string PlayerType { get; init; } = "";

    /// <summary>Constant term of the linear formula.</summary>
    public double Intercept { get; init; }

    /// <summary>Attribute coefficients; attributes absent here do not affect overall.</summary>
    public Dictionary<string, double> Coefficients { get; init; } = new();

    /// <summary>Lowest overall the formula may produce.</summary>
    public int MinimumOverall { get; init; } = 12;

    /// <summary>Highest overall the formula may produce.</summary>
    public int MaximumOverall { get; init; } = 99;

    /// <summary>
    /// Sum of all coefficients — the overall gained per point added to every
    /// contributing attribute. Used to invert the formula during calibration.
    /// </summary>
    public double CoefficientSum => Coefficients.Values.Sum();

    /// <summary>Raw (unrounded, unclamped) overall for a set of attributes.</summary>
    public double ComputeRaw(IReadOnlyDictionary<string, double> attributes)
    {
        var total = Intercept;
        foreach (var (attribute, coefficient) in Coefficients)
        {
            if (attributes.TryGetValue(attribute, out var value))
            {
                total += value * coefficient;
            }
        }

        return total;
    }

    /// <summary>
    /// The final overall: EA rounds to the nearest integer with exact .5
    /// rounding DOWN, then clamps to the formula's bounds.
    /// </summary>
    public int Compute(IReadOnlyDictionary<string, double> attributes)
    {
        var raw = ComputeRaw(attributes);
        var floor = Math.Floor(raw);
        var rounded = Math.Abs(raw - floor - 0.5) < 1e-9
            ? (int)floor
            : (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, MinimumOverall, MaximumOverall);
    }
}

/// <summary>
/// EA's own overall-rating formulas for CFB27, loaded from
/// <c>data/OverallFormulas.json</c>: 79 formulas covering all 21 positions
/// and 59 archetypes, of the form
/// <c>overall = intercept + Σ(rating × coefficient)</c>.
///
/// Using the game's real formula (rather than an invented weighting) means a
/// generated player's overall is exactly what the game itself will display
/// for those attributes, and it lets the engine work backwards: because the
/// formula is linear, the attribute offset needed to hit a target overall is
/// solvable in closed form (see <see cref="Calibrate"/>).
///
/// Verified against the 16,257 real players of a base dynasty export:
/// 99.33% exact, 99.90% within one point. See <c>Ratings/Rating_Model.md</c>
/// for the two known weak spots (FS/S_RunSupport and a few KP_Power rows).
/// </summary>
public sealed class OverallFormulaSet
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Floor on an attribute's share so nothing is entirely frozen.</summary>
    private const double MinimumShare = 0.05;

    private readonly Dictionary<(string Position, string PlayerType), OverallFormula> _byKey;
    private readonly Dictionary<string, OverallFormula> _firstByPosition;

    private OverallFormulaSet(IReadOnlyList<OverallFormula> formulas)
    {
        Formulas = formulas;
        _byKey = new Dictionary<(string, string), OverallFormula>();
        _firstByPosition = new Dictionary<string, OverallFormula>(StringComparer.Ordinal);
        foreach (var formula in formulas)
        {
            _byKey.TryAdd((formula.Position, formula.PlayerType), formula);
            _firstByPosition.TryAdd(formula.Position, formula);
        }
    }

    /// <summary>All loaded formulas.</summary>
    public IReadOnlyList<OverallFormula> Formulas { get; }

    /// <summary>Archetype names available for a position.</summary>
    public IEnumerable<string> PlayerTypesFor(string position) =>
        Formulas.Where(f => f.Position == position).Select(f => f.PlayerType);

    /// <summary>
    /// Finds the formula for a position and archetype. Falls back to the
    /// position's first archetype when the archetype is unknown, so an
    /// unfamiliar save can still be processed.
    /// </summary>
    public OverallFormula Resolve(string position, string? playerType)
    {
        if (playerType is not null && _byKey.TryGetValue((position, playerType), out var formula))
        {
            return formula;
        }

        if (_firstByPosition.TryGetValue(position, out var fallback))
        {
            return fallback;
        }

        throw new KeyNotFoundException(
            $"No overall formula for position '{position}'. OverallFormulas.json covers: " +
            string.Join(", ", _firstByPosition.Keys.Order()));
    }

    /// <summary>
    /// Shifts <paramref name="attributes"/> so the formula produces
    /// <paramref name="targetOverall"/>.
    ///
    /// Because the formula is linear, adding δ to every contributing
    /// attribute changes the overall by δ × Σcoefficients, so the required
    /// δ is <c>(target − current) / Σcoefficients</c> — solved directly, not
    /// searched. <paramref name="clamp"/> re-applies the caller's sanity
    /// bounds after each pass; because clamping can absorb part of the
    /// shift, the solve repeats a few times and then stops, leaving the
    /// closest achievable overall.
    /// </summary>
    /// <param name="formula">The archetype formula to satisfy.</param>
    /// <param name="attributes">Attribute values, modified in place.</param>
    /// <param name="targetOverall">The overall the caller wants.</param>
    /// <param name="clamp">Applies per-attribute sanity bounds.</param>
    /// <param name="locked">Attributes fixed by verified measurements; never shifted.</param>
    /// <param name="share">
    /// Relative willingness of each attribute to absorb the correction —
    /// the position model's talent sensitivity. Null shifts everything equally.
    /// </param>
    /// <param name="maxPasses">Maximum solve/clamp iterations.</param>
    /// <returns>The overall actually achieved.</returns>
    public static int Calibrate(
        OverallFormula formula,
        Dictionary<string, double> attributes,
        int targetOverall,
        Action<Dictionary<string, double>> clamp,
        IReadOnlySet<string>? locked = null,
        Func<string, double>? share = null,
        int maxPasses = 8)
    {
        // Only attributes the formula actually uses can move the overall,
        // and attributes fixed by a verified measurement are never moved —
        // a player with a timed 4.49 forty keeps the speed that time implies.
        var adjustable = formula.Coefficients.Keys
            .Where(a => attributes.ContainsKey(a) && (locked is null || !locked.Contains(a)))
            .ToList();
        if (adjustable.Count == 0)
        {
            return formula.Compute(attributes);
        }

        // The correction is shared out in proportion to how much each
        // attribute is supposed to vary with talent, so the attributes that
        // define a player's quality move first and near-constant physical
        // traits stay put. Without this a uniform shift would hand a
        // no-evidence walk-on receiver elite speed just to reach his overall.
        var shares = adjustable.ToDictionary(a => a, a => Math.Max(share?.Invoke(a) ?? 1.0, MinimumShare));
        var scale = adjustable.Sum(a => formula.Coefficients[a] * shares[a]);
        if (scale <= 0)
        {
            return formula.Compute(attributes);
        }

        for (var pass = 0; pass < maxPasses; pass++)
        {
            var current = formula.ComputeRaw(attributes);
            // Aim a hair below the .5 boundary: EA rounds exact .5 down.
            var step = (targetOverall - 0.25 - current) / scale;
            if (Math.Abs(step) < 1e-6)
            {
                break;
            }

            foreach (var attribute in adjustable)
            {
                attributes[attribute] += step * shares[attribute];
            }

            clamp(attributes);
            if (formula.Compute(attributes) == targetOverall)
            {
                break;
            }
        }

        return formula.Compute(attributes);
    }

    /// <summary>Loads EA's formula file.</summary>
    public static OverallFormulaSet Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("formulas", out var formulas))
        {
            throw new InvalidDataException($"'{path}' has no 'formulas' array.");
        }

        var parsed = formulas.EnumerateArray()
            .Select(f => f.Deserialize<OverallFormula>(JsonOptions)!)
            .ToList();
        if (parsed.Count == 0)
        {
            throw new InvalidDataException($"'{path}' contains no formulas.");
        }

        return new OverallFormulaSet(parsed);
    }
}
