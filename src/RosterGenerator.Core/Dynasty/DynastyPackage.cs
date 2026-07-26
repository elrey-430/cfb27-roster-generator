using System.IO.Compression;

namespace RosterGenerator.Core.Dynasty;

/// <summary>
/// A dynasty export as the user actually has it: either the folder the
/// community export tool wrote, or the <c>.zip</c> they made of it to move it
/// around. Both are opened the same way, and either can be handed back as a
/// single archive with the generated tables written into it.
///
/// The archive is never modified in place. Reading extracts to a scratch
/// folder that this object owns and deletes on <see cref="Dispose"/>; writing
/// streams a NEW archive out of the source tree, substituting the tables that
/// were regenerated and copying every other byte of every other file through
/// untouched. A user who dislikes the result still has their original.
/// </summary>
public sealed class DynastyPackage : IDisposable
{
    private readonly string? _scratch;
    private readonly string? _entryPrefix;

    private DynastyPackage(
        DynastyExport export, string root, string name, bool fromArchive, string? scratch, string? entryPrefix)
    {
        Export = export;
        RootDirectory = root;
        Name = name;
        IsArchive = fromArchive;
        _scratch = scratch;
        _entryPrefix = entryPrefix;
    }

    /// <summary>The discovered tables inside this package.</summary>
    public DynastyExport Export { get; }

    /// <summary>
    /// The folder the tables were read from — the user's own folder, or the
    /// scratch folder an archive was expanded into.
    /// </summary>
    public string RootDirectory { get; }

    /// <summary>
    /// The dynasty's own name (<c>DYNASTY-BASE1</c>), taken from the archive
    /// or the folder. Used to name what is written back, so a user can tell
    /// which save a package came from.
    /// </summary>
    public string Name { get; }

    /// <summary>True when the user supplied a <c>.zip</c> rather than a folder.</summary>
    public bool IsArchive { get; }

    /// <summary>True when the path looks like a zip archive rather than a folder.</summary>
    public static bool LooksLikeArchive(string path) =>
        File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens a dynasty export from a folder, a lone Player CSV, or a
    /// <c>.zip</c> of any of those.
    /// </summary>
    /// <exception cref="FileNotFoundException">The path does not exist.</exception>
    public static DynastyPackage Open(string path)
    {
        if (!LooksLikeArchive(path))
        {
            var directory = File.Exists(path)
                ? Path.GetDirectoryName(Path.GetFullPath(path)) ?? "."
                : path;
            var folderName = DescribeFolder(directory);

            // A folder has no wrapper of its own, so one is added on the way
            // out: an archive that explodes loose files over someone's desktop
            // is a bad way to hand back a dynasty.
            return new DynastyPackage(
                DynastyExport.Open(path), directory, folderName,
                fromArchive: false, scratch: null, entryPrefix: folderName);
        }

        var scratch = Path.Combine(
            Path.GetTempPath(), "cfb27-dynasty-" + Guid.NewGuid().ToString("N")[..12]);
        try
        {
            Directory.CreateDirectory(scratch);
            ZipFile.ExtractToDirectory(path, scratch);

            // The archive's own layout is preserved exactly. Almost every
            // export is wrapped in one folder named for the dynasty, and that
            // wrapper is already in the extracted paths — prefixing another
            // would nest the result inside itself.
            var top = Directory.GetDirectories(scratch);
            var name = top.Length == 1 && Directory.GetFiles(scratch).Length == 0
                ? new DirectoryInfo(top[0]).Name
                : Path.GetFileNameWithoutExtension(path);
            return new DynastyPackage(
                DynastyExport.Open(scratch), scratch, name,
                fromArchive: true, scratch, entryPrefix: null);
        }
        catch
        {
            TryDelete(scratch);
            throw;
        }
    }

    /// <summary>
    /// Writes the whole dynasty back out as one archive, with the supplied
    /// files substituted for the tables they replace.
    ///
    /// Every other file in the export is copied through byte for byte, so what
    /// comes out is the user's own dynasty with the generated tables in it,
    /// laid out exactly as the export tool laid it out. Entry order and
    /// relative paths are preserved, and the export's <c>_manifest.json</c>
    /// stays valid because a regenerated table has the same record and field
    /// count as the one it replaces.
    /// </summary>
    /// <param name="destinationArchive">Path of the .zip to create.</param>
    /// <param name="replacements">
    /// Table path inside this package → path of the file whose content should
    /// take its place. A key that is not part of this package is an error, so
    /// a typo cannot silently produce an unchanged archive.
    /// </param>
    /// <returns>The table paths that were substituted, relative to the root.</returns>
    public IReadOnlyList<string> WriteArchive(
        string destinationArchive, IReadOnlyDictionary<string, string> replacements)
    {
        var byFullPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (table, replacement) in replacements)
        {
            var full = Path.GetFullPath(table);
            if (!full.StartsWith(Path.GetFullPath(RootDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"'{table}' is not part of the dynasty at '{RootDirectory}', so it cannot be replaced in it.",
                    nameof(replacements));
            }

            byFullPath[full] = replacement;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationArchive));
        if (directory is { Length: > 0 })
        {
            Directory.CreateDirectory(directory);
        }

        var substituted = new List<string>();
        var root = Path.GetFullPath(RootDirectory);

        // Write to a temporary file and move it into place, so an interrupted
        // run cannot leave a half-written archive looking like a finished one.
        var staging = destinationArchive + ".partial";
        TryDeleteFile(staging);
        try
        {
            using (var stream = File.Create(staging))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                             .OrderBy(f => f, StringComparer.Ordinal))
                {
                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    var replaced = byFullPath.TryGetValue(Path.GetFullPath(file), out var replacement);
                    var source = replaced ? replacement! : file;
                    if (replaced)
                    {
                        substituted.Add(relative);
                    }

                    var entryName = _entryPrefix is { Length: > 0 } prefix
                        ? $"{prefix}/{relative}"
                        : relative;
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var input = File.OpenRead(source);
                    input.CopyTo(entryStream);
                }
            }

            TryDeleteFile(destinationArchive);
            File.Move(staging, destinationArchive);
        }
        finally
        {
            TryDeleteFile(staging);
        }

        var missed = byFullPath.Keys
            .Where(k => !substituted.Contains(Path.GetRelativePath(root, k).Replace('\\', '/')))
            .ToList();
        if (missed.Count > 0)
        {
            throw new InvalidOperationException(
                "These tables were meant to be replaced but were not found in the dynasty: " +
                string.Join(", ", missed));
        }

        return substituted;
    }

    /// <summary>Deletes the scratch folder an archive was expanded into.</summary>
    public void Dispose()
    {
        if (_scratch is not null)
        {
            TryDelete(_scratch);
        }
    }

    private static string DescribeFolder(string directory)
    {
        var name = new DirectoryInfo(Path.GetFullPath(directory)).Name;
        // The export tool writes <DYNASTY-NAME>/CSV/, and the dynasty's name
        // is the more useful of the two to put on what we hand back.
        if (name.Equals("CSV", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(Path.GetFullPath(directory));
            if (parent is not null)
            {
                return parent.Name;
            }
        }

        return name.Length > 0 ? name : "Dynasty";
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A scratch folder that outlives the process is untidy, not wrong.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
