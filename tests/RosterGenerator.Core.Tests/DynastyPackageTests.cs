using System.IO.Compression;
using RosterGenerator.Core.Dynasty;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A dynasty goes in as one file and comes back as one file.
///
/// Users move exports between machines, and what arrives is a <c>.zip</c>, not
/// the folder that was inside it. Making them unpack it first was a step that
/// only existed because the tool could not read an archive. Handing back loose
/// CSVs to place by hand was the same problem in the other direction.
///
/// The property that matters is not that the archive round-trips — it is that
/// <b>everything the tool did not generate comes back byte for byte.</b> A
/// dynasty export is 2,273 files and this tool understands two of them; the
/// other 2,271 must survive untouched, or the package is worse than the loose
/// CSVs it replaced.
/// </summary>
public sealed class DynastyPackageTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "cfb27-package-tests-" + Guid.NewGuid().ToString("N")[..12]);

    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    public DynastyPackageTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Zips the donor fixture the way a user would zip their export.</summary>
    private string ZipTheDonorDynasty(string name = "DYNASTY-TEST")
    {
        var staging = Path.Combine(_scratch, "staging");
        var inner = Path.Combine(staging, name, "CSV");
        Directory.CreateDirectory(inner);
        foreach (var file in Directory.EnumerateFiles(TestsPath("DonorDynasty")))
        {
            File.Copy(file, Path.Combine(inner, Path.GetFileName(file)));
        }

        var archive = Path.Combine(_scratch, name + ".zip");
        ZipFile.CreateFromDirectory(staging, archive);
        Directory.Delete(staging, recursive: true);
        return archive;
    }

    private static Dictionary<string, string> Entries(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        return archive.Entries
            .Where(e => e.Length > 0 || !e.FullName.EndsWith('/'))
            .ToDictionary(e => e.FullName, e =>
            {
                using var stream = e.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(memory.ToArray()));
            }, StringComparer.Ordinal);
    }

    [Fact]
    public void AZippedExportOpensExactlyLikeTheFolderInsideIt()
    {
        var archive = ZipTheDonorDynasty();

        using var fromArchive = DynastyPackage.Open(archive);
        using var fromFolder = DynastyPackage.Open(TestsPath("DonorDynasty"));

        Assert.True(fromArchive.IsArchive);
        Assert.False(fromFolder.IsArchive);
        Assert.Equal("DYNASTY-TEST", fromArchive.Name);

        // Same discovery result, so nothing downstream can tell the difference.
        Assert.Equal(
            Path.GetFileName(fromFolder.Export.PlayerTablePath),
            Path.GetFileName(fromArchive.Export.PlayerTablePath));
        Assert.Equal(fromFolder.Export.Teams.Count, fromArchive.Export.Teams.Count);
        Assert.Equal(
            File.ReadAllBytes(fromFolder.Export.PlayerTablePath),
            File.ReadAllBytes(fromArchive.Export.PlayerTablePath));
    }

    [Fact]
    public void EverythingTheToolDidNotGenerateComesBackByteForByte()
    {
        var archive = ZipTheDonorDynasty();
        var before = Entries(archive);

        var replacement = Path.Combine(_scratch, "NewPlayer.csv");
        File.WriteAllText(replacement, "this stands in for a generated player table\n");

        var destination = Path.Combine(_scratch, "returned.zip");
        using (var package = DynastyPackage.Open(archive))
        {
            var substituted = package.WriteArchive(
                destination,
                new Dictionary<string, string> { [package.Export.PlayerTablePath] = replacement });
            Assert.Single(substituted);
        }

        var after = Entries(destination);
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));

        var changed = before.Keys.Where(k => before[k] != after[k]).ToList();
        Assert.Single(changed);
        Assert.EndsWith("Player.csv", changed[0], StringComparison.Ordinal);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.MD5.HashData(File.ReadAllBytes(replacement))),
            after[changed[0]]);

        // And the archive that went in is untouched: a user who dislikes the
        // result still has their dynasty.
        Assert.Equal(before, Entries(archive));
    }

    [Fact]
    public void APackageCanBeFedStraightBackIn()
    {
        var archive = ZipTheDonorDynasty();
        var destination = Path.Combine(_scratch, "returned.zip");

        string playerTable;
        using (var package = DynastyPackage.Open(archive))
        {
            playerTable = package.Export.PlayerTablePath;
            package.WriteArchive(destination, new Dictionary<string, string>());
        }

        using var reopened = DynastyPackage.Open(destination);
        Assert.Equal("DYNASTY-TEST", reopened.Name);
        Assert.Equal(Path.GetFileName(playerTable), Path.GetFileName(reopened.Export.PlayerTablePath));
        Assert.NotEmpty(reopened.Export.Teams);
    }

    [Fact]
    public void AFolderIsWrappedSoItDoesNotExplodeOverSomebodysDesktop()
    {
        var destination = Path.Combine(_scratch, "from-folder.zip");
        using (var package = DynastyPackage.Open(TestsPath("DonorDynasty")))
        {
            package.WriteArchive(destination, new Dictionary<string, string>());
        }

        using var archive = ZipFile.OpenRead(destination);
        var roots = archive.Entries
            .Select(e => e.FullName.Split('/')[0])
            .Distinct()
            .ToList();
        Assert.Single(roots);
        Assert.Equal("DonorDynasty", roots[0]);
    }

    [Fact]
    public void ReplacingATableThatIsNotInThisDynastyIsRefused()
    {
        var stray = Path.Combine(_scratch, "stray.csv");
        File.WriteAllText(stray, "not part of any dynasty\n");

        using var package = DynastyPackage.Open(TestsPath("DonorDynasty"));
        var destination = Path.Combine(_scratch, "never-written.zip");

        // Silently producing an unchanged archive would look like success.
        Assert.Throws<ArgumentException>(() => package.WriteArchive(
            destination, new Dictionary<string, string> { [stray] = stray }));
    }

    [Fact]
    public void ExpandingAnArchiveLeavesNothingBehind()
    {
        var archive = ZipTheDonorDynasty();
        string scratch;
        using (var package = DynastyPackage.Open(archive))
        {
            scratch = package.RootDirectory;
            Assert.True(Directory.Exists(scratch));
        }

        Assert.False(Directory.Exists(scratch), $"the scratch folder '{scratch}' outlived the package");
    }
}
