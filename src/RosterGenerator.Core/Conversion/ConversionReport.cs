using System.Text;
using RosterGenerator.Core.Historical;

namespace RosterGenerator.Core.Conversion;

/// <summary>What happened to one historical player during conversion.</summary>
public sealed class PlayerConversionEntry
{
    /// <summary>Creates an entry for one converted (or skipped) player.</summary>
    public PlayerConversionEntry(HistoricalPlayer player)
    {
        Player = player;
    }

    /// <summary>The historical player.</summary>
    public HistoricalPlayer Player { get; }

    /// <summary>The donor slot's <c>_row</c> key, or null if skipped.</summary>
    public int? AssignedRowKey { get; set; }

    /// <summary>The CFB27 position written for this player.</summary>
    public string? AssignedPosition { get; set; }

    /// <summary>Historical fields that had no value in the dataset.</summary>
    public List<string> MissingFields { get; } = new();

    /// <summary>Defaults substituted for missing values ("field: value (reason)").</summary>
    public List<string> DefaultsUsed { get; } = new();

    /// <summary>Per-player warnings and assumptions.</summary>
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// The full record of one historical→CFB27 conversion: per-player outcomes,
/// donor slots left over, players that could not be placed, and the global
/// assumptions the converter operated under. Renders to the milestone's
/// required Markdown validation report.
/// </summary>
public sealed class ConversionReport
{
    /// <summary>Creates an empty report for a roster.</summary>
    public ConversionReport(HistoricalRoster source, int teamId)
    {
        Source = source;
        TeamId = teamId;
    }

    /// <summary>The historical roster that was converted.</summary>
    public HistoricalRoster Source { get; }

    /// <summary>The resolved CFB27 team index.</summary>
    public int TeamId { get; }

    /// <summary>One entry per historical player, in dataset order.</summary>
    public List<PlayerConversionEntry> Entries { get; } = new();

    /// <summary>Donor slots not replaced (original fictional players remain).</summary>
    public List<string> LeftoverDonorSlots { get; } = new();

    /// <summary>Conversion-wide assumptions (weights, ratings, assets...).</summary>
    public List<string> GlobalAssumptions { get; } = new();

    /// <summary>Conversion-wide warnings.</summary>
    public List<string> GlobalWarnings { get; } = new();

    /// <summary>Players successfully placed into a donor slot.</summary>
    public IEnumerable<PlayerConversionEntry> Converted => Entries.Where(e => e.AssignedRowKey is not null);

    /// <summary>Players that could not be placed.</summary>
    public IEnumerable<PlayerConversionEntry> Skipped => Entries.Where(e => e.AssignedRowKey is null);

    /// <summary>Renders the Markdown validation report.</summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {Source.Season} {Source.School} — CFB27 Conversion Report");
        sb.AppendLine();
        sb.AppendLine($"- **Team ID:** {TeamId}");
        sb.AppendLine($"- **Players in historical dataset:** {Entries.Count}");
        sb.AppendLine($"- **Players generated:** {Converted.Count()}");
        sb.AppendLine($"- **Players skipped:** {Skipped.Count()}");
        sb.AppendLine($"- **Donor slots left unreplaced:** {LeftoverDonorSlots.Count}");
        if (Source.Source is not null)
        {
            sb.AppendLine($"- **Dataset source:** {Source.Source}");
        }

        sb.AppendLine();
        sb.AppendLine("## Global assumptions");
        sb.AppendLine();
        foreach (var assumption in GlobalAssumptions)
        {
            sb.AppendLine($"- {assumption}");
        }

        if (GlobalWarnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            sb.AppendLine();
            foreach (var warning in GlobalWarnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        var withIssues = Entries
            .Where(e => e.MissingFields.Count > 0 || e.DefaultsUsed.Count > 0 || e.Warnings.Count > 0)
            .ToList();
        sb.AppendLine();
        sb.AppendLine("## Players with missing information, defaults, or warnings");
        sb.AppendLine();
        if (withIssues.Count == 0)
        {
            sb.AppendLine("None.");
        }

        foreach (var entry in withIssues)
        {
            sb.AppendLine($"### {entry.Player.FirstName} {entry.Player.LastName}");
            sb.AppendLine();
            if (entry.MissingFields.Count > 0)
            {
                sb.AppendLine("Missing:");
                foreach (var field in entry.MissingFields)
                {
                    sb.AppendLine($"- {field}");
                }

                sb.AppendLine();
            }

            if (entry.DefaultsUsed.Count > 0)
            {
                sb.AppendLine("Default used:");
                foreach (var d in entry.DefaultsUsed)
                {
                    sb.AppendLine($"- {d}");
                }

                sb.AppendLine();
            }

            if (entry.Warnings.Count > 0)
            {
                sb.AppendLine("Warnings:");
                foreach (var warning in entry.Warnings)
                {
                    sb.AppendLine($"- {warning}");
                }

                sb.AppendLine();
            }
        }

        if (Skipped.Any())
        {
            sb.AppendLine("## Skipped players (no donor slot available)");
            sb.AppendLine();
            foreach (var entry in Skipped)
            {
                sb.AppendLine($"- {entry.Player}");
            }

            sb.AppendLine();
        }

        if (LeftoverDonorSlots.Count > 0)
        {
            sb.AppendLine("## Donor slots left unreplaced");
            sb.AppendLine();
            sb.AppendLine("These original (fictional) players remain on the team because the");
            sb.AppendLine("historical dataset had fewer players than the donor roster:");
            sb.AppendLine();
            foreach (var slot in LeftoverDonorSlots)
            {
                sb.AppendLine($"- {slot}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Converted players");
        sb.AppendLine();
        sb.AppendLine("| # | Name | Pos | Class | Donor slot (_row) |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var entry in Converted)
        {
            var p = entry.Player;
            sb.AppendLine(
                $"| {(p.JerseyNumber?.ToString() ?? "—")} | {p.FirstName} {p.LastName} | " +
                $"{entry.AssignedPosition} | {p.ClassYear ?? "—"} | {entry.AssignedRowKey} |");
        }

        return sb.ToString();
    }
}
