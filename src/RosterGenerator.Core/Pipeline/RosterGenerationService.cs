using RosterGenerator.Core.Conversion;
using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Equipment;
using RosterGenerator.Core.Export;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Validation;

namespace RosterGenerator.Core.Pipeline;

/// <summary>Whether ratings are generated or taken from the players being replaced.</summary>
public enum RatingsMode
{
    /// <summary>Build every attribute from the historical evidence.</summary>
    Generate,

    /// <summary>Keep the ratings of the roster slot each player takes over.</summary>
    Inherit,
}

/// <summary>Everything one generation run needs.</summary>
public sealed record RosterGenerationRequest
{
    /// <summary>The dynasty export folder, or a lone Player table CSV.</summary>
    public required string DynastyPath { get; init; }

    /// <summary>The user's roster CSV (or a Milestone 2 JSON dataset).</summary>
    public required string RosterPath { get; init; }

    /// <summary>Folder holding the JSON data files. Defaults to <c>data/</c> beside the executable.</summary>
    public string? DataDirectory { get; init; }

    /// <summary>Team override; null uses the roster file's Team column.</summary>
    public string? Team { get; init; }

    /// <summary>Season override; null uses the roster file's Season column.</summary>
    public int? Season { get; init; }

    /// <summary>Where the import-ready player table is written.</summary>
    public string OutputPath { get; init; } = Path.Combine("Output", "Generated_Roster.csv");

    /// <summary>Where the plain-text generation report is written.</summary>
    public string ReportPath { get; init; } = Path.Combine("Output", "Generation_Report.txt");

    /// <summary>Whether to generate ratings.</summary>
    public RatingsMode Ratings { get; init; } = RatingsMode.Generate;

    /// <summary>Choose each player's archetype. Requires <see cref="RatingsMode.Generate"/>.</summary>
    public bool SelectArchetypes { get; init; } = true;

    /// <summary>Fill unsupplied slots as depth. Requires <see cref="RatingsMode.Generate"/>.</summary>
    public bool FillRoster { get; init; } = true;

    /// <summary>
    /// Give a historical player a generated face when the roster slot they
    /// take over carried a real person's head scan. Most slots do, so leaving
    /// them puts most of a recreated roster in the recognisable faces of
    /// present-day players under other people's names.
    /// </summary>
    public bool ReplaceRealPersonFaces { get; init; } = true;

    /// <summary>
    /// Put period-correct helmets on the team, chosen from the season already
    /// being recreated. Silently does nothing when the export carries no
    /// CharacterVisuals table or no era covers the season.
    /// </summary>
    public bool ApplyEquipment { get; init; } = true;

    /// <summary>Where the modified equipment table is written, when one is produced.</summary>
    public string EquipmentOutputPath { get; init; } =
        Path.Combine("Output", "Generated_Equipment.csv");

    /// <summary>
    /// Write the whole dynasty back out as a single <c>.zip</c> at this path,
    /// with the generated tables inside it and every other file copied through
    /// untouched. Null writes only the loose CSVs, as before.
    ///
    /// The dynasty that came in is never modified — a package is always a new
    /// archive, so a user who dislikes the result still has their original.
    /// </summary>
    public string? PackageOutputPath { get; init; }

    /// <summary>
    /// Write the dynasty back out as a <b>save file</b> at this path, ready to
    /// drop straight into the game's saves folder — no export, no separate
    /// importer. Requires <see cref="DynastyPath"/> to be a save rather than
    /// an export folder, because a save is what gets written into.
    ///
    /// The save that came in is never modified: this is always a new file.
    /// </summary>
    public string? SaveOutputPath { get; init; }
}

