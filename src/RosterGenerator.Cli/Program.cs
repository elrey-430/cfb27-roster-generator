using RosterGenerator.Core.Comparison;
using RosterGenerator.Core.Legacy;
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
// --dynasty is the dynasty save itself, the folder of CSV files the community
// export tool wrote out of one, or a .zip of that folder. The Player and Team
// tables are discovered inside, so the Player CSV on its own is also accepted.
// Paired with --save-out, a save goes in and a new save comes back.
//
//   generate   --dynasty <folder of exported CSVs> --roster <simple .csv or .json>
//              [--team <name>] [--season <year>] [--output <csv>] [--report <txt|md>]
//              [--ratings generate|inherit] [--team-mappings <json>] [--position-mappings <json>]
//   template   --dynasty <folder of exported CSVs> --season <year> [--output <csv>]
//   list-teams --dynasty <folder of exported CSVs>
//   compare    --left <Player.csv> --right <Player.csv> --team <name or id>
//              [--dynasty <folder of exported CSVs>] [--output <md>]
//   export     --dynasty <save or folder> [--team <name>] [--season <year>]
//              [--output <csv>]
//   import     --legacy <PS2- or PS3-era roster file> --season <year> [--team <name>]
//              [--output <csv>] [--legacy-team-ids <json>]
//
// export writes a team out of a dynasty AS A ROSTER FILE — the same format
// generate reads. Omit --team to write every team the dynasty carries, which
// is a whole season in one file. What the save knows is filled in; the
// evidence columns (stats, awards, combine, draft) are left empty, because a
// save has never held them.
//
// import reads an older NCAA Football roster file into that same format, and
// what it writes depends on which game wrote the file. Identity always crosses
// over exactly. A PS2-era file's ratings do not, because that game held
// eighteen of this one's fifty-seven on a scale nobody has anchored — what
// crosses instead is the ORDER, in the Legacy* columns. NCAA 14 holds
// forty-two on the same 0-99 scale CFB27 uses, so those cross as numbers, in
// the Source* columns. The file is recognised by its own byte order; there is
// no flag to get wrong. --season is required either way: neither records a
// year.
//
// The roster CSV's own Team column decides where each player goes, so one
// file can carry a whole season. --team is only a fallback for rows that
// leave it blank; when the file names teams it is not consulted. --season
// IS a true override, because a season is genuinely roster-wide.
//
// When neither the file nor --team supplies a team, generate asks
// interactively (listing the dynasty's own teams).

