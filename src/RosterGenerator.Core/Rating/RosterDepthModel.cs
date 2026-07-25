using System.Text.Json;
using System.Text.Json.Serialization;

namespace RosterGenerator.Core.Rating;

/// <summary>The class-year mix observed over a span of roster ranks.</summary>
public sealed class RankBandClassModel
{
    /// <summary>First roster rank in the band (1 = the team's best player).</summary>
    public int FromRank { get; init; }

    /// <summary>Last roster rank in the band.</summary>
    public int ToRank { get; init; }

    /// <summary>Observed share of each <c>SchoolYear</c> value in the band.</summary>
    public Dictionary<string, double> Weights { get; init; } = new();
}

/// <summary>
/// What the *shape* of a full CFB27 roster looks like, measured from an
/// untouched base save (<c>data/RosterDepth.json</c>).
///
/// A real team carries 85 players and only ever fields a handful; the tail is
/// young and lightly rated. Those two facts are what let the generator fill
/// slots a user did not supply without inventing numbers: a filler at roster
/// rank 80 is given what the game itself puts at rank 80.
/// </summary>
public sealed class RosterDepthModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Players per team in the observed save (85 on every FBS team).</summary>
    public int RosterSize { get; init; } = 85;

    /// <summary>
    /// Median overall across every live player in the save — the rating a
    /// nondescript player at a nondescript program carries.
    /// <see cref="ProgramAdjustment"/> measures a team against it.
    /// </summary>
    public int LeagueMedianOverall { get; init; } = 69;

    /// <summary>Largest program adjustment allowed in either direction.</summary>
    public int MaxProgramAdjustment { get; init; } = 8;

    /// <summary>
    /// Overall no filler is taken below, whatever the curve and ceilings say.
    /// The weakest player observed on any roster rated 41, so this stays
    /// inside real data while ruling out absurd results on a roster where the
    /// user supplied only a handful of very weak players.
    /// </summary>
    public int MinimumOverall { get; init; } = 45;

    /// <summary>
    /// Points a filler is held below the weakest historical player at its
    /// position. One point is enough to settle the depth chart, and staying
    /// close keeps the roster's rating curve smooth.
    /// </summary>
    public int MarginBelowHistorical { get; init; } = 1;

    /// <summary>Median overall by roster rank; index 0 is the team's best player.</summary>
    public List<int> MedianOverallByRank { get; init; } = new();

    /// <summary>Class-year mix per rank band.</summary>
    public List<RankBandClassModel> ClassYearByRankBand { get; init; } = new();

    /// <summary>Loads the model from <c>data/RosterDepth.json</c>.</summary>
    public static RosterDepthModel Load(string path) =>
        JsonSerializer.Deserialize<RosterDepthModel>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"'{path}' does not contain a roster depth model.");

    /// <summary>
    /// Median overall at a one-based roster rank. Ranks past the end of the
    /// measured curve reuse its last value rather than extrapolating downwards
    /// — a roster deeper than the game's own has no measured shape.
    /// </summary>
    public int OverallAtRank(int rank)
    {
        if (MedianOverallByRank.Count == 0)
        {
            return MinimumOverall;
        }

        var index = Math.Clamp(rank - 1, 0, MedianOverallByRank.Count - 1);
        return MedianOverallByRank[index];
    }

    /// <summary>
    /// How much better or worse than a typical program a team is, in overall
    /// points, from the median of the roster it already carries.
    ///
    /// Role, awards and statistics say what a player did; none of them say
    /// where. A backup cornerback at a playoff program and a backup
    /// cornerback at the worst team in the country are not the same player,
    /// and rating both from the same league-average role score is what made
    /// the generated 2023 Florida State roster collapse through its middle —
    /// 69 at roster rank 30 where the game's own Florida State carries 76.
    /// The donor roster already encodes the program's tier, so no new input
    /// is needed from the user.
    /// </summary>
    public int ProgramAdjustment(IEnumerable<int> donorTeamOveralls)
    {
        var sorted = donorTeamOveralls.OrderBy(o => o).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var median = sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;
        return Math.Clamp(median - LeagueMedianOverall, -MaxProgramAdjustment, MaxProgramAdjustment);
    }

    /// <summary>
    /// Class-year weights covering a one-based roster rank, or an empty map
    /// when no band does.
    /// </summary>
    public IReadOnlyDictionary<string, double> ClassWeightsAtRank(int rank)
    {
        foreach (var band in ClassYearByRankBand)
        {
            if (rank >= band.FromRank && rank <= band.ToRank)
            {
                return band.Weights;
            }
        }

        return ClassYearByRankBand.Count > 0
            ? ClassYearByRankBand[^1].Weights
            : new Dictionary<string, double>();
    }
}