/// <summary>What a generation run produced.</summary>
/// <param name="Conversion">
/// The first team's conversion report. A roster file naming one team — still
/// the common case — has only this one.
/// </param>
/// <param name="Export">Validation report plus the per-row changed columns.</param>
/// <param name="OutputPath">Where the player table was written.</param>
/// <param name="ReportPath">Where the report was written.</param>
/// <param name="CsvWarnings">Roster CSV values that could not be used as written.</param>
/// <param name="CsvCorrections">Roster CSV values that were cleaned up and used.</param>
/// <param name="Equipment">What the era did to the team's head gear, if anything.</param>
/// <param name="EquipmentOutputPath">Where the equipment table went, when one was written.</param>
/// <param name="PackageOutputPath">Where the whole dynasty was written back, when asked for.</param>
/// <param name="PackagedTables">Tables substituted inside that package.</param>
/// <param name="Conversions">
/// Every team the run converted, in file order. A whole-season file carries
/// one per school; the tallies below are over all of them, so a caller that
/// only reports totals needs no changes to handle a season.
/// </param>
/// <param name="SaveOutput">What was written when a dynasty save was asked for.</param>
public sealed record RosterGenerationResult(
    ConversionReport Conversion,
    ExportResult Export,
    string OutputPath,
    string ReportPath,
    IReadOnlyList<string> CsvWarnings,
    IReadOnlyList<string> CsvCorrections,
    EquipmentReport? Equipment = null,
    string? EquipmentOutputPath = null,
    string? PackageOutputPath = null,
    IReadOnlyList<string>? PackagedTables = null,
    IReadOnlyList<ConversionReport>? Conversions = null,
    Dynasty.NativeSaveWriteReport? SaveOutput = null)
{
    /// <summary>Every team converted, never empty.</summary>
    public IReadOnlyList<ConversionReport> Teams =>
        Conversions is { Count: > 0 } all ? all : new[] { Conversion };

    /// <summary>Players written to the save, across every team.</summary>
    public int Converted => Teams.Sum(t => t.Converted.Count());

    /// <summary>Players that could not be placed, across every team.</summary>
    public int Skipped => Teams.Sum(t => t.Skipped.Count());

    /// <summary>Slots filled as end-of-roster depth, across every team.</summary>
    public int Filled => Teams.Sum(t => t.FilledSlots.Count);
}

/// <summary>
/// The generation pipeline, in one place.
///
/// It exists so the command line and the desktop app cannot diverge. Every
/// decision that shapes a roster — which archetype rules apply, that filling
/// slots requires the rating engine, that the program adjustment needs the
/// depth model, which data file supplies what — lives here once, so a fix
/// reaches both front-ends and neither can quietly grow its own behaviour.
/// The front-ends are left with what they should own: asking the user
/// questions and displaying the answer.
/// </summary>
public sealed class RosterGenerationService
{
    /// <summary>
    /// Called with a plain-English line whenever the run reaches a step slow
    /// enough that silence would look like a hang. Optional: a caller that
    /// does not set it simply gets no progress.
    /// </summary>
    public Action<string>? Progress { get; init; }

    /// <summary>
    /// Opens a dynasty for listing teams before generating. Accepts a dynasty
    /// save, the folder of exported CSVs, the Player CSV alone, or a
    /// <c>.zip</c> of the folder.
    ///
    /// <para><b>The caller owns the returned package and must dispose it.</b>
    /// An archive and a save are both expanded into a scratch folder that
    /// disposal deletes; dropping the package on the floor leaves a copy of
    /// the dynasty's tables in the temp folder every time one is opened.</para>
    /// </summary>
    public static DynastyPackage OpenDynasty(string path) => DynastyPackage.Open(path);

    /// <summary>
    /// Resolves a data file: an explicit folder if given, otherwise
    /// <c>data/</c> beside the executable, then the current directory.
    /// </summary>
    public static string FindDataFile(string? dataDirectory, string fileName) =>
        FindOptionalDataFile(dataDirectory, fileName)
        ?? throw new FileNotFoundException(
            $"Could not find the data file '{fileName}'. It should sit in a 'data' folder next to the " +
            "application. Looked in: " + string.Join("; ", DataFileCandidates(dataDirectory, fileName)));

