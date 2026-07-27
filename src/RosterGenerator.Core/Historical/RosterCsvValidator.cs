using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Historical;

/// <summary>How much a finding matters.</summary>
public enum RosterCsvSeverity
{
    /// <summary>Generation cannot run until this is fixed.</summary>
    Blocking,

    /// <summary>Generation will run, but this player or value will not be used as written.</summary>
    Warning,

    /// <summary>Worth knowing before importing; nothing is wrong.</summary>
    Note,
}

/// <summary>One thing worth telling the user about their roster CSV.</summary>
/// <param name="Severity">How much it matters.</param>
/// <param name="Message">What is wrong, in the user's terms.</param>
/// <param name="Player">The player it concerns, when it concerns one.</param>
public sealed record RosterCsvFinding(RosterCsvSeverity Severity, string Message, string? Player = null)
{
    /// <inheritdoc />
    public override string ToString() =>
        Player is null ? Message : $"{Player}: {Message}";
}

/// <summary>The result of checking a roster CSV.</summary>
public sealed class RosterCsvReport
{
    /// <summary>Creates a report.</summary>
    public RosterCsvReport(
        string path,
        HistoricalRoster? roster,
        IReadOnlyList<RosterCsvFinding> findings,
        IReadOnlyList<HistoricalRoster>? rosters = null)
    {
        Path = path;
        Roster = roster;
        Findings = findings;
        Rosters = rosters is { Count: > 0 }
            ? rosters
            : roster is null
                ? Array.Empty<HistoricalRoster>()
                : new[] { roster };
    }

    /// <summary>The file that was checked.</summary>
    public string Path { get; }

    /// <summary>
    /// The first team in the file, or null when it could not be read at all.
    /// A single-team file — still the common case — has only this one.
    /// </summary>
    public HistoricalRoster? Roster { get; }

    /// <summary>Every team the file carries, in file order.</summary>
    public IReadOnlyList<HistoricalRoster> Rosters { get; }

    /// <summary>Everything worth reporting, most severe first.</summary>
    public IReadOnlyList<RosterCsvFinding> Findings { get; }

    /// <summary>Players that would be written to the save, across every team.</summary>
    public int UsablePlayers => Rosters.Sum(r => r.Players.Count);

    /// <summary>True when generation can run.</summary>
    public bool CanGenerate =>
        Roster is not null && Findings.All(f => f.Severity != RosterCsvSeverity.Blocking);

    /// <summary>Findings of one severity.</summary>
    public IEnumerable<RosterCsvFinding> OfSeverity(RosterCsvSeverity severity) =>
        Findings.Where(f => f.Severity == severity);

    /// <summary>Renders the report as plain text.</summary>
    public string ToText()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"Roster CSV check — {System.IO.Path.GetFileName(Path)}");
        text.AppendLine();

        if (Roster is null)
        {
            text.AppendLine("The file could not be read:");
            foreach (var finding in Findings)
            {
                text.AppendLine($"  - {finding}");
            }

            return text.ToString();
        }

        text.AppendLine($"Players usable:  {UsablePlayers}");
        if (Rosters.Count > 1)
        {
            // Naming 119 schools here would bury the findings; the teams are
            // the point of a season file, the count is what needs checking.
            text.AppendLine($"Teams:           {Rosters.Count}");
        }
        else
        {
            text.AppendLine($"Team:            {Roster.School}");
        }

        var seasons = Rosters.Select(r => r.Season).Distinct().OrderBy(s => s).ToList();
        text.AppendLine($"Season:          {(seasons is [0] ? "not given" : string.Join(", ", seasons))}");
        text.AppendLine();

        foreach (var (severity, heading) in new[]
                 {
                     (RosterCsvSeverity.Blocking, "Must fix before generating"),
                     (RosterCsvSeverity.Warning, "Will not be used as written"),
                     (RosterCsvSeverity.Note, "Worth knowing"),
                 })
        {
            var group = OfSeverity(severity).ToList();
            if (group.Count == 0)
            {
                continue;
            }

            text.AppendLine($"{heading} ({group.Count}):");
            foreach (var finding in group)
            {
                text.AppendLine($"  - {finding}");
            }

            text.AppendLine();
        }

        text.AppendLine(CanGenerate
            ? "Ready to generate."
            : "Fix the blocking problems above, then run this again.");
        return text.ToString();
    }
}

