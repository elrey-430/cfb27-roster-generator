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

    /// <summary>Generated ratings, or null when ratings were inherited.</summary>
    public Rating.GeneratedRatings? Ratings { get; set; }

    /// <summary>Archetype choice, or null when the donor archetype was kept.</summary>
    public Rating.ArchetypeChoice? Archetype { get; set; }

    /// <summary>
    /// The commentary index written for this player, from their surname.
    /// <see cref="Mapping.CommentaryIdSet.None"/> means the announcers have no
    /// recording of that name and will not say it — which is the game's own
    /// answer for a fifth of its players, not a failure of this tool.
    /// </summary>
    public int CommentaryId { get; set; }
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

    /// <summary>
    /// Slots re-rated as end-of-roster depth because the historical roster did
    /// not fill them. Empty when the fill is disabled, in which case the same
    /// slots appear in <see cref="LeftoverDonorSlots"/> instead.
    /// </summary>
    public List<FilledSlot> FilledSlots { get; } = new();

    /// <summary>Conversion-wide assumptions (weights, ratings, assets...).</summary>
    public List<string> GlobalAssumptions { get; } = new();

    /// <summary>Conversion-wide warnings.</summary>
    public List<string> GlobalWarnings { get; } = new();

    /// <summary>Players successfully placed into a donor slot.</summary>
    public IEnumerable<PlayerConversionEntry> Converted => Entries.Where(e => e.AssignedRowKey is not null);

    /// <summary>Players that could not be placed.</summary>
    public IEnumerable<PlayerConversionEntry> Skipped => Entries.Where(e => e.AssignedRowKey is null);

    /// <summary>
    /// Renders the plain-text generation report (the end-user format written
    /// to <c>Generation_Report.txt</c>).
    /// </summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generation Report — {Source.Season} {Source.School} (team {TeamId})");
        sb.AppendLine();
        sb.AppendLine($"Players Processed: {Entries.Count}");
        sb.AppendLine($"Players Mapped:    {Converted.Count()}");
        sb.AppendLine($"Players Skipped:   {Skipped.Count()}");
        sb.AppendLine($"Donor Slots Left:  {LeftoverDonorSlots.Count}");
        if (Source.Source is not null)
        {
            sb.AppendLine($"Data Source:       {Source.Source}");
        }

        sb.AppendLine();
        sb.AppendLine("Assumptions:");
        foreach (var assumption in GlobalAssumptions)
        {
            sb.AppendLine($"- {assumption}");
        }

        if (GlobalWarnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Warnings:");
            foreach (var warning in GlobalWarnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        var withIssues = Entries
            .Where(e => e.MissingFields.Count > 0 || e.DefaultsUsed.Count > 0 || e.Warnings.Count > 0)
            .ToList();
        sb.AppendLine();
        sb.AppendLine("Players with missing information, defaults, or warnings:");
        if (withIssues.Count == 0)
        {
            sb.AppendLine("(none)");
        }

        foreach (var entry in withIssues)
        {
            sb.AppendLine();
            sb.AppendLine($"Player: {entry.Player.FirstName} {entry.Player.LastName}");
            if (entry.MissingFields.Count > 0)
            {
                sb.AppendLine("  Missing:");
                foreach (var field in entry.MissingFields)
                {
                    sb.AppendLine($"  - {field}");
                }
            }

            if (entry.DefaultsUsed.Count > 0)
            {
                sb.AppendLine("  Default used:");
                foreach (var d in entry.DefaultsUsed)
                {
                    sb.AppendLine($"  - {d}");
                }
            }

            if (entry.Warnings.Count > 0)
            {
                sb.AppendLine("  Warnings:");
                foreach (var warning in entry.Warnings)
                {
                    sb.AppendLine($"  - {warning}");
                }
            }
        }

        var withRatings = Converted.Where(e => e.Ratings is not null).ToList();
        if (withRatings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Generated ratings:");
            foreach (var confidence in new[] { "High", "Medium", "Low" })
            {
                var count = withRatings.Count(e => e.Ratings!.Confidence.ToString() == confidence);
                sb.AppendLine($"  {confidence,-6} confidence: {count}");
            }

            sb.AppendLine();
            sb.AppendLine($"  {"Player",-26} {"Pos",-4} {"OVR",3}  Confidence  Basis");
            foreach (var entry in withRatings.OrderByDescending(e => e.Ratings!.Overall))
            {
                var r = entry.Ratings!;
                var basis = r.Talent.Reasons.FirstOrDefault() ?? "position and class defaults";
                sb.AppendLine(
                    $"  {entry.Player.FirstName + " " + entry.Player.LastName,-26} " +
                    $"{entry.AssignedPosition,-4} {r.Overall,3}  {r.Confidence,-10}  {basis}");
            }
        }

        if (Skipped.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Skipped players (no roster slot available):");
            foreach (var entry in Skipped)
            {
                sb.AppendLine($"- {entry.Player}");
            }
        }

        if (LeftoverDonorSlots.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Roster slots not replaced (original players remain):");
            foreach (var slot in LeftoverDonorSlots)
            {
                sb.AppendLine($"- {slot}");
            }
        }

        if (FilledSlots.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"Roster depth filled ({FilledSlots.Count} slot(s) you did not supply players for). " +
                "These keep their original names, jersey numbers and portraits; only their ratings " +
                "and class years were rewritten, so none of them can out-rate your roster:");
            sb.AppendLine();
            sb.AppendLine($"  {"Player",-26} {"Pos",-4} {"OVR",3} {"was",4}  {"Class",-10} Why");
            foreach (var slot in FilledSlots)
            {
                sb.AppendLine(
                    $"  {slot.Name,-26} {slot.Position,-4} {slot.Overall,3} {slot.PreviousOverall,4}  " +
                    $"{slot.ClassYear,-10} {slot.Reason}");
            }
        }

        return sb.ToString();
    }

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
