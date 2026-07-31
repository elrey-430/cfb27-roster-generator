using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace RosterGenerator.Core.Dynasty;

/// <summary>Something went wrong reading or writing a dynasty save directly.</summary>
public sealed class NativeSaveException : Exception
{
    /// <summary>Creates the exception.</summary>
    public NativeSaveException(string message) : base(message) { }
}

/// <summary>What writing a dynasty back out actually changed.</summary>
/// <param name="Destination">The new save.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="CellsChanged">Fields written, across every table.</param>
/// <param name="EmptyRecordsSkipped">Pre-allocated empty slots deliberately left alone.</param>
/// <param name="Tables">Table name → fields written in it.</param>
/// <param name="SeasonYearFrom">
/// The year the save displayed before, when the season year was set; null when
/// it was left alone.
/// </param>
/// <param name="SeasonYearTo">The year it displays now, or null.</param>
public sealed record NativeSaveWriteReport(
    string Destination,
    long Bytes,
    int CellsChanged,
    int EmptyRecordsSkipped,
    IReadOnlyDictionary<string, int> Tables,
    int? SeasonYearFrom = null,
    int? SeasonYearTo = null)
{
    /// <summary>True when this run changed the year the game will show.</summary>
    public bool SeasonYearChanged => SeasonYearTo is not null;
}

/// <summary>
/// Reads and writes a CFB27 dynasty save directly, instead of requiring the
/// user to export it to CSV first and import the result back with a separate
/// editor.
///
/// <para><b>Why this is a sidecar rather than C#.</b> A save is EA's franchise
/// database — zstd-compressed with a trained dictionary, bit-packed records,
/// a 3,498-entry schema that a game patch can move. That format is already
/// solved, correctly and under an MIT licence, by <c>madden-franchise</c>,
/// which ships the College Football 27 schema and dictionaries. Reimplementing
/// it in C# would mean owning a bit-packer and a schema table for the rest of
/// the project's life in exchange for nothing a user can see.</para>
///
/// <para>So the format work happens in Node, and this class is the boundary:
/// it turns a save into the CSVs the pipeline has read since Milestone 3, and
/// turns generated CSVs back into a save. Everything between those two points
/// is unchanged and does not know a save was involved. The extracted CSVs are
/// byte-identical to the community export tool's own output, which is what
/// makes that true rather than merely hoped for.</para>
///
/// <para>The source save is never modified. Writing always produces a new
/// file, and the sidecar refuses a destination equal to the source.</para>
/// </summary>
public static class NativeSave
{
    private static readonly byte[] Magic = "FBCHUNKS"u8.ToArray();

    /// <summary>Tables the generator needs out of a save.</summary>
    public const string DefaultTables = "Player,Team,CharacterVisuals";

    /// <summary>
    /// True when the file begins with the dynasty save header. Checked by
    /// content rather than by name: a save has no extension, so there is
    /// nothing else to go on.
    /// </summary>
    public static bool LooksLikeSave(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);
            var head = new byte[Magic.Length];
            return stream.Read(head, 0, head.Length) == head.Length && head.SequenceEqual(Magic);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the tables the generator needs into <paramref name="outputDirectory"/>,
    /// which then reads exactly like a folder from the export tool.
    /// </summary>
    public static void Extract(string savePath, string outputDirectory, string? tables = null)
    {
        Directory.CreateDirectory(outputDirectory);
        Run("extract.mjs", ExtractArguments(savePath, outputDirectory, tables));
    }

    /// <summary>
    /// The arguments handed to <c>extract.mjs</c>, with every path made absolute.
    /// See <see cref="ApplyArguments"/> for why that matters.
    /// </summary>
    internal static IReadOnlyList<string> ExtractArguments(
        string savePath, string outputDirectory, string? tables = null) =>
        new[] { Absolute(savePath), Absolute(outputDirectory), tables ?? DefaultTables };

    /// <summary>
    /// Resolves a path against the caller's working directory, so that it still
    /// means the same file once the sidecar has changed to its own.
    ///
    /// <para>This is the whole of the bug this method exists for. The sidecar
    /// runs with its working directory set to <c>tools/native-save</c>, because
    /// that is where its scripts and its <c>node_modules</c> live. The app's
    /// own paths are relative to wherever the app was started — the generated
    /// roster defaults to <c>Output/Generated_Roster.csv</c> — so handing one
    /// straight through asked Node for
    /// <c>tools/native-save/Output/Generated_Roster.csv</c>, which has never
    /// existed, and writing a save died on a raw <c>ENOENT</c> stack trace.
    /// Every test passed because every test used a temporary directory, which
    /// is to say an absolute path.</para>
    /// </summary>
    private static string Absolute(string path) => Path.GetFullPath(path);