try
{
    return args.FirstOrDefault() switch
    {
        "generate" => Generate(ParseOptions(args[1..])),
        "validate" => Validate(ParseOptions(args[1..])),
        "list-teams" => ListTeams(ParseOptions(args[1..])),
        "template" => Template(ParseOptions(args[1..])),
        "compare" => Compare(ParseOptions(args[1..])),
        "export" => Export(ParseOptions(args[1..])),
        "import" => Import(ParseOptions(args[1..])),
        "export-legacy" => ExportLegacy(ParseOptions(args[1..])),
        _ => Usage(),
    };
}
catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or KeyNotFoundException
    or InvalidDataException or RosterGenerator.Core.Csv.CsvSchemaException
    or RosterGenerator.Core.Dynasty.NativeSaveException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int Usage()
{
    Console.Error.WriteLine("""
        Historical CFB27 Roster Generator

        Usage:
          generate   --dynasty <exported CSVs: folder or .zip> --roster <historical roster .csv>
                     [--team <name>] [--season <year>] [--output <csv>] [--report <txt|md>]
                     [--ratings generate|inherit] [--archetypes select|inherit]
                     [--fill fill|leave] [--equipment era|leave] [--faces replace|inherit]
                     [--equipment-output <csv>] [--package <out.zip>] [--save-out <save>]
                     [--dynasty-year <year>|roster]
                     [--team-mappings <json>] [--position-mappings <json>]
          template   --dynasty <exported CSVs: folder or .zip> --season <year>
                     [--output <csv>] [--from-template <csv>]
                     [--fbs-membership <json>] [--roster-skeleton <json>]
          validate   --roster <historical roster .csv>
                     [--dynasty <exported CSVs: folder or .zip>] [--team <name>] [--season <year>]
          list-teams --dynasty <exported CSVs: folder or .zip>
          compare    --left <Player.csv> --right <Player.csv> --team <name or id>
                     [--dynasty <exported CSVs: folder or .zip>] [--output <md>]
          export     --dynasty <save or exported CSVs> [--team <name>] [--season <year>]
                     [--output <csv>]
          import     --legacy <old NCAA Football roster file> --season <year>
                     [--team <name>] [--output <csv>] [--legacy-team-ids <json>]
          export-legacy --dynasty <save or exported CSVs> --legacy <PS2 save or roster file>
                     [--team <name>|all] [--output <file>] [--db-out <file>]
                     [--legacy-team-ids <json>] [--team-mappings <json>]

        export-legacy goes the other way: it writes your CFB27 teams INTO a
        PS2-era roster, over the squads already there. Name a school with
        --team, or leave it out and every school both games have is written in
        one pass. You always get a NEW file; the one you point at is never
        touched.

        --legacy takes your MEMORY-CARD SAVE (.psu) as well as a bare roster
        file, and gives back the same kind you gave it. Point it at a save and
        what comes out goes straight back on the card -- no database editor in
        between. Every other file in the save comes through byte for byte;
        only the roster changes, and only the teams you asked for. Which kind
        of file it is, is read off the file, so there is no flag to get wrong.

        --db-out additionally writes the roster on its own, without the save
        around it, for looking the result over in a database editor first.

        Three things are worth knowing before you use it. A PS2 squad holds
        about 69 players against CFB27's 85, so the depth chart decides who
        comes and everyone cut is named. Nobody changes position on the way,
        so a slot your CFB27 team has nobody for keeps the player it had.
        And that generation stores a rating in five bits, 32 steps across
        0-99, so a rating can move by half a step -- an 84 stays an 84 and a
        77 becomes a 76.

        import turns a roster file from an older NCAA Football game into the
        same roster CSV generate reads, which saves typing a hundred squads by
        hand. Point --legacy at a PS2 memory-card save (.psu), the bare roster
        file out of one, or the USR-DATA inside an NCAA 14 save folder.
        --season is required because neither generation records a year.

        Which game wrote it decides what you get, and it is read off the file
        rather than asked for. A PS2-era file gives identity and the ORDER of
        its players; its ratings are held on a scale nobody has anchored and
        are not written across. NCAA 14 gives identity and forty-two RATINGS on
        the same 0-99 scale CFB27 uses, and those are written across as-is.

        template writes a blank roster file for a whole season: every team
        that played that year, each with its 85 slots and its Team, Season and
        Position already filled in, ready to hand to a spreadsheet. Doing it by
        hand means typing over 11,000 rows, and the easy mistake is invisible —
        CFB27 ships today's 138 teams, so a 2010 file assembled from that list
        silently includes schools that were still in the FCS. Teams that had
        not reached the FBS that season are left out and named on the way past.
        The dates are in data/FbsMembership.json and the position layout in
        data/RosterSkeleton.json; both are plain files you can correct.

        generate then takes that filled file back. One file may carry any
        number of teams: each team's slots are disjoint, so they all convert
        into the single output table you import once.

        --dynasty is the folder of CSV files the community export tool writes
        out of a dynasty — one CSV per table — or a .zip of that folder, which
        is what you get if you moved it off the machine that made it. Either
        works, and it finds the Player and Team tables itself. The Player CSV on
        its own also works, though team names then have to be given with
        --team. Nothing you point it at is ever modified.

        --dynasty also takes your DYNASTY SAVE FILE itself, straight out of
        Documents\EA SPORTS College Football 27\saves. Paired with --save-out
        you get a save back, so there is no export step and no separate
        importer:

          generate --dynasty DYNASTY-BASE1 --roster 2014_FSU.csv
                   --save-out DYNASTY-2014FSU

        Only the fields that actually differ are written, and the empty roster
        slots the game pre-allocates are left exactly as they were. The save
        you supplied is never modified — --save-out is always a new file, and
        writing over the original is refused. This needs Node.js 22.19+ on your
        machine; without it the export-to-CSV route still works.

        --team is a FALLBACK, not a filter. Each player goes to the team their
        own Team cell names, so a file covering all 138 teams generates all 138
        in one run. --team only fills in rows that leave Team blank. To build
        one team, put one team in the file.

        --dynasty-year sets the season the GAME DISPLAYS while you play, so a
        1985 roster is played in 1985 rather than in the year your save started
        in. Give a year, or "roster" to use the one your roster file already
        names:

          generate --dynasty DYNASTY-BASE1 --roster 1985_Roster.csv
                   --save-out DYNASTY-1985 --dynasty-year roster

        It is opt-in, because recreating an old roster inside a present-day
        dynasty is a perfectly reasonable thing to want and rewinding the
        calendar is not something to do to somebody's save uninvited. Only the
        year moves: it writes two fields plus each team's current-season row,
        141 bytes of a 30 MB database, and it needs --save-out because the year
        lives in a table the export tool does not write.

        --package writes your whole dynasty back out as a single .zip with the
        generated tables inside it and every other file copied through byte for
        byte, so you get one archive back instead of loose CSVs to place. It is
        always a NEW archive; the one you supplied is left alone.

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

        A replacement keeps the skin tone the slot already had, so swapping a
        face does not also change how a player looks. The roster CSV's optional
        SkinTone column (EA's 1 lightest to 8 darkest) overrides that per
        player. A tone is never inferred from a name or a hometown -- if you
        leave the column blank the slot's own appearance is kept.

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
// Reads a PS2-era NCAA Football roster file into this tool's roster CSV.
static int Import(Dictionary<string, string> options)
{
    if (!options.TryGetValue("legacy", out var rosterPath))
    {
        throw new ArgumentException("--legacy <roster file> is required.");
    }

    if (!File.Exists(rosterPath))
    {
        throw new FileNotFoundException($"There is no roster file at '{Path.GetFullPath(rosterPath)}'.");
    }

    // The file records no year at all, and guessing one from the players on it
    // would be a research result presented as a fact the file supplied.
    if (!options.TryGetValue("season", out var seasonText) ||
        !int.TryParse(seasonText, out var season) || season <= 0)
    {
        throw new ArgumentException(
            "--season <year> is required: a legacy roster file does not record which season it is.");
    }

    var teamIds = RosterGenerator.Core.Legacy.LegacyRosterImporter.LoadTeamIds(
        FindDataFile(options, "legacy-team-ids", "LegacyTeamIds.json", required: true)!);
    var output = options.GetValueOrDefault("output", "ImportedRoster.csv");
    var result = RosterGenerator.Core.Legacy.LegacyRosterImporter.Import(
        rosterPath, output, teamIds, season, options.GetValueOrDefault("team"));

    Console.Error.WriteLine(
        $"wrote {result.Path}: {result.Players} player(s) across {result.Teams} team(s), season {season}");
    foreach (var note in result.Notes)
    {
        Console.Error.WriteLine($"  {note}");
    }

    Console.Error.WriteLine(result.CarriedRatings
        ? "  Ratings ARE imported: this generation records them on the same 0-99 scale CFB27 uses, so " +
          "the 42 columns it holds were written out as Source* columns. Generating moves them as a group " +
          "onto this game's scale, and fills the 15 columns it never had from the archetype's measured " +
          "profile."
        : "  Ratings are NOT imported: this generation held 18 of CFB27's 57 columns, on a scale nobody " +
          "has anchored. What crossed over is the ORDER. Fill in stats, awards or a draft pick to rate " +
          "these players on their own record.");
    return 0;
}

static int ExportLegacy(Dictionary<string, string> options)
{
    if (!options.TryGetValue("legacy", out var legacyPath))
    {
        throw new ArgumentException("--legacy <PS2 roster file> is required.");
    }

    if (!File.Exists(legacyPath))
    {
        throw new FileNotFoundException($"There is no roster file at '{Path.GetFullPath(legacyPath)}'.");
    }

    if (!options.TryGetValue("dynasty", out var dynastyPath))
    {
        throw new ArgumentException("--dynasty <save or exported CSVs> is required.");
    }

    var teamIds = FindDataFile(options, "legacy-team-ids", "LegacyTeamIds.json", required: true)!;
    var mappings = TeamMappingSet.Load(
        FindDataFile(options, "team-mappings", "TeamMappings.json", required: true)!);
    var scale = LegacyRatingScale.Load(
        FindDataFile(options, "legacy-rating-scale", "LegacyRatingScale.json", required: true)!);

    // A save has to be unpacked before its Player table can be read, and that
    // takes long enough that saying nothing looks like a hang.
    Console.Error.WriteLine(NativeSave.LooksLikeSave(dynastyPath)
        ? "Reading your dynasty save — this takes a few seconds…"
        : "Reading your dynasty…");
    using var package = DynastyPackage.Open(dynastyPath);
    var roster = package.Export.LoadPlayerRoster();

    // The save names its own schools, so its aliases beat the shipped file
    // wherever the two disagree.
    if (package.Export.Teams.Count > 0)
    {
        mappings = package.Export.BuildTeamMappings(
            FindDataFile(options, "team-mappings", "TeamMappings.json", required: false));
    }

    var wanted = options.GetValueOrDefault("team");
    IReadOnlyList<LegacyExportTeam> teams;
    IReadOnlyList<string> unpaired = Array.Empty<string>();
    if (wanted is null || wanted.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        teams = LegacyTeamPairing.Pair(teamIds, mappings, out unpaired);
        Console.Error.WriteLine($"Writing every school both games have: {teams.Count} team(s).");
    }
    else
    {
        var one = LegacyTeamPairing.Find(teamIds, mappings, wanted)
            ?? throw new ArgumentException(
                $"'{wanted}' is not a school both games have. The PS2 file carries " +
                $"{LegacyTeamPairing.Schools(teamIds).Count} of them; --team all writes every one.");
        teams = new[] { one };
    }

    var output = options.GetValueOrDefault(
        "output", Path.Combine("Output", Path.GetFileName(legacyPath)));
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

    // Asked for separately, because wanting the roster on its own as well as
    // in the save is an ordinary thing to want and running the export twice to
    // get it is not.
    var databaseOut = options.GetValueOrDefault("db-out");
    if (databaseOut is { Length: > 0 })
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databaseOut))!);
    }

    // Who comes is the coach's call before it is the ratings'. Without the
    // chart the cut falls back to overall, which is a different squad wherever
    // a starter is not the highest-rated man at his position.
    var result = LegacyRosterExporter.Export(
        legacyPath, output, roster, teams, scale, LegacyDepthChart.For(package.Export), databaseOut);

    var written = result.Teams.Sum(t => t.Written.Count);
    var cut = result.Teams.Sum(t => t.Cut.Count);
    var kept = result.Teams.Sum(t => t.Unfilled.Count);
    var charted = result.Teams.Count(t => t.DepthChartDecided);
    Console.Error.WriteLine($"read {Path.GetFileName(legacyPath)}: {result.SourceDescription}.");
    Console.Error.WriteLine(
        $"wrote {result.Path}: {written} player(s) across {result.Teams.Count} team(s), " +
        $"{cut} cut, {kept} slot(s) left as they were");
    Console.Error.WriteLine(result.WroteSave
        ? "  That is a memory-card save, so it goes straight back on the card — no database editor " +
          "in between. Every other file in it came through untouched."
        : "  That is a bare roster file, the kind a database editor opens. Point --legacy at a .psu " +
          "save instead and you get a save back.");
    if (result.DatabasePath is { } databasePath)
    {
        Console.Error.WriteLine($"  Roster on its own also written to {databasePath}.");
    }
    Console.Error.WriteLine(charted == result.Teams.Count
        ? "  Your dynasty's own depth chart decided who came, at every team."
        : charted == 0
            ? "  This dynasty carries no depth chart, so the cut fell back to overall."
            : $"  The depth chart decided {charted} of {result.Teams.Count} team(s); the rest fell back " +
              "to overall, having no chart in this dynasty.");

    foreach (var team in result.Teams.Where(t => t.Notes.Count > 0))
    {
        foreach (var note in team.Notes)
        {
            Console.Error.WriteLine($"  {team.Team}: {note}");
        }
    }

    foreach (var line in result.Skipped.Concat(unpaired))
    {
        Console.Error.WriteLine($"  {line}");
    }

    Console.Error.WriteLine(
        "  Ratings went through the measured five-bit scale, so one can move by half a step. " +
        "Everything the PS2 format cannot hold — the other 39 rating columns, and anything past " +
        "13 characters of a surname — is listed above rather than silently dropped.");
    return 0;
}

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

static DynastyPackage OpenDynastyPackage(Dictionary<string, string> options)
{
    // --base is the pre-Milestone-3 spelling; accept both.
    var path = options.TryGetValue("dynasty", out var dynasty)
        ? dynasty
        : options.TryGetValue("base", out var basePath)
            ? basePath
            : throw new ArgumentException(
                "Missing required option --dynasty (the folder of CSV files exported from your " +
                "dynasty, or the Player table CSV itself).");
    // Said before the work starts, not after. Opening a dynasty save means
    // unpacking 30 MB of bit-packed tables, which takes twenty seconds or so;
    // printing nothing until it finished made a working program look like a
    // hung one.
    if (NativeSave.LooksLikeSave(path))
    {
        Console.WriteLine($"Reading dynasty save {Path.GetFileName(path)} — this takes a few seconds…");
    }

    // A .zip of the export folder, or the dynasty save itself, is accepted
    // wherever the folder is. The caller owns the package: both of those are
    // expanded into a scratch folder that disposing deletes.
    var package = DynastyPackage.Open(path);
    Console.WriteLine($"Player table: {package.Export.PlayerTablePath}");
    Console.WriteLine($"  {package.Export.Teams.Count} teams discovered");
    return package;
}

static int ListTeams(Dictionary<string, string> options)
{
    using var package = OpenDynastyPackage(options);
    var export = package.Export;
    Console.WriteLine();
    Console.WriteLine("Available teams:");
    foreach (var team in export.Teams)
    {
        Console.WriteLine($"  {team}");
    }

    return 0;
}

// Writes the blank roster template for a whole season: every team that played
// that year, each with its 85 slots, Team/Season/Position already filled in.
static int Export(Dictionary<string, string> options)
{
    int? season = null;
    if (options.TryGetValue("season", out var seasonText))
    {
        if (!int.TryParse(seasonText, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"--season '{seasonText}' is not a year.");
        }

        season = parsed;
    }

    using var package = OpenDynastyPackage(options);
    var export = package.Export;
    var roster = export.LoadPlayerRoster();

    IReadOnlyList<int>? teams = null;
    if (options.TryGetValue("team", out var teamName))
    {
        teams = new[] { export.BuildTeamMappings().Resolve(teamName) };
    }

    // Roles come from the dynasty's own depth chart when it carries one, so an
    // exported file says who actually starts rather than leaving it to guess.
    var slotsPath = FindDataFile(options, "depth-chart-slots", "DepthChartSlots.json", required: false);
    var charts = slotsPath is null
        ? null
        : RosterGenerator.Core.Depth.DepthChartTable.Open(
            Path.GetDirectoryName(export.PlayerTablePath) ?? ".");

    var output = options.TryGetValue("output", out var chosen)
        ? chosen
        : Path.Combine("Output", "Exported_Roster.csv");

    var result = RosterCsvExporter.Write(
        export, roster, output, teams, season, export.LoadCharacterVisuals(),
        charts,
        slotsPath is null ? null : RosterGenerator.Core.Depth.DepthChartSlotModel.Load(slotsPath));

    Console.WriteLine();
    Console.WriteLine($"Roster file: {result.Path}");
    Console.WriteLine($"  {result.Players} player(s) across {result.Teams.Count} team(s).");
    Console.WriteLine(result.RolesFromDepthChart
        ? $"  Roles read from the dynasty's depth chart — {result.Starters} starter(s)."
        : "  Role left blank: this dynasty carries no depth chart.");
    Console.WriteLine(
        "  Stats, awards, combine numbers and draft slots are empty — a save has never held them. " +
        "Fill them in and the ratings become yours.");
    return 0;
}

static int Template(Dictionary<string, string> options)
{
    var seasonText = Require(options, "season");
    if (!int.TryParse(seasonText, out var season) || season <= 0)
    {
        throw new ArgumentException($"--season '{seasonText}' is not a year.");
    }

    using var package = OpenDynastyPackage(options);
    var export = package.Export;
    var teams = export.Teams.Select(t => t.DisplayName).ToList();

    var membershipPath = FindDataFile(options, "fbs-membership", "FbsMembership.json", required: false);
    var membership = membershipPath is null ? FbsMembership.Empty : FbsMembership.Load(membershipPath);
    if (membershipPath is null)
    {
        Console.WriteLine(
            "No FbsMembership.json found, so every team in the dynasty is included — including any that " +
            "had not reached the FBS in " + season + ".");
    }

    var skeletonPath = FindDataFile(options, "roster-skeleton", "RosterSkeleton.json", required: true)!;
    var templatePath = options.TryGetValue("from-template", out var explicitTemplate)
        ? explicitTemplate
        : FindTemplate();

    var output = options.TryGetValue("output", out var chosen)
        ? chosen
        : Path.Combine("Output", $"{season}_AllTeams_Template.csv");

    var result = SeasonTemplateWriter.Load(skeletonPath)
        .Write(output, templatePath, teams, season, membership);

    Console.WriteLine();
    Console.WriteLine($"Blank template for {season}: {result.Path}");
    Console.WriteLine(
        $"  {result.Teams} teams x {result.SlotsPerTeam} roster slots = {result.Rows} rows to fill in.");

    if (result.Excluded.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"Left out ({result.Excluded.Count}) — not FBS in {season}:");
        foreach (var problem in result.Excluded.OrderBy(p => p.School, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {problem.Reason}.");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Membership dates live in data/FbsMembership.json and are yours to correct.");
    }

    return 0;
}

// The shipped template supplies the header, so the blank file and the
// documented format cannot drift apart.
static string FindTemplate()
{
    foreach (var candidate in new[]
             {
                 Path.Combine("templates", "HistoricalRosterTemplate.csv"),
                 Path.Combine(AppContext.BaseDirectory, "templates", "HistoricalRosterTemplate.csv"),
             })
    {
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    throw new ArgumentException(
        "templates/HistoricalRosterTemplate.csv was not found next to the executable or the current " +
        "directory. Pass --from-template to point at it.");
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
    using var dynastyPackage = options.ContainsKey("dynasty") || options.ContainsKey("base")
        ? OpenDynastyPackage(options)
        : null;
    var export = dynastyPackage?.Export;

    var report = RosterCsvValidator.Check(
        rosterPath,
        PositionMappingSet.Load(FindDataFile(options, "position-mappings", "PositionMappings.json", required: true)!),
        export,
        options.GetValueOrDefault("team"),
        options.TryGetValue("season", out var season) && int.TryParse(season, out var year) ? year : null,
        RatingEngine.Load(
            FindDataFile(options, "rating-models", "RatingModels.json", required: true)!,
            FindDataFile(options, "overall-formulas", "OverallFormulas.json", required: true)!,
            FindDataFile(options, "archetype-profiles", "ArchetypeProfiles.json", required: false)),
        FindDataFile(options, "fbs-membership", "FbsMembership.json", required: false) is { } fbs
            ? FbsMembership.Load(fbs)
            : null);

    Console.WriteLine();
    Console.Write(report.ToText());

    // A non-zero exit only for problems that stop generation, so this can be
    // used as a gate without failing on advisory notes.
    return report.CanGenerate ? 0 : 1;
}

static int Generate(Dictionary<string, string> options)
{
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
            // Opened only to ask the question. The pipeline opens the dynasty
            // itself, and opening a save is expensive enough that doing it
            // twice for nothing is worth avoiding.
            using var package = OpenDynastyPackage(options);
            teamOption = SelectTeamInteractively(package.Export);
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
        PackageOutputPath = options.GetValueOrDefault("package"),
        SaveOutputPath = options.GetValueOrDefault("save-out"),
        DynastyYear = DynastyYearOption(options, seasonOption, rosterPath),
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
        result = new RosterGenerationService { Progress = Console.WriteLine }.Run(request);
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

    var slotSummary = result.Filled > 0
        ? $"{result.Filled} slots filled as depth"
        : $"{result.Teams.Sum(t => t.LeftoverDonorSlots.Count)} donor slots left";

    if (result.Teams.Count == 1)
    {
        var only = result.Teams[0];

        // An all-time roster is one team spanning decades, so naming a single
        // year would be the least useful thing to print about it.
        var years = only.Entries
            .Select(e => e.Player.Season ?? only.Source.Season)
            .Where(s => s > 0)
            .Distinct()
            .OrderBy(s => s)
            .ToList();
        var when = years.Count > 1 ? $"{years[0]}-{years[^1]}" : $"{only.Source.Season}";
        Console.WriteLine($"Historical roster: {when} {only.Source.School} " +
                          $"— {only.Entries.Count} players");
        Console.WriteLine($"Converted {result.Converted} players onto team {only.TeamId} " +
                          $"({result.Skipped} skipped, {slotSummary}).");
    }
    else
    {
        // A whole season would print 119 near-identical lines here, so the
        // teams are summarised and then spelled out one by one in the report.
        var seasons = result.Teams.Select(t => t.Source.Season).Distinct().OrderBy(s => s).ToList();
        var span = seasons.Count == 1 ? $"{seasons[0]}" : $"{seasons[0]}-{seasons[^1]}";
        Console.WriteLine($"Historical roster: {span} — {result.Teams.Count} teams, " +
                          $"{result.Teams.Sum(t => t.Entries.Count)} players supplied");
        Console.WriteLine($"Converted {result.Converted} players across {result.Teams.Count} teams " +
                          $"({result.Skipped} skipped, {slotSummary}).");
    }

    // Teams the dynasty does not carry are reported, never silently dropped.
    foreach (var skipped in result.Teams[0].GlobalWarnings.Where(w => w.StartsWith("Skipped —")))
    {
        Console.WriteLine($"  {skipped}");
    }

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

    if (result.SaveOutput is { } save)
    {
        Console.WriteLine(
            $"Dynasty save:     {save.Destination} ({save.Bytes:N0} bytes) — " +
            $"{save.CellsChanged:N0} field(s) written across {save.Tables.Count} table(s); " +
            $"{save.EmptyRecordsSkipped:N0} empty roster slot(s) left untouched.");
        if (save.SeasonYearChanged)
        {
            Console.WriteLine(
                $"                  The game will show {save.SeasonYearTo} rather than " +
                $"{save.SeasonYearFrom}.");
        }

        Console.WriteLine(
            "                  Copy it into your CFB27 saves folder. Your original is unchanged.");
    }

    if (result.PackageOutputPath is { } packagePath)
    {
        var tables = result.PackagedTables ?? Array.Empty<string>();
        Console.WriteLine(
            $"Dynasty package:  {packagePath} — your whole dynasty with {tables.Count} table(s) replaced " +
            $"({string.Join(", ", tables.Select(Path.GetFileName))}). Everything else is byte-for-byte " +
            "what you gave it.");
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

/// <summary>
/// Resolves <c>--dynasty-year</c>: an explicit season, or "roster" to take the
/// one the roster file already names.
///
/// <para>It is opt-in rather than automatic. Recreating a 1985 roster inside a
/// present-day dynasty is a perfectly reasonable thing to want, and silently
/// rewinding somebody's dynasty to 1985 because their roster file said so
/// would be the tool making a decision that is not its to make.</para>
/// </summary>
static int? DynastyYearOption(
    Dictionary<string, string> options, int? seasonOption, string rosterPath)
{
    if (!options.TryGetValue("dynasty-year", out var value) || value.Length == 0)
    {
        return null;
    }

    if (value.Equals("roster", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("auto", StringComparison.OrdinalIgnoreCase))
    {
        var fromRoster = seasonOption
            ?? RosterGenerationService.ReadRoster(new RosterGenerationRequest
            {
                DynastyPath = "", RosterPath = rosterPath, Season = seasonOption,
            }).Roster.Season;

        return fromRoster > 0
            ? fromRoster
            : throw new ArgumentException(
                "--dynasty-year roster needs a season to read: the roster file has no Season column " +
                "and --season was not given. Pass a year instead, e.g. --dynasty-year 1985.");
    }

    if (!int.TryParse(value, out var year))
    {
        throw new ArgumentException(
            $"--dynasty-year must be a season, or 'roster' to use the roster file's own; got '{value}'.");
    }

    if (!NativeSave.IsSupportedSeason(year))
    {
        throw new ArgumentException(
            $"--dynasty-year must be between {NativeSave.FirstSeason} and {NativeSave.LastSeason}; " +
            $"got {year}.");
    }

    return year;
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
            ? OpenDynastyPackage(options).Export.BuildTeamMappings(FindDataFile(options, "team-mappings", "TeamMappings.json", required: false))
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