    /// <summary>
    /// Resolves a data file the same way as <see cref="FindDataFile"/> but
    /// returns null when it is absent, for files the tool can work without.
    /// </summary>
    public static string? FindOptionalDataFile(string? dataDirectory, string fileName) =>
        DataFileCandidates(dataDirectory, fileName).FirstOrDefault(File.Exists);

    private static List<string> DataFileCandidates(string? dataDirectory, string fileName)
    {
        var candidates = new List<string>();
        if (dataDirectory is { Length: > 0 })
        {
            candidates.Add(Path.Combine(dataDirectory, fileName));
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "data", fileName));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "data", fileName));
        return candidates;
    }

    /// <summary>Reads the roster file, applying the request's team and season overrides.</summary>
    public static HistoricalCsvResult ReadRoster(RosterGenerationRequest request)
    {
        if (Path.GetExtension(request.RosterPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            var loaded = HistoricalRoster.Load(request.RosterPath);
            if (request.Team is not null)
            {
                loaded = loaded with { School = request.Team };
            }

            if (request.Season is int season)
            {
                loaded = loaded with { Season = season };
            }

            return new HistoricalCsvResult(loaded, Array.Empty<string>());
        }

        return HistoricalCsv.Read(request.RosterPath, request.Team, request.Season);
    }

    /// <summary>
    /// Runs the whole pipeline: open the dynasty, read the roster, convert,
    /// validate, export, and write the report.
    /// </summary>
    /// <exception cref="RosterExportException">Validation rejected the result; nothing was written.</exception>
    public RosterGenerationResult Run(RosterGenerationRequest request)
    {
        // The dynasty may be a folder, a .zip of one, or the save itself; the
        // package owns the difference, and owns cleaning up after anything it
        // expanded. Opening a save is the slow step of the whole run, so the
        // caller is told before it starts rather than after.
        Progress?.Invoke(NativeSave.LooksLikeSave(request.DynastyPath)
            ? "Reading your dynasty save — this takes a few seconds…"
            : "Reading your dynasty…");
        using var package = DynastyPackage.Open(request.DynastyPath);
        var export = package.Export;
        var data = request.DataDirectory;

        var positionMappings = PositionMappingSet.Load(FindDataFile(data, "PositionMappings.json"));
        var teamAliases = TryFindDataFile(data, "TeamMappings.json");
        var teamMappings = export.Teams.Count > 0
            ? export.BuildTeamMappings(teamAliases)
            : TeamMappingSet.Load(teamAliases
                ?? throw new FileNotFoundException(
                    "The exported CSVs contain no Team table and TeamMappings.json was not found."));

        var roster = ReadRoster(request);

        RatingEngine? ratingEngine = null;
        OverallFormulaSet? formulas = null;
        if (request.Ratings == RatingsMode.Generate)
        {
            var formulaPath = FindDataFile(data, "OverallFormulas.json");
            formulas = OverallFormulaSet.Load(formulaPath);
            ratingEngine = RatingEngine.Load(
                FindDataFile(data, "RatingModels.json"), formulaPath,
                FindOptionalDataFile(data, "ArchetypeProfiles.json"));
        }

        // Both of these write ratings, so both need the engine. Silently
        // ignoring the request would be worse than refusing it.
        if (request.SelectArchetypes && ratingEngine is null)
        {
            throw new ArgumentException(
                "Choosing archetypes requires rating generation: the archetype decides which of EA's " +
                "overall formulas applies, so the overall must be recomputed at the same time.");
        }

        if (request.FillRoster && ratingEngine is null)
        {
            throw new ArgumentException(
                "Filling the rest of the roster requires rating generation: filling a slot means writing " +
                "a rating for it.");
        }

        var archetypeSelector = request.SelectArchetypes && ratingEngine is not null
            ? ArchetypeSelector.Load(FindDataFile(data, "ArchetypeRules.json"))
            : null;

        // The depth model is loaded whenever ratings are generated, not only
        // when filling: it also tells the engine how strong the program is.
        var depth = ratingEngine is not null
            ? RosterDepthModel.Load(FindDataFile(data, "RosterDepth.json"))
            : null;
        var filler = request.FillRoster && ratingEngine is not null && depth is not null
            ? new RosterFiller(depth, ratingEngine)
            : null;

        var donor = export.LoadPlayerRoster();
        var session = new RosterEditSession(donor);

        // Loaded before conversion, not just for equipment afterwards: a real
        // person's head scan does not spell its skin tone out the way a
        // generated head does, so this is the only place to read the tone a
        // roster slot already has and keep it through a face swap.
        var visuals = request.ReplaceRealPersonFaces || request.ApplyEquipment
            ? export.LoadCharacterVisuals()
            : null;

        // Optional: a missing file leaves commentary alone rather than
        // refusing to generate, and the report says which happened.
        var commentaryPath = FindOptionalDataFile(data, "CommentaryIds.json");
        var commentaryIds = commentaryPath is null
            ? CommentaryIdSet.Empty
            : CommentaryIdSet.Load(commentaryPath);

        var converter = new HistoricalTeamConverter(
            teamMappings, positionMappings, ratingEngine, archetypeSelector, filler, depth,
            export.BuildPreviousSchoolMappings(teamAliases), request.ReplaceRealPersonFaces,
            visuals, commentaryIds);

        // One file can carry a whole season. Each team's slots are disjoint, so
        // they convert one after another into the same session and the single
        // output table ends up holding all of them.
        var teams = roster.Rosters.Count > 0
            ? roster.Rosters
            : new List<HistoricalRoster> { roster.Roster };
        var conversions = new List<ConversionReport>();
        var failures = new List<string>();
        foreach (var team in teams)
        {
            try
            {
                conversions.Add(converter.Convert(session, team));
            }
            catch (KeyNotFoundException ex)
            {
                // A whole season's file naming one school this dynasty does not
                // carry must not cost the user the other 130 teams.
                failures.Add($"{team.School}: {ex.Message}");
            }
        }

        if (conversions.Count == 0)
        {
            throw new Csv.CsvSchemaException(
                "No team in the roster file could be matched to this dynasty. " + string.Join(" ", failures));
        }

        var conversion = conversions[0];
        foreach (var failure in failures)
        {
            conversion.GlobalWarnings.Add($"Skipped — {failure}");
        }

        CreateParentDirectory(request.OutputPath);
        CreateParentDirectory(request.ReportPath);

        var result = new RosterExporter().Export(
            new RosterValidationContext(
                donor, session, overallFormulas: formulas, commentaryIds: commentaryIds),
            request.OutputPath);

        // Equipment is a second table, so it is applied after the player table
        // has validated and been written: a run that refuses to produce a
        // roster must not leave a stray equipment file beside it.
        var (equipment, equipmentPath) = ApplyEquipment(request, export, donor, conversions, data, visuals);

        var markdown = Path.GetExtension(request.ReportPath)
            .Equals(".md", StringComparison.OrdinalIgnoreCase);
        var body = string.Join(
            Environment.NewLine + Environment.NewLine,
            conversions.Select(c => markdown ? c.ToMarkdown() : c.ToText()));
        File.WriteAllText(request.ReportPath,
            markdown ? body : body + EquipmentSection(equipment, equipmentPath));

        // Finally, if asked for, hand the whole dynasty back as one archive.
        // This happens last for the same reason equipment does: a run that
        // refused to produce a roster must not leave a package behind
        // suggesting it succeeded.
        string? packagePath = null;
        IReadOnlyList<string>? packaged = null;
        if (request.PackageOutputPath is { Length: > 0 } destination)
        {
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [export.PlayerTablePath] = request.OutputPath,
            };
            if (equipmentPath is not null && export.CharacterVisualsPath is not null)
            {
                replacements[export.CharacterVisualsPath] = equipmentPath;
            }

            CreateParentDirectory(destination);
            packaged = package.WriteArchive(destination, replacements);
            packagePath = destination;
        }

        // And the shortest path of all: the dynasty goes back out as a save,
        // which is what the user actually has and what the game actually
        // loads. Last, for the same reason as the two above — a run that
        // refused to produce a roster must not leave a save behind implying
        // it succeeded.
        NativeSaveWriteReport? saveOutput = null;
        if (request.SaveOutputPath is { Length: > 0 } saveDestination)
        {
            if (!package.IsNativeSave)
            {
                throw new NativeSaveException(
                    "Writing a dynasty save needs a dynasty save to write into. Point --dynasty at your " +
                    "save file rather than at an export folder.");
            }

            var tables = new List<string> { request.OutputPath };
            if (equipmentPath is not null)
            {
                tables.Add(equipmentPath);
            }

            saveOutput = package.WriteSave(saveDestination, tables);
        }

        return new RosterGenerationResult(
            conversion, result, request.OutputPath, request.ReportPath,
            roster.Warnings, roster.Corrections, equipment, equipmentPath, packagePath, packaged,
            conversions, saveOutput);
    }

    /// <summary>
    /// Puts period-correct helmets on every converted team and writes the
    /// equipment table beside the roster. Returns nulls — and writes nothing —
    /// when equipment was not requested, the export carries no
    /// CharacterVisuals table, the season is unknown, or no era covers it.
    /// </summary>
    private static (EquipmentReport? Report, string? Path) ApplyEquipment(
        RosterGenerationRequest request,
        DynastyExport export,
        PlayerRoster donor,
        IReadOnlyList<ConversionReport> conversions,
        string? data,
        Equipment.CharacterVisualsTable? visuals)
    {
        var dated = conversions.Where(c => c.Source.Season > 0).ToList();
        if (!request.ApplyEquipment || visuals is null || dated.Count == 0)
        {
            return (null, null);
        }

        var erasPath = TryFindDataFile(data, "EquipmentEras.json");
        if (erasPath is null)
        {
            return (null, null);
        }

        // Teams sharing a season share an era, so they are rehelmeted together
        // in one pass. A file that mixes seasons gets a pass per season and the
        // reports are folded into one summary.
        var applier = new EquipmentApplier(EquipmentEraSet.Load(erasPath));
        var report = EquipmentReport.Merge(dated
            .GroupBy(c => c.Source.Season)
            .Select(season => applier.Apply(
                donor, visuals, season.Select(c => c.TeamId).Distinct().ToList(), season.Key))
            .ToList());

        // Nothing changed means nothing to import; writing a 30 MB copy of the
        // user's own table would just be another file to explain.
        if (report.Changed.Count == 0)
        {
            return (report, null);
        }

        CreateParentDirectory(request.EquipmentOutputPath);
        visuals.Save(request.EquipmentOutputPath);
        return (report, request.EquipmentOutputPath);
    }

    private static string EquipmentSection(EquipmentReport? equipment, string? path)
    {
        if (equipment is null)
        {
            return "";
        }

        var text = new System.Text.StringBuilder();
        text.AppendLine();
        text.AppendLine("EQUIPMENT");
        text.AppendLine(equipment.Describe());

        if (path is not null)
        {
            text.AppendLine($"Written to: {path} — import this as well as the roster.");
        }

        foreach (var change in equipment.Changed)
        {
            text.AppendLine($"  - {change.PlayerName}: {change.Before} -> {change.After}");
        }

        foreach (var name in equipment.Unresolved)
        {
            text.AppendLine($"  - {name}: no helmet found to change; left as it was.");
        }

        return text.ToString();
    }

    private static string? TryFindDataFile(string? dataDirectory, string fileName)
    {
        try
        {
            return FindDataFile(dataDirectory, fileName);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static void CreateParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is { Length: > 0 })
        {
            Directory.CreateDirectory(directory);
        }
    }
}