/// <summary>
/// Checks a roster CSV on its own, before anything is generated.
///
/// Generation already reports every problem it meets, but it does so in a
/// 39,000-line report written *after* a 27 MB save file has been produced —
/// so a user who mistyped a class on player 40 sees "85 warnings" and has to
/// go looking. This answers "is my file right?" directly.
///
/// It deliberately runs the **same** reader, the same position mappings and
/// the same bounds that generation uses, rather than reimplementing the
/// checks. A validator that drifts from the thing it validates is worse than
/// no validator, because it is believed.
/// </summary>
public static class RosterCsvValidator
{
    /// <summary>
    /// Checks a roster CSV.
    /// </summary>
    /// <param name="path">The roster CSV to check.</param>
    /// <param name="positions">
    /// Position mappings, so an unusable position is caught here rather than
    /// silently dropping a player during generation.
    /// </param>
    /// <param name="dynasty">
    /// The user's dynasty, when they have one selected. Supplied, the team
    /// name is resolved against it and roster size is checked against the real
    /// slot count.
    /// </param>
    /// <param name="school">Team override, as passed to generation.</param>
    /// <param name="season">Season override, as passed to generation.</param>
    /// <param name="ratings">
    /// The rating engine, so a role generation would reject is reported here
    /// too. Without it, roles are not checked at all rather than checked
    /// against a second, drifting copy of the vocabulary.
    /// </param>
    /// <param name="membership">
    /// When each school reached the FBS. Supplied, a team that had not got
    /// there yet in the season being recreated is reported. Omitted, the
    /// question is not asked at all.
    /// </param>
    public static RosterCsvReport Check(
        string path,
        PositionMappingSet? positions = null,
        DynastyExport? dynasty = null,
        string? school = null,
        int? season = null,
        Rating.RatingEngine? ratings = null,
        FbsMembership? membership = null)
    {
        var findings = new List<RosterCsvFinding>();

        if (!File.Exists(path))
        {
            findings.Add(new RosterCsvFinding(RosterCsvSeverity.Blocking, $"'{path}' does not exist."));
            return new RosterCsvReport(path, null, findings);
        }

        HistoricalCsvResult result;
        try
        {
            result = HistoricalCsv.Read(path, school, season);
        }
        catch (CsvSchemaException ex)
        {
            findings.Add(new RosterCsvFinding(RosterCsvSeverity.Blocking, ex.Message));
            return new RosterCsvReport(path, null, findings);
        }

        // Everything the reader itself noticed. A row it dropped is a player
        // the user will not get, so those are warnings; a value it cleaned up
        // and then used is not a problem and must not be dressed as one.
        foreach (var warning in result.Warnings)
        {
            findings.Add(new RosterCsvFinding(RosterCsvSeverity.Warning, warning));
        }

        foreach (var correction in result.Corrections)
        {
            findings.Add(new RosterCsvFinding(RosterCsvSeverity.Note, correction));
        }

        // One file can carry a whole season, and every team in it gets the same
        // checks — a season file is exactly where a bad row is easiest to miss.
        var rosters = result.Rosters.Count > 0 ? result.Rosters : new[] { result.Roster };
        var facts = DynastyFacts.For(dynasty);
        foreach (var roster in rosters)
        {
            var team = new List<RosterCsvFinding>();
            CheckRoles(roster, ratings, team);
            CheckPositions(roster, positions, team);
            CheckValueRanges(roster, team);
            CheckDuplicates(roster, team);
            CheckTeamAndSeason(roster, facts, school, team);
            CheckFbsMembership(roster, membership, team);

            // In a season file "appears twice" is useless without a school —
            // two teams may each legitimately carry a number 12.
            findings.AddRange(rosters.Count == 1
                ? team
                : team.Select(f => f with { Player = Qualify(roster.School, f.Player) }));
        }

        var ordered = findings
            .OrderBy(f => f.Severity)
            .ToList();
        return new RosterCsvReport(path, result.Roster, ordered, rosters);
    }

    private static string Qualify(string school, string? player) =>
        player is { Length: > 0 } && player != school ? $"{school} — {player}" : school;

    /// <summary>
    /// Reports a team that had not reached the FBS in the season being
    /// recreated — a 2010 file naming Sacramento State, say.
    ///
    /// <para>It is a note, not a blocker. The dates are the tool's best
    /// reading of the record and are a plain JSON file the user can correct,
    /// so a user who knows better than the data must not be stopped by it.
    /// The point is to say so out loud, because CFB27 carries today's 138
    /// teams and a season assembled from that list gives no other sign.</para>
    /// </summary>
    private static void CheckFbsMembership(
        HistoricalRoster roster, FbsMembership? membership, List<RosterCsvFinding> findings)
    {
        if (membership is null || roster.Season <= 0)
        {
            return;
        }

        if (membership.Check(roster.School, roster.Season) is { } problem)
        {
            findings.Add(new RosterCsvFinding(
                RosterCsvSeverity.Note,
                $"{problem.Detail}. The roster is still generated — correct " +
                "data/FbsMembership.json if you know better.",
                roster.School));
        }
    }

    /// <summary>
    /// Roles are checked against the engine's own vocabulary, the same one
    /// generation uses, so the two can never disagree about what a valid role
    /// is.
    /// </summary>
    private static void CheckRoles(
        HistoricalRoster roster, Rating.RatingEngine? ratings, List<RosterCsvFinding> findings)
    {
        if (ratings is null)
        {
            return;
        }

        foreach (var player in roster.Players)
        {
            if (player.Evidence.Role is { Length: > 0 } role &&
                !string.IsNullOrWhiteSpace(role) &&
                !ratings.IsKnownRole(role))
            {
                findings.Add(new RosterCsvFinding(
                    RosterCsvSeverity.Warning,
                    $"role '{role.Trim()}' is not one the tool knows, so it would be ignored. " +
                    $"Use one of: {string.Join(", ", ratings.KnownRoles)}.",
                    $"{player.FirstName} {player.LastName}"));
            }
        }
    }

