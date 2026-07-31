using RosterGenerator.Core.Dynasty;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A path that means one thing to the app must mean the same thing to the
/// sidecar that reads and writes saves.
///
/// <para>Reported from a shipped build: generating a full FBS roster with
/// "write a new save" on died on a Node stack trace —</para>
///
/// <code>
/// Error: ENOENT: no such file or directory, open
///   '...\CFB27-Roster-Generator-0.7.2-alpha-win-x64\tools\native-save\Output\Generated_Roster.csv'
/// </code>
///
/// <para>The roster had been written, to <c>Output\Generated_Roster.csv</c>
/// beside the executable. The sidecar runs with its working directory set to
/// <c>tools\native-save</c>, where its scripts and <c>node_modules</c> live, so
/// the same relative path pointed somewhere that has never existed. Nothing to
/// do with the size of the roster, and nothing the user could work around.</para>
///
/// <para>Every existing test missed it for one reason: they all write to a
/// temporary directory, which is to say they all pass absolute paths. These
/// pass relative ones on purpose.</para>
/// </summary>
public sealed class NativeSavePathTests
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>
    /// A folder under the working directory, so a genuinely relative path can
    /// be handed in. The working directory itself is never changed — it is
    /// process-wide, and the rest of the suite runs alongside this one.
    /// </summary>
    private sealed class RelativeDirectory : IDisposable
    {
        public string Relative { get; } =
            System.IO.Path.Combine("Output", "path-test-" + Guid.NewGuid().ToString("N")[..8]);

        public RelativeDirectory() => Directory.CreateDirectory(Relative);

        public string File(string name) => System.IO.Path.Combine(Relative, name);

        public void Dispose() => Directory.Delete(Relative, recursive: true);
    }

    /// <summary>A file with the save header and nothing behind it.</summary>
    private static void WriteFakeSave(string path) =>
        File.WriteAllBytes(path, "FBCHUNKS"u8.ToArray().Concat(new byte[64]).ToArray());

    // ---- The arguments the sidecar is handed --------------------------------

    [Fact]
    public void TheGeneratedRosterIsHandedOverAsAnAbsolutePath()
    {
        // The shipped default, exactly as the app passes it.
        var roster = Path.Combine("Output", "Generated_Roster.csv");

        var arguments = NativeSave.ApplyArguments(
            "DYNASTY-SOURCE", "DYNASTY-OUT", new[] { roster }, seasonYear: null);

        Assert.All(arguments, argument =>
            Assert.True(Path.IsPathRooted(argument),
                $"'{argument}' is relative, so the sidecar resolves it against its own folder."));

        // Absolute against the caller's directory — not merely absolute.
        Assert.Equal(Path.GetFullPath(roster), arguments[2]);
        Assert.Equal(Path.GetFullPath("DYNASTY-SOURCE"), arguments[0]);
        Assert.Equal(Path.GetFullPath("DYNASTY-OUT"), arguments[1]);
    }

    [Fact]
    public void AnAbsolutePathIsLeftAsItIs()
    {
        using var temp = new TempDirectory();

        var arguments = NativeSave.ApplyArguments(
            temp.File("DYNASTY-SOURCE"), temp.File("DYNASTY-OUT"),
            new[] { temp.File("player.csv") }, seasonYear: null);

        Assert.Equal(temp.File("player.csv"), arguments[2]);
    }

    [Fact]
    public void TheYearStillGoesThroughAsAFlagRatherThanAPath()
    {
        var arguments = NativeSave.ApplyArguments(
            "source", "dest", new[] { "player.csv" }, seasonYear: 1985);

        Assert.Equal("--year", arguments[^2]);
        Assert.Equal("1985", arguments[^1]);
    }

    [Fact]
    public void ExtractingResolvesTheSaveAndTheOutputFolderToo()
    {
        var arguments = NativeSave.ExtractArguments("DYNASTY-BASE1", "scratch");

        Assert.Equal(Path.GetFullPath("DYNASTY-BASE1"), arguments[0]);
        Assert.Equal(Path.GetFullPath("scratch"), arguments[1]);
        Assert.Equal(NativeSave.DefaultTables, arguments[2]);
    }

    // ---- Through the real sidecar ------------------------------------------

    [Fact]
    public void ARelativeRosterPathReachesTheSidecarIntact()
    {
        if (!NativeSave.IsAvailable(out _))
        {
            return;
        }

        using var folder = new RelativeDirectory();
        var roster = folder.File("Generated_Roster.csv");
        File.WriteAllText(roster, "_tableName,_row,FirstName\nPlayer,0,Aa\n");
        File.WriteAllBytes(folder.File("DYNASTY-SOURCE"), new byte[72]);

        // The source is deliberately not a save, so the run stops at the header
        // check. What matters is that it got that far: before the fix it never
        // did, dying on the roster CSV first.
        var thrown = Assert.Throws<NativeSaveException>(() => NativeSave.Apply(
            folder.File("DYNASTY-SOURCE"), folder.File("DYNASTY-OUT"), new[] { roster }));

        Assert.DoesNotContain("ENOENT", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Generated_Roster.csv", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("not a CFB27 dynasty save", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARosterThatGenuinelyIsNotThereIsSaidPlainly()
    {
        if (!NativeSave.IsAvailable(out _))
        {
            return;
        }

        using var temp = new TempDirectory();
        WriteFakeSave(temp.File("DYNASTY-SOURCE"));

        var thrown = Assert.Throws<NativeSaveException>(() => NativeSave.Apply(
            temp.File("DYNASTY-SOURCE"), temp.File("DYNASTY-OUT"),
            new[] { temp.File("never-written.csv") }));

        // A stack trace out of readFileSync is not an error message. The
        // missing file is named, and so is the fact that the save survived.
        Assert.DoesNotContain("readFileSync", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("never-written.csv", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("save was not touched", thrown.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(temp.File("DYNASTY-OUT")));
    }

    [Fact]
    public void APointerAtNoFileAtAllSaysThatRatherThanTalkingAboutHeaders()
    {
        if (!NativeSave.IsAvailable(out _))
        {
            return;
        }

        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("player.csv"), "_tableName,_row\nPlayer,0\n");

        var thrown = Assert.Throws<NativeSaveException>(() => NativeSave.Apply(
            temp.File("DYNASTY-NOT-HERE"), temp.File("DYNASTY-OUT"),
            new[] { temp.File("player.csv") }));

        Assert.Contains("no dynasty save at", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("FBCHUNKS", thrown.Message, StringComparison.Ordinal);
    }
}