    /// <summary>
    /// Writes generated tables into a copy of <paramref name="savePath"/>,
    /// producing a new save at <paramref name="destinationPath"/>.
    /// </summary>
    /// <param name="savePath">The user's original save. Never modified.</param>
    /// <param name="destinationPath">The save to create.</param>
    /// <param name="tableCsvPaths">Generated table CSVs to write in.</param>
    /// <param name="seasonYear">
    /// The season the game should display, or null to leave the save's own year
    /// alone. See <see cref="FirstSeason"/> for the accepted range.
    /// </param>
    public static NativeSaveWriteReport Apply(
        string savePath,
        string destinationPath,
        IReadOnlyList<string> tableCsvPaths,
        int? seasonYear = null)
    {
        if (tableCsvPaths.Count == 0)
        {
            throw new ArgumentException("No tables were supplied to write into the save.", nameof(tableCsvPaths));
        }

        if (seasonYear is int year && !IsSupportedSeason(year))
        {
            throw new ArgumentOutOfRangeException(
                nameof(seasonYear), year,
                $"The season year must be between {FirstSeason} and {LastSeason}.");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (directory is { Length: > 0 })
        {
            Directory.CreateDirectory(directory);
        }

        return ParseWriteReport(
            Run("apply.mjs", ApplyArguments(savePath, destinationPath, tableCsvPaths, seasonYear)),
            destinationPath);
    }

    /// <summary>
    /// The arguments handed to <c>apply.mjs</c>, with every path made absolute
    /// first — see <see cref="Absolute"/> for what that is guarding against.
    /// </summary>
    internal static IReadOnlyList<string> ApplyArguments(
        string savePath,
        string destinationPath,
        IReadOnlyList<string> tableCsvPaths,
        int? seasonYear)
    {
        var arguments = new List<string> { Absolute(savePath), Absolute(destinationPath) };
        arguments.AddRange(tableCsvPaths.Select(Absolute));
        if (seasonYear is int display)
        {
            arguments.Add("--year");
            arguments.Add(display.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return arguments;
    }

    /// <summary>
    /// The earliest season the year can be set to: the first college football
    /// game ever played. Anything before it is a typo rather than a season
    /// somebody is recreating.
    /// </summary>
    public const int FirstSeason = 1869;

    /// <summary>
    /// The latest season the save format can hold. The field is a 12-bit
    /// integer, and the library does not enforce its own schema — setting 5000
    /// is accepted in silence and writes a number the game cannot read — so
    /// the bound is checked here and again in the sidecar.
    /// </summary>
    public const int LastSeason = 4095;

    /// <summary>True when the year can actually be written into a save.</summary>
    public static bool IsSupportedSeason(int year) => year is >= FirstSeason and <= LastSeason;

    private static NativeSaveWriteReport ParseWriteReport(string json, string destination)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var byTable = new Dictionary<string, int>(StringComparer.Ordinal);
            var changed = 0;
            var skipped = 0;
            if (root.TryGetProperty("tables", out var tables))
            {
                foreach (var table in tables.EnumerateArray())
                {
                    var cells = table.GetProperty("cellsChanged").GetInt32();
                    changed += cells;
                    skipped += table.GetProperty("emptyRecordsSkipped").GetInt32();
                    byTable[table.GetProperty("table").GetString() ?? "?"] = cells;
                }
            }

            var bytes = root.TryGetProperty("destinationBytes", out var size) ? size.GetInt64() : 0;
            int? yearFrom = null, yearTo = null;
            if (root.TryGetProperty("seasonYear", out var season))
            {
                yearFrom = season.GetProperty("from").GetInt32();
                yearTo = season.GetProperty("to").GetInt32();
            }

            return new NativeSaveWriteReport(
                destination, bytes, changed, skipped, byTable, yearFrom, yearTo);
        }
        catch (JsonException ex)
        {
            throw new NativeSaveException($"The save writer returned output that could not be read: {ex.Message}");
        }
    }

    /// <summary>
    /// Where the sidecar scripts live: beside the executable in a shipped
    /// build, or in the repository when running from source.
    /// </summary>
    public static string? FindToolsDirectory()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            for (var i = 0; i < 8 && directory is not null; i++, directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "tools", "native-save");
                if (File.Exists(Path.Combine(candidate, "extract.mjs")))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The JavaScript runtime that reads saves: the copy shipped beside the
    /// application if there is one, otherwise whatever <c>node</c> is on PATH.
    ///
    /// <para>A bundled copy is strongly preferred, and the release ships one.
    /// Somebody who downloaded a roster tool should not have to install a
    /// programming language to open their own dynasty, and a private copy also
    /// cannot be broken by whatever else on the machine wants a different
    /// Node version.</para>
    /// </summary>
    public static string ResolveRuntime()
    {
        if (FindToolsDirectory() is { } tools)
        {
            var name = OperatingSystem.IsWindows() ? "node.exe" : "node";
            var bundled = Path.Combine(tools, "runtime", name);
            if (File.Exists(bundled))
            {
                return bundled;
            }
        }

        return "node";
    }

    /// <summary>True when the runtime in use is the one shipped with the app.</summary>
    public static bool UsesBundledRuntime => ResolveRuntime() != "node";

    /// <summary>
    /// Whether reading and writing saves directly is available, and if not,
    /// what the user would have to do about it.
    /// </summary>
    public static bool IsAvailable(out string reason)
    {
        var tools = FindToolsDirectory();
        if (tools is null)
        {
            reason = "The 'tools/native-save' folder is missing from this installation.";
            return false;
        }

        if (!Directory.Exists(Path.Combine(tools, "node_modules")))
        {
            reason =
                $"The save reader's dependencies are not installed. Run 'npm install' in '{tools}'.";
            return false;
        }

        if (!NodeWorks(out var nodeProblem))
        {
            reason = nodeProblem;
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// One line describing how saves are read, for the app to show. Says
    /// nothing about installing anything when nothing needs installing.
    /// </summary>
    public static string DescribeRuntime() =>
        IsAvailable(out var reason)
            ? UsesBundledRuntime
                ? "Dynasty saves can be read and written directly (using the included runtime)."
                : "Dynasty saves can be read and written directly (using the Node.js on this machine)."
            : reason;

    private static bool NodeWorks(out string reason)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(ResolveRuntime(), "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                reason = NodeMissing;
                return false;
            }

            var version = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                reason = NodeMissing;
                return false;
            }

            // The library needs 22.19; an older Node fails deep inside it with
            // a syntax error, which is a miserable thing to hand a user.
            if (version.StartsWith('v') && int.TryParse(version[1..].Split('.')[0], out var major) && major < 22)
            {
                reason = $"Node {version} is too old to read a dynasty save; version 22.19 or newer is needed.";
                return false;
            }

            reason = "";
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            reason = NodeMissing;
            return false;
        }
    }

