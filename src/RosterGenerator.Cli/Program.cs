using RosterGenerator.Core.Comparison;
using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Validation;

// End-user front end for the historical roster pipeline.
//
//   generate   --dynasty <export folder or Player.csv> --roster <simple .csv or .json>
//              [--team <name>] [--season <year>] [--output <csv>] [--report <txt|md>]
//              [--ratings generate|inherit] [--team-mappings <json>] [--position-mappings <json>]
//   list-teams --dynasty <export folder or Player.csv>
//   compare    --left <Player.csv> --right <Player.csv> --team <name or id>
//              [--dynasty <export>] [--output <md>]
//
// When --team / --season are omitted and the roster CSV does not supply
// them, generate asks interactively (listing the dynasty's own teams).

try
{
    return args.FirstOrDefault() switch
    {
        "generate" => Generate(ParseOptions(args[1..])),
        "list-teams" => ListTeams(ParseOptions(args[1..])),
        "compare" => Compare(ParseOptions(args[1..])),
        _ => Usage(),
    };
}
catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or KeyNotFoundException
    or InvalidDataException or RosterGenerator.Core.Csv.CsvSchemaException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int Usage()
{
    Console.Error.WriteLine("""
        Historical CFB27 Roster Generator

        Usage:
          generate   --dynasty <export folder or Player.csv> --roster <historical roster .csv>
                     [--team <name>] [--season <year>] [--output <csv>] [--report <txt|md>]
                     [--ratings generate|inherit] [--team-mappings <json>] [--position-mappings <json>]
          list-teams --dynasty <export folder or Player.csv>
          compare    --left <Player.csv> --right <Player.csv> --team <name or id>
                     [--dynasty <export>] [--output <md>]

        --ratings generate (the default) builds each player's attributes from the
        historical evidence in the roster CSV; --ratings inherit keeps the ratings
        of the players being replaced.

        The roster CSV format is documented in docs/Historical_CSV_Format.md
        (template: templates/HistoricalRosterTemplate.csv). Defaults:
        --output Output/Generated_Roster.csv, --report Output/Generation_Report.txt.
        """);
    return 1;
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Expected an --option, got '{args[i]}'.");
        }

        options[args[i][2..]] = args[i + 1];
    }

    return options;
}

static string Require(Dictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value)
        ? value
        : throw new ArgumentException($"Missing required option --{name}.");

