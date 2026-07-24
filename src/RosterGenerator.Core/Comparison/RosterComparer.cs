using System.Text;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Comparison;

/// <summary>One field difference between two versions of the same player.</summary>
/// <param name="Column">CSV column name.</param>
/// <param name="LeftValue">Value in the left roster.</param>
/// <param name="RightValue">Value in the right roster.</param>
public sealed record FieldDifference(string Column, string LeftValue, string RightValue);

/// <summary>A player found in both rosters, with any field differences.</summary>
public sealed class MatchedPlayer
{
    /// <summary>Creates a match between two player rows.</summary>
    public MatchedPlayer(Player left, Player right)
    {
        Left = left;
        Right = right;
    }

    /// <summary>The player's row in the left roster.</summary>
    public Player Left { get; }

    /// <summary>The player's row in the right roster.</summary>
    public Player Right { get; }

    /// <summary>Differences over the compared columns (empty = identical).</summary>
    public List<FieldDifference> Differences { get; } = new();
}

/// <summary>Result of comparing one team's roster across two Player tables.</summary>
public sealed class ComparisonReport
{
    /// <summary>Creates a report shell.</summary>
    public ComparisonReport(string leftLabel, string rightLabel, int teamId)
    {
        LeftLabel = leftLabel;
        RightLabel = rightLabel;
        TeamId = teamId;
    }

    /// <summary>Display name for the left roster (e.g. "Generated").</summary>
    public string LeftLabel { get; }

    /// <summary>Display name for the right roster (e.g. "Manual export").</summary>
    public string RightLabel { get; }

    /// <summary>The compared team index.</summary>
    public int TeamId { get; }

    /// <summary>Players present in both rosters (matched by name).</summary>
    public List<MatchedPlayer> Matched { get; } = new();

    /// <summary>Players only in the left roster.</summary>
    public List<Player> OnlyInLeft { get; } = new();

    /// <summary>Players only in the right roster.</summary>
    public List<Player> OnlyInRight { get; } = new();

    /// <summary>Renders a Markdown summary of the comparison.</summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Roster comparison — team {TeamId}");
        sb.AppendLine();
        sb.AppendLine($"- **Left:** {LeftLabel}");
        sb.AppendLine($"- **Right:** {RightLabel}");
        sb.AppendLine($"- **Matched players:** {Matched.Count} " +
                      $"({Matched.Count(m => m.Differences.Count == 0)} identical on compared fields)");
        sb.AppendLine($"- **Only in left:** {OnlyInLeft.Count}");
        sb.AppendLine($"- **Only in right:** {OnlyInRight.Count}");
        sb.AppendLine();

        var withDiffs = Matched.Where(m => m.Differences.Count > 0).ToList();
        if (withDiffs.Count > 0)
        {
            sb.AppendLine("## Field differences");
            sb.AppendLine();
            sb.AppendLine("| Player | Column | Left | Right |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var match in withDiffs)
            {
                foreach (var diff in match.Differences)
                {
                    sb.AppendLine($"| {match.Left.FirstName} {match.Left.LastName} | {diff.Column} | " +
                                  $"{diff.LeftValue} | {diff.RightValue} |");
                }
            }

            sb.AppendLine();
        }

        if (OnlyInLeft.Count > 0)
        {
            sb.AppendLine($"## Only in {LeftLabel}");
            sb.AppendLine();
            foreach (var player in OnlyInLeft)
            {
                sb.AppendLine($"- {player} — {player.Position}");
            }

            sb.AppendLine();
        }

        if (OnlyInRight.Count > 0)
        {
            sb.AppendLine($"## Only in {RightLabel}");
            sb.AppendLine();
            foreach (var player in OnlyInRight)
            {
                sb.AppendLine($"- {player} — {player.Position}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Field-by-field comparison of one team's roster across two Player tables
/// (e.g. the generated 2023 FSU CSV vs a manually created dynasty export).
/// Players are matched by normalized full name; the compared column set is
/// caller-configurable and defaults to the confirmed-safe identity fields.
/// </summary>
public sealed class RosterComparer
{
    /// <summary>Columns compared when the caller does not specify a set.</summary>
    public static readonly IReadOnlyList<string> DefaultColumns = new[]
    {
        PlayerColumns.JerseyNum, PlayerColumns.Position, PlayerColumns.Height,
        PlayerColumns.SchoolYear, PlayerColumns.RedshirtStatus,
    };

    /// <summary>
    /// Compares <paramref name="teamId"/>'s players between two rosters.
    /// </summary>
    /// <param name="left">Left roster (typically the generated one).</param>
    /// <param name="right">Right roster (typically the manual benchmark).</param>
    /// <param name="teamId">Team index to compare.</param>
    /// <param name="leftLabel">Label for the left roster in the report.</param>
    /// <param name="rightLabel">Label for the right roster in the report.</param>
    /// <param name="columns">Columns to diff; null = <see cref="DefaultColumns"/>.</param>
    public ComparisonReport Compare(
        PlayerRoster left,
        PlayerRoster right,
        int teamId,
        string leftLabel = "Left",
        string rightLabel = "Right",
        IReadOnlyList<string>? columns = null)
    {
        columns ??= DefaultColumns;
        var report = new ComparisonReport(leftLabel, rightLabel, teamId);

        var leftPlayers = TeamPlayersByName(left, teamId);
        var rightPlayers = TeamPlayersByName(right, teamId);

        foreach (var (name, leftPlayer) in leftPlayers)
        {
            if (rightPlayers.TryGetValue(name, out var rightPlayer))
            {
                var match = new MatchedPlayer(leftPlayer, rightPlayer);
                foreach (var column in columns)
                {
                    var leftValue = leftPlayer.GetRaw(column);
                    var rightValue = rightPlayer.GetRaw(column);
                    if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                    {
                        match.Differences.Add(new FieldDifference(column, leftValue, rightValue));
                    }
                }

                report.Matched.Add(match);
            }
            else
            {
                report.OnlyInLeft.Add(leftPlayer);
            }
        }

        foreach (var (name, rightPlayer) in rightPlayers)
        {
            if (!leftPlayers.ContainsKey(name))
            {
                report.OnlyInRight.Add(rightPlayer);
            }
        }

        return report;
    }

    private static Dictionary<string, Player> TeamPlayersByName(PlayerRoster roster, int teamId)
    {
        var players = new Dictionary<string, Player>(StringComparer.Ordinal);
        foreach (var player in roster.Players.Where(p => p.TeamIndex == teamId))
        {
            // Last write wins on duplicate names — acceptable for a
            // comparison utility; genuine duplicates are rare on one team.
            players[Mapping.TeamMappingSet.Normalize(player.FirstName + " " + player.LastName)] = player;
        }

        return players;
    }
}