    private const string NodeMissing =
        "The runtime that reads dynasty saves could not be started. The release ships its own copy in " +
        "'tools/native-save/runtime' — if that folder is missing, unzip the download again, keeping it " +
        "whole. Failing that, install Node.js 22.19 or newer from https://nodejs.org, or export your " +
        "dynasty to CSVs and point --dynasty at that folder instead.";

    private static string Run(string script, IReadOnlyList<string> arguments)
    {
        if (!IsAvailable(out var reason))
        {
            throw new NativeSaveException(reason);
        }

        var tools = FindToolsDirectory()!;
        var info = new ProcessStartInfo(ResolveRuntime())
        {
            WorkingDirectory = tools,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("--max-old-space-size=6144");
        info.ArgumentList.Add(Path.Combine(tools, script));
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)
            ?? throw new NativeSaveException(NodeMissing);

        // Read both streams concurrently: a save produces enough output on
        // stderr to fill the pipe and deadlock a sequential read.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var output = stdout.GetAwaiter().GetResult();
        var errors = stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            var detail = new StringBuilder(errors.Trim());
            if (detail.Length == 0)
            {
                detail.Append($"'{script}' exited with code {process.ExitCode}.");
            }

            throw new NativeSaveException(detail.ToString());
        }

        return output;
    }
}