// Data files are looked up: explicit flag, then ./data/, then the data/
// folder next to the executable (populated by publish).
static string? FindDataFile(Dictionary<string, string> options, string option, string fileName, bool required)
{
    if (options.TryGetValue(option, out var explicitPath))
    {
        return explicitPath;
    }

    foreach (var candidate in new[]
             {
                 Path.Combine("data", fileName),
                 Path.Combine(AppContext.BaseDirectory, "data", fileName),
             })
    {
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return required
        ? throw new ArgumentException($"--{option} not given and data/{fileName} not found next to the " +
                                      "executable or the current directory.")
        : null;
}

static DynastyExport OpenDynasty(Dictionary<string, string> options)
{
    // --base is the pre-Milestone-3 spelling; accept both.
    var path = options.TryGetValue("dynasty", out var dynasty)
        ? dynasty
        : options.TryGetValue("base", out var basePath)
            ? basePath
            : throw new ArgumentException("Missing required option --dynasty (your dynasty export folder or its Player table CSV).");
    var export = DynastyExport.Open(path);
    Console.WriteLine($"Dynasty loaded: {export.PlayerTablePath}");
    Console.WriteLine($"  {export.Teams.Count} teams discovered");
    return export;
}

static int ListTeams(Dictionary<string, string> options)
{
    var export = OpenDynasty(options);
    Console.WriteLine();
    Console.WriteLine("Available teams:");
    foreach (var team in export.Teams)
    {
        Console.WriteLine($"  {team}");
    }

    return 0;
}

static int Generate(Dictionary<string, string> options)
{
    var export = OpenDynasty(options);
    var positionMappings = PositionMappingSet.Load(
        FindDataFile(options, "position-mappings", "PositionMappings.json", required: true)!);
    var teamMappings = export.Teams.Count > 0
        ? export.BuildTeamMappings(FindDataFile(options, "team-mappings", "TeamMappings.json", required: false))
        : TeamMappingSet.Load(FindDataFile(options, "team-mappings", "TeamMappings.json", required: true)!);

    // --historical is the pre-Milestone-3 spelling; accept both.
    var rosterPath = options.TryGetValue("roster", out var roster)
        ? roster
        : options.TryGetValue("historical", out var historicalPath)
            ? historicalPath
            : throw new ArgumentException("Missing required option --roster (your historical roster CSV).");

    var teamOption = options.TryGetValue("team", out var teamValue) ? teamValue : null;
    int? seasonOption = options.TryGetValue("season", out var seasonValue) ? int.Parse(seasonValue) : null;

    HistoricalRoster historical;
    if (Path.GetExtension(rosterPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
    {
        historical = HistoricalRoster.Load(rosterPath);
        if (teamOption is not null)
        {
            historical = historical with { School = teamOption };
        }

        if (seasonOption is int seasonOverride)
        {
            historical = historical with { Season = seasonOverride };
        }
    }
    else
    {
        // The simple CSV may omit Team/Season; ask interactively when the
        // console allows it.
        if (teamOption is null && !CsvHasTeam(rosterPath))
        {
            teamOption = SelectTeamInteractively(export);
        }

        if (seasonOption is null && teamOption is not null && !Console.IsInputRedirected)
        {
            Console.Write("Season (e.g. 2013, Enter to skip): ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var enteredSeason))
            {
                seasonOption = enteredSeason;
            }
        }

        var result = HistoricalCsv.Read(rosterPath, teamOption, seasonOption);
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"  roster CSV: {warning}");
        }

        historical = result.Roster;
    }

    Console.WriteLine($"Historical roster: {(historical.Season == 0 ? "season ?" : historical.Season.ToString())} " +
                      $"{historical.School} — {historical.Players.Count} players");

    var ratingsMode = options.GetValueOrDefault("ratings", "generate");
    RatingEngine? ratingEngine = null;
    if (ratingsMode.Equals("generate", StringComparison.OrdinalIgnoreCase))
    {
        ratingEngine = RatingEngine.Load(
            FindDataFile(options, "rating-models", "RatingModels.json", required: true)!,
            FindDataFile(options, "overall-formulas", "OverallFormulas.json", required: true)!);
        Console.WriteLine("Rating generation: on (EA overall formulas driven by historical evidence)");
    }
    else if (!ratingsMode.Equals("inherit", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("--ratings must be 'generate' or 'inherit'.");
    }

    var donor = export.LoadPlayerRoster();
    var session = new RosterEditSession(donor);
    var report = new HistoricalTeamConverter(teamMappings, positionMappings, ratingEngine)
        .Convert(session, historical);
    Console.WriteLine($"Converted {report.Converted.Count()} players onto team {report.TeamId} " +
                      $"({report.Skipped.Count()} skipped, {report.LeftoverDonorSlots.Count} donor slots left).");

    var outputPath = options.TryGetValue("output", out var output)
        ? output
        : Path.Combine("Output", "Generated_Roster.csv");
    var reportPath = options.TryGetValue("report", out var reportOption)
        ? reportOption
        : Path.Combine("Output", "Generation_Report.txt");
    CreateParentDirectory(outputPath);
    CreateParentDirectory(reportPath);

    ExportResult result2;
    try
    {
        result2 = new RosterExporter().Export(new RosterValidationContext(donor, session), outputPath);
    }
    catch (RosterExportException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    File.WriteAllText(reportPath,
        Path.GetExtension(reportPath).Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? report.ToMarkdown()
            : report.ToText());

    Console.WriteLine($"Validation: 0 errors, {result2.Report.Warnings.Count()} warnings.");
    Console.WriteLine($"Generated roster: {outputPath} ({result2.ChangedColumnsByRowKey.Count} rows modified)");
    Console.WriteLine($"Report:           {reportPath}");
    return 0;
}

static bool CsvHasTeam(string rosterPath)
{
    using var reader = new StreamReader(rosterPath);
    var header = (reader.ReadLine() ?? "").Split(',');
    return header.Any(h => h.Trim().Equals("Team", StringComparison.OrdinalIgnoreCase));
}

static string SelectTeamInteractively(DynastyExport export)
{
    if (export.Teams.Count == 0)
    {
        throw new ArgumentException(
            "The roster CSV has no Team column, no --team was given, and the dynasty export has no Team " +
            "table to choose from. Pass --team <name>.");
    }

    if (Console.IsInputRedirected)
    {
        throw new ArgumentException(
            "The roster CSV has no Team column and no --team was given. Pass --team <name> " +
            "(see list-teams for this dynasty's team names).");
    }

    Console.WriteLine();
    Console.WriteLine("Available teams:");
    for (var i = 0; i < export.Teams.Count; i++)
    {
        Console.WriteLine($"  {i + 1,3}. {export.Teams[i]}");
    }

    while (true)
    {
        Console.Write("Select team (number or name): ");
        var input = (Console.ReadLine() ?? "").Trim();
        if (int.TryParse(input, out var index) && index >= 1 && index <= export.Teams.Count)
        {
            return export.Teams[index - 1].DisplayName;
        }

        var byName = export.Teams.FirstOrDefault(t =>
            t.DisplayName.Equals(input, StringComparison.OrdinalIgnoreCase) ||
            t.ShortName.Equals(input, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            return byName.DisplayName;
        }

        Console.WriteLine("Not recognized — enter a number from the list or an exact team name.");
    }
}

static void CreateParentDirectory(string path)
{
    var directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (directory is not null)
    {
        Directory.CreateDirectory(directory);
    }
}

static int Compare(Dictionary<string, string> options)
{
    var leftPath = Require(options, "left");
    var rightPath = Require(options, "right");
    var teamOption = Require(options, "team");

    int teamId;
    if (!int.TryParse(teamOption, out teamId))
    {
        var mappings = options.ContainsKey("dynasty") || options.ContainsKey("base")
            ? OpenDynasty(options).BuildTeamMappings(FindDataFile(options, "team-mappings", "TeamMappings.json", required: false))
            : TeamMappingSet.Load(FindDataFile(options, "team-mappings", "TeamMappings.json", required: true)!);
        teamId = mappings.Resolve(teamOption);
    }

    var left = PlayerRoster.Load(leftPath);
    var right = PlayerRoster.Load(rightPath);
    var report = new RosterComparer().Compare(
        left, right, teamId,
        leftLabel: Path.GetFileName(leftPath),
        rightLabel: Path.GetFileName(rightPath));

    var markdown = report.ToMarkdown();
    if (options.TryGetValue("output", out var outputPath))
    {
        File.WriteAllText(outputPath, markdown);
        Console.WriteLine($"Comparison written to {outputPath}");
    }
    else
    {
        Console.WriteLine(markdown);
    }

    return 0;
}
