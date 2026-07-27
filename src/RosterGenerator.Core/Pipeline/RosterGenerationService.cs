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
}

/// <summary>What a generation run produced.</summary>
/// <param name="Conversion">The per-player conversion report.</param>
/// <param name="Export">Validation report plus the per-row changed columns.</param>
/// <param name="OutputPath">Where the player table was written.</param>
/// <param name="ReportPath">Where the report was written.</param>
/// <param name="CsvWarnings">Roster CSV values that could not be used as written.</param>
/// <param name="CsvCorrections">Roster CSV values that were cleaned up and used.</param>
/// <param name="Equipment">What the era did to the team's head gear, if anything.</param>
/// <param name="EquipmentOutputPath">Where the equipment table went, when one was written.</param>
/// <param name="PackageOutputPath">Where the whole dynasty was written back, when asked for.</param>
/// <param name="PackagedTables">Tables substituted inside that package.</param>
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
    IReadOnlyList<string>? PackagedTables = null)
{
    /// <summary>Players written to the save.</summary>
    public int Converted => Conversion.Converted.Count();

    /// <summary>Players that could not be placed.</summary>
    public int Skipped => Conversion.Skipped.Count();

    /// <summary>Slots filled as end-of-roster depth.</summary>
    public int Filled => Conversion.FilledSlots.Count;
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
    /// <summary>Opens a dynasty export, for listing teams before generating.</summary>
    /// <summary>
    /// Opens a dynasty export for listing teams before generating. Accepts the
    /// folder of exported CSVs, the Player CSV alone, or a <c>.zip</c> of the
    /// folder — which is what a user has after moving an export between
    /// machines.
    /// </summary>
    public static DynastyExport OpenDynasty(string path) => DynastyPackage.Open(path).Export;

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
        // The dynasty may be a folder or a .zip of one; the package owns the
        // difference, and owns cleaning up after an archive it expanded.
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
        var conversion = new HistoricalTeamConverter(
                teamMappings, positionMappings, ratingEngine, archetypeSelector, filler, depth,
                export.BuildPreviousSchoolMappings(teamAliases), request.ReplaceRealPersonFaces)
            .Convert(session, roster.Roster);

        CreateParentDirectory(request.OutputPath);
        CreateParentDirectory(request.ReportPath);

        var result = new RosterExporter().Export(
            new RosterValidationContext(donor, session, overallFormulas: formulas), request.OutputPath);

        // Equipment is a second table, so it is applied after the player table
        // has validated and been written: a run that refuses to produce a
        // roster must not leave a stray equipment file beside it.
        var (equipment, equipmentPath) = ApplyEquipment(request, export, donor, conversion, data);

        File.WriteAllText(request.ReportPath,
            Path.GetExtension(request.ReportPath).Equals(".md", StringComparison.OrdinalIgnoreCase)
                ? conversion.ToMarkdown()
                : conversion.ToText() + EquipmentSection(equipment, equipmentPath));

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

        return new RosterGenerationResult(
            conversion, result, request.OutputPath, request.ReportPath,
            roster.Warnings, roster.Corrections, equipment, equipmentPath, packagePath, packaged);
    }

    /// <summary>
    /// Puts period-correct helmets on the converted team and writes the
    /// equipment table beside the roster. Returns nulls — and writes nothing —
    /// when equipment was not requested, the export carries no
    /// CharacterVisuals table, the season is unknown, or no era covers it.
    /// </summary>
    private static (EquipmentReport? Report, string? Path) ApplyEquipment(
        RosterGenerationRequest request,
        DynastyExport export,
        PlayerRoster donor,
        ConversionReport conversion,
        string? data)
    {
        if (!request.ApplyEquipment || export.CharacterVisualsPath is null || conversion.Source.Season <= 0)
        {
            return (null, null);
        }

        var erasPath = TryFindDataFile(data, "EquipmentEras.json");
        if (erasPath is null)
        {
            return (null, null);
        }

        var visuals = export.LoadCharacterVisuals();
        if (visuals is null)
        {
            return (null, null);
        }

        var report = new EquipmentApplier(EquipmentEraSet.Load(erasPath))
            .Apply(donor, visuals, conversion.TeamId, conversion.Source.Season);

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
