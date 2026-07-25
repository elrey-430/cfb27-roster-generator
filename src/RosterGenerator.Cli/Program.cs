using RosterGenerator.Core.Comparison;
using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Validation;

// End-user front end for the historical roster pipeline.
//
// --dynasty is the folder of CSV files the community export tool wrote out of
// a dynasty, not a save file: this program never opens a save. The Player and
// Team tables are discovered inside that folder, so the Player CSV on its own
// is also accepted.
//
//   generate   --dynasty <folder of exported CSVs> --roster <simple .csv or .json>
//              [--team <name>] [--season <year>] [--output <csv>] [--report <txt|md>]
//              [--ratings generate|inherit] [--team-mappings <json>] [--position-mappings <json>]
//   list-teams --dynasty <folder of exported CSVs>
//   compare    --left <Player.csv> --right <Player.csv> --team <name or id>
//              [--dynasty <folder of exported CSVs>] [--output <md>]
//
// When --team / --season are omitted and the roster CSV does not supply
// them, generate asks interactively (listing the dynasty's own teams).

try
{
    return args.FirstOrDefault() switch
    {
        "generate" => Generate(ParseOptions(args[1..])),
        "validate" => Validate(ParseOptions(args[1..])),
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
          generate   --dynasty <folder of exported CSVs> --roster <historical roster .csv>
                     [--team <name>] [--season <year>] [--output <csv>] [--report <txt|md>]
                     [--ratings generate|inherit] [--archetypes select|inherit]
                     [--fill fill|leave] [--equipment era|leave] [--faces replace|inherit]
                     [--equipment-output <csv>]
                     [--team-mappings <json>] [--position-mappings <json>]
          validate   --roster <historical roster .csv>
                     [--dynasty <folder of exported CSVs>] [--team <name>] [--season <year>]
          list-teams --dynasty <folder of exported CSVs>
          compare    --left <Player.csv> --right <Player.csv> --team <name or id>
                     [--dynasty <folder of exported CSVs>] [--output <md>]

        --dynasty is the folder of CSV files the community export tool writes
        out of a dynasty — one CSV per table. This program never opens a save
        file: export first, point it at that folder, and it finds the Player
        and Team tables itself. The Player CSV on its own also works, though
        team names then have to be given with --team.

        validate checks your roster CSV without generating anything, so a
        mistake shows up in a few lines instead of inside a 27 MB file's report.
        Add --dynasty to also check the team name and the roster size against
        the exported tables. It exits non-zero only when something would stop
        generation.

        --ratings generate (the default) builds each player's attributes from the
        historical evidence in the roster CSV; --ratings inherit keeps the ratings
        of the players being replaced. --archetypes select (the default when
        ratings are generated) also picks each player's PlayerType from their
        profile and recomputes the overall with that archetype's formula.

        A CFB27 team always carries 85 players, so any slot your roster does not
        supply keeps its original fictional player — and because the game builds
        its depth chart from ratings, a leftover 82-overall player will start
        ahead of your roster. --fill fill (the default when ratings are
        generated) re-rates those slots as end-of-roster depth, holding each one
        below your weakest player at that position. Their names and jersey
        numbers do not change. --fill leave keeps them exactly as they are.

        Most roster slots carry a scan of a real person's head -- 9,011 of
        16,257 players in a base save -- and a replaced player used to inherit
        it, so a recreated roster ended up wearing the recognisable faces of
        present-day players under other people's names. --faces replace (the
        default) gives those players a generated face taken from elsewhere in
        your own export; --faces inherit keeps the slot's head as it was.

        Equipment is period-correct by default. Helmets live in the save's
        CharacterVisuals table, so when --season falls inside a known era the
        team's head gear is rewritten to match and the changed table is written
        to Output/Generated_Equipment.csv — import that alongside the roster.
        A season no era covers changes nothing, as does --equipment leave. The
        eras are editable in data/EquipmentEras.json.

        Your roster CSV needs only FirstName, LastName and Position per player;
        Number, Class, Team and Season are worth adding when you have them.
        Start from templates/HistoricalRosterTemplate_Basics.csv. Anything you
        leave out is filled in and listed in the report, and a bad value in one
        cell is reported without failing the export. The fuller
        templates/HistoricalRosterTemplate.csv adds statistics, draft positions
        and awards, which improve the ratings but are never required. Format
        reference: docs/Historical_CSV_Format.md. Defaults:
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
            : throw new ArgumentException(
                "Missing required option --dynasty (the folder of CSV files exported from your " +
                "dynasty, or the Player table CSV itself).");
    var export = DynastyExport.Open(path);
    Console.WriteLine($"Player table: {export.PlayerTablePath}");
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

static int Validate(Dictionary<string, string> options)
{
    var rosterPath = options.TryGetValue("roster", out var roster)
        ? roster
        : options.TryGetValue("historical", out var historical)
            ? historical
            : throw new ArgumentException("Missing required option --roster (the roster CSV to check).");

    // The dynasty is optional: a user can check their file before they have
    // exported a save. With one, the team name and the roster size are checked
    // against the real thing too.
    var export = options.ContainsKey("dynasty") || options.ContainsKey("base")
        ? OpenDynasty(options)
        : null;

    var report = RosterCsvValidator.Check(
        rosterPath,
        PositionMappingSet.Load(FindDataFile(options, "position-mappings", "PositionMappings.json", required: true)!),
        export,
        options.GetValueOrDefault("team"),
        options.TryGetValue("season", out var season) && int.TryParse(season, out var year) ? year : null,
        RatingEngine.Load(
            FindDataFile(options, "rating-models", "RatingModels.json", required: true)!,
            FindDataFile(options, "overall-formulas", "OverallFormulas.json", required: true)!));

    Console.WriteLine();
    Console.Write(report.ToText());

    // A non-zero exit only for problems that stop generation, so this can be
    // used as a gate without failing on advisory notes.
    return report.CanGenerate ? 0 : 1;
}

static int Generate(Dictionary<string, string> options)
{
    var export = OpenDynasty(options);

    // --historical is the pre-Milestone-3 spelling; accept both.
    var rosterPath = options.TryGetValue("roster", out var roster)
        ? roster
        : options.TryGetValue("historical", out var historicalPath)
            ? historicalPath
            : throw new ArgumentException("Missing required option --roster (your historical roster CSV).");

    var teamOption = options.TryGetValue("team", out var teamValue) ? teamValue : null;
    int? seasonOption = options.TryGetValue("season", out var seasonValue) ? int.Parse(seasonValue) : null;

    // Asking the user questions is the front-end's job; everything after this
    // is the shared pipeline, so the command line and the desktop app cannot
    // grow different behaviour.
    if (!Path.GetExtension(rosterPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
    {
        if (teamOption is null && !CsvHasTeam(rosterPath))
        {
            teamOption = SelectTeamInteractively(export);
        }

        if (seasonOption is null && teamOption is not null && !Console.IsInputRedirected)
        {
            Console.Write("Season (e.g. 2013, Enter to skip): ");
            if (int.TryParse(Console.ReadLine(), out var enteredSeason))
            {
                seasonOption = enteredSeason;
            }
        }
    }

    var ratingsMode = options.GetValueOrDefault("ratings", "generate");
    if (!ratingsMode.Equals("generate", StringComparison.OrdinalIgnoreCase) &&
        !ratingsMode.Equals("inherit", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("--ratings must be 'generate' or 'inherit'.");
    }

    var generateRatings = ratingsMode.Equals("generate", StringComparison.OrdinalIgnoreCase);

    var archetypeMode = options.GetValueOrDefault("archetypes", generateRatings ? "select" : "inherit");
    if (!archetypeMode.Equals("select", StringComparison.OrdinalIgnoreCase) &&
        !archetypeMode.Equals("inherit", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("--archetypes must be 'select' or 'inherit'.");
    }

    var fillMode = options.GetValueOrDefault("fill", generateRatings ? "fill" : "leave");
    if (!fillMode.Equals("fill", StringComparison.OrdinalIgnoreCase) &&
        !fillMode.Equals("leave", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("--fill must be 'fill' or 'leave'.");
    }

    var facesMode = options.GetValueOrDefault("faces", "replace");
    if (!facesMode.Equals("replace", StringComparison.OrdinalIgnoreCase) &&
        !facesMode.Equals("inherit", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("--faces must be 'replace' or 'inherit'.");
    }

    var equipmentMode = options.GetValueOrDefault("equipment", "era");
    if (!equipmentMode.Equals("era", StringComparison.OrdinalIgnoreCase) &&
        !equipmentMode.Equals("leave", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("--equipment must be 'era' or 'leave'.");
    }

    var request = new RosterGenerationRequest
    {
        DynastyPath = DynastyPathOption(options),
        RosterPath = rosterPath,
        DataDirectory = options.GetValueOrDefault("data"),
        Team = teamOption,
        Season = seasonOption,
        OutputPath = options.TryGetValue("output", out var output)
            ? output
            : Path.Combine("Output", "Generated_Roster.csv"),
        ReportPath = options.TryGetValue("report", out var reportOption)
            ? reportOption
            : Path.Combine("Output", "Generation_Report.txt"),
        Ratings = generateRatings ? RatingsMode.Generate : RatingsMode.Inherit,
        SelectArchetypes = archetypeMode.Equals("select", StringComparison.OrdinalIgnoreCase),
        FillRoster = fillMode.Equals("fill", StringComparison.OrdinalIgnoreCase),
        ReplaceRealPersonFaces = facesMode.Equals("replace", StringComparison.OrdinalIgnoreCase),
        ApplyEquipment = equipmentMode.Equals("era", StringComparison.OrdinalIgnoreCase),
        EquipmentOutputPath = options.TryGetValue("equipment-output", out var equipmentOutput)
            ? equipmentOutput
            : Path.Combine("Output", "Generated_Equipment.csv"),
    };

    if (generateRatings)
    {
        Console.WriteLine("Rating generation: on (EA overall formulas driven by historical evidence)");
    }

    if (request.SelectArchetypes)
    {
        Console.WriteLine("Archetype selection: on (PlayerType chosen from each player's profile)");
    }

    if (request.FillRoster)
    {
        Console.WriteLine("Roster fill: on (unsupplied slots re-rated as end-of-roster depth)");
    }

    RosterGenerationResult result;
    try
    {
        result = new RosterGenerationService().Run(request);
    }
    catch (RosterExportException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    foreach (var correction in result.CsvCorrections)
    {
        Console.WriteLine($"  roster CSV: {correction}");
    }

    foreach (var warning in result.CsvWarnings)
    {
        Console.WriteLine($"  roster CSV: {warning}");
    }

    Console.WriteLine($"Historical roster: {result.Conversion.Source.Season} {result.Conversion.Source.School} " +
                      $"— {result.Conversion.Entries.Count} players");
    var slotSummary = result.Filled > 0
        ? $"{result.Filled} slots filled as depth"
        : $"{result.Conversion.LeftoverDonorSlots.Count} donor slots left";
    Console.WriteLine($"Converted {result.Converted} players onto team {result.Conversion.TeamId} " +
                      $"({result.Skipped} skipped, {slotSummary}).");
    Console.WriteLine($"Validation: 0 errors, {result.Export.Report.Warnings.Count()} warnings.");
    Console.WriteLine($"Generated roster: {result.OutputPath} " +
                      $"({result.Export.ChangedColumnsByRowKey.Count} rows modified)");
    Console.WriteLine($"Report:           {result.ReportPath}");

    if (result.Equipment is { } equipment)
    {
        Console.WriteLine(equipment.Describe());
        if (result.EquipmentOutputPath is { } equipmentPath)
        {
            Console.WriteLine($"Equipment table:  {equipmentPath} — import this as well as the roster.");
        }
    }

    return 0;
}

static string DynastyPathOption(Dictionary<string, string> options) =>
    options.TryGetValue("dynasty", out var dynasty)
        ? dynasty
        : options.TryGetValue("base", out var legacy)
            ? legacy
            : throw new ArgumentException(
                "Missing required option --dynasty (the folder of CSV files exported from your " +
                "dynasty, or the Player table CSV itself).");

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
            "The roster CSV has no Team column, no --team was given, and the exported CSVs contain no " +
            "Team table to choose from. Pass --team <name>.");
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