    private static void CheckPositions(
        HistoricalRoster roster, PositionMappingSet? positions, List<RosterCsvFinding> findings)
    {
        if (positions is null)
        {
            return;
        }

        foreach (var player in roster.Players)
        {
            if (!positions.TryResolve(player.Position, out _))
            {
                findings.Add(new RosterCsvFinding(
                    RosterCsvSeverity.Warning,
                    $"position '{player.Position}' is not one the tool knows, so this player would be " +
                    "skipped. Fix the spelling, or add it to data/PositionMappings.json.",
                    $"{player.FirstName} {player.LastName}"));
            }
        }
    }

    private static void CheckValueRanges(HistoricalRoster roster, List<RosterCsvFinding> findings)
    {
        foreach (var player in roster.Players)
        {
            var name = $"{player.FirstName} {player.LastName}";

            void Range(int? value, int min, int max, string label, string unit = "")
            {
                if (value is int number && (number < min || number > max))
                {
                    findings.Add(new RosterCsvFinding(
                        RosterCsvSeverity.Warning,
                        $"{label} {number}{unit} is outside the {min}–{max} the game accepts, so this " +
                        "player would keep the value of the player they replace.",
                        name));
                }
            }

            Range(player.JerseyNumber, PlayerSchema.JerseyNumMin, PlayerSchema.JerseyNumMax, "jersey number");
            Range(player.HeightInches, PlayerSchema.HeightInchesMin, PlayerSchema.HeightInchesMax,
                "height", " inches");
            Range(player.WeightPounds, PlayerSchema.WeightPoundsMin, PlayerSchema.WeightPoundsMax, "weight", " lb");

            if (player.ClassYear is { Length: > 0 } classYear &&
                !Conversion.ClassYear.TryParse(classYear, out _, out _))
            {
                findings.Add(new RosterCsvFinding(
                    RosterCsvSeverity.Warning,
                    $"class '{classYear}' is not one the tool knows, so this player would keep the class " +
                    "of the player they replace. Try Freshman / Sophomore / Junior / Senior, optionally " +
                    "with an RS prefix.",
                    name));
            }
        }
    }

    private static void CheckDuplicates(HistoricalRoster roster, List<RosterCsvFinding> findings)
    {
        // Two rows for the same person is a copy-paste slip, and the second
        // silently takes a second roster slot.
        var duplicateNames = roster.Players
            .GroupBy(p => $"{p.FirstName.Trim()} {p.LastName.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var duplicate in duplicateNames)
        {
            findings.Add(new RosterCsvFinding(
                RosterCsvSeverity.Warning,
                $"appears {duplicate.Count()} times — each row takes its own roster slot.",
                duplicate.Key));
        }
    }

    /// <summary>
    /// The dynasty's team names and each team's slot count, read once.
    ///
    /// A season file asks the same two questions of 119 teams, and the answers
    /// come from a 27 MB table — so the table is loaded once here rather than
    /// once per team.
    /// </summary>
    private sealed record DynastyFacts(TeamMappingSet Teams, IReadOnlyDictionary<int, int> SlotsByTeam)
    {
        public static DynastyFacts? For(DynastyExport? dynasty)
        {
            if (dynasty is null || dynasty.Teams.Count == 0)
            {
                return null;
            }

            var slots = dynasty.LoadPlayerRoster().Players
                .GroupBy(p => p.TeamIndex)
                .ToDictionary(g => g.Key, g => g.Count());
            return new DynastyFacts(dynasty.BuildTeamMappings(), slots);
        }
    }

    private static void CheckTeamAndSeason(
        HistoricalRoster roster, DynastyFacts? dynasty, string? school, List<RosterCsvFinding> findings)
    {
        if (roster.Season == 0)
        {
            findings.Add(new RosterCsvFinding(
                RosterCsvSeverity.Note,
                "no Season was given. It is only used for labelling the report."));
        }

        if (dynasty is null)
        {
            return;
        }

        if (!dynasty.Teams.TryResolve(roster.School, out var teamId))
        {
            findings.Add(new RosterCsvFinding(
                RosterCsvSeverity.Blocking,
                $"'{roster.School}' is not a team in your dynasty. Run list-teams to see the names it " +
                "uses, or add an alias to data/TeamMappings.json."));
            return;
        }

        if (!dynasty.SlotsByTeam.TryGetValue(teamId, out var slots) || slots == 0)
        {
            return;
        }

        if (roster.Players.Count > slots)
        {
            findings.Add(new RosterCsvFinding(
                RosterCsvSeverity.Warning,
                $"the file has {roster.Players.Count} players but {roster.School} has {slots} roster " +
                $"slots, so the last {roster.Players.Count - slots} would be skipped."));
        }
        else if (roster.Players.Count < slots)
        {
            findings.Add(new RosterCsvFinding(
                RosterCsvSeverity.Note,
                $"the file has {roster.Players.Count} players and {roster.School} has {slots} roster " +
                $"slots, so {slots - roster.Players.Count} will be filled in as end-of-roster depth."));
        }
    }
}
