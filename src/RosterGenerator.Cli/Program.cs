using RosterGenerator.Core.Comparison;
using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Validation;

// Command-line front end for the historical roster pipeline.
//
//   generate --base <Player.csv> --historical <roster.json> --output <csv>
//            [--report <md>] [--team-mappings <json>] [--position-mappings <json>]
//
//   compare  --left <Player.csv> --right <Player.csv> --team <name or id>
//            [--team-mappings <json>] [--output <md>]

return args.FirstOrDefault() switch
{
    "generate" => Generate(ParseOptions(args[1..])),
    "compare" => Compare(ParseOptions(args[1..])),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("""
        Usage:
          RosterGenerator.Cli generate --base <Player.csv> --historical <roster.json> --output <csv>
                                       [--report <md>] [--team-mappings <json>] [--position-mappings <json>]
          RosterGenerator.Cli compare  --left <Player.csv> --right <Player.csv> --team <name or id>
                                       [--team-mappings <json>] [--output <md>]
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

// Default mapping files live next to the executable's repo layout; fall back
// to the data/ directory relative to the current working directory.
static string DefaultDataFile(Dictionary<string, string> options, string option, string fileName)
{
    if (options.TryGetValue(option, out var explicitPath))
    {
        return explicitPath;
    }

    var local = Path.Combine("data", fileName);
    if (File.Exists(local))
    {
        return local;
    }

    throw new ArgumentException(
        $"--{option} not given and ./data/{fileName} not found. Pass the mapping file explicitly.");
}

static int Generate(Dictionary<string, string> options)
{
    var basePath = Require(options, "base");
    var historicalPath = Require(options, "historical");
    var outputPath = Require(options, "output");
    var teamMappings = TeamMappingSet.Load(DefaultDataFile(options, "team-mappings", "TeamMappings.json"));
    var positionMappings = PositionMappingSet.Load(DefaultDataFile(options, "position-mappings", "PositionMappings.json"));

    Console.WriteLine($"Loading donor roster: {basePath}");
    var roster = PlayerRoster.Load(basePath);
    Console.WriteLine($"Loading historical roster: {historicalPath}");
    var historical = HistoricalRoster.Load(historicalPath);
    Console.WriteLine($"  {historical.Season} {historical.School}: {historical.Players.Count} players");

    var session = new RosterEditSession(roster);
    var converter = new HistoricalTeamConverter(teamMappings, positionMappings);
    var report = converter.Convert(session, historical);

    Console.WriteLine($"Converted {report.Converted.Count()} players onto team {report.TeamId} " +
                      $"({report.Skipped.Count()} skipped, {report.LeftoverDonorSlots.Count} donor slots left).");

    ExportResult result;
    try
    {
        result = new RosterExporter().Export(new RosterValidationContext(roster, session), outputPath);
    }
    catch (RosterExportException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    Console.WriteLine($"Validation: 0 errors, {result.Report.Warnings.Count()} warnings.");
    Console.WriteLine($"Exported: {outputPath} ({result.ChangedColumnsByRowKey.Count} rows modified)");

    if (options.TryGetValue("report", out var reportPath))
    {
        File.WriteAllText(reportPath, report.ToMarkdown());
        Console.WriteLine($"Report: {reportPath}");
    }

    return 0;
}

static int Compare(Dictionary<string, string> options)
{
    var leftPath = Require(options, "left");
    var rightPath = Require(options, "right");
    var teamOption = Require(options, "team");

    int teamId;
    if (!int.TryParse(teamOption, out teamId))
    {
        var teamMappings = TeamMappingSet.Load(DefaultDataFile(options, "team-mappings", "TeamMappings.json"));
        teamId = teamMappings.Resolve(teamOption);
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
