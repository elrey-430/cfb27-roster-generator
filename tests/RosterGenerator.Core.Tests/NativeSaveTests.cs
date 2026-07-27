using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Pipeline;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Reading and writing a dynasty save directly.
///
/// <para>A save is 9.6 MB of somebody's dynasty, so one cannot be committed as
/// a fixture. What is pinned here is everything that does not need one: how a
/// save is recognised, what happens when the machine cannot read one, and that
/// asking for a save back without giving one is refused rather than half-done.
/// The end-to-end test runs only when <c>CFB27_TEST_SAVE</c> points at a real
/// save; without one it does nothing, so treat a green suite as saying nothing
/// about that path unless the variable was set.</para>
/// </summary>
public sealed class NativeSaveTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())).FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    // ---- Recognising a save ----------------------------------------------

    [Fact]
    public void ASaveIsRecognisedByItsHeaderNotItsName()
    {
        // A dynasty save has no extension, so the only thing to go on is what
        // is inside it.
        using var temp = new TempDirectory();
        var save = temp.File("DYNASTY-WHATEVER");
        File.WriteAllBytes(save, "FBCHUNKS"u8.ToArray().Concat(new byte[64]).ToArray());

        Assert.True(NativeSave.LooksLikeSave(save));
    }

    [Fact]
    public void AnExportIsNotMistakenForASave()
    {
        Assert.False(NativeSave.LooksLikeSave(TestsPath("DonorDynasty", "0152_Player.csv")));
        Assert.False(NativeSave.LooksLikeSave(TestsPath("DonorDynasty")));
        Assert.False(NativeSave.LooksLikeSave(Path.Combine(AppContext.BaseDirectory, "does-not-exist")));
    }

    [Fact]
    public void AShortFileIsNotASave()
    {
        // Reading the header must not throw on a file smaller than the header.
        using var temp = new TempDirectory();
        var stub = temp.File("tiny");
        File.WriteAllBytes(stub, "FBC"u8.ToArray());

        Assert.False(NativeSave.LooksLikeSave(stub));
    }

    // ---- Refusing clearly -------------------------------------------------

    [Fact]
    public void AMachineThatCannotReadSavesSaysWhatIsMissing()
    {
        // Whether this machine can or cannot is not the point; the point is
        // that the answer is never a bare false. A user told "no" without a
        // reason has nothing to act on.
        var available = NativeSave.IsAvailable(out var reason);
        if (available)
        {
            Assert.Equal("", reason);
        }
        else
        {
            Assert.NotEqual("", reason);
            Assert.True(
                reason.Contains("Node", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("npm install", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("native-save", StringComparison.OrdinalIgnoreCase),
                $"The reason should tell the user what to do; it said: {reason}");
        }
    }

    [Fact]
    public void AnExportCannotBeHandedBackAsASave()
    {
        // There is no save to write into, and quietly writing nothing would be
        // worse than refusing.
        using var package = DynastyPackage.Open(TestsPath("DonorDynasty"));
        Assert.False(package.IsNativeSave);
        Assert.Null(package.SourceSavePath);

        var error = Assert.Throws<InvalidOperationException>(
            () => package.WriteSave("out", new[] { "player.csv" }));
        Assert.Contains("--dynasty", error.Message);
    }

    [Fact]
    public void AskingForASaveFromAnExportFailsBeforeAnythingIsWritten()
    {
        using var temp = new TempDirectory();
        var request = new RosterGenerationRequest
        {
            DynastyPath = TestsPath("DonorDynasty"),
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
            OutputPath = temp.File("out.csv"),
            ReportPath = temp.File("report.txt"),
            SaveOutputPath = temp.File("DYNASTY-OUT"),
        };

        var error = Assert.Throws<NativeSaveException>(() => new RosterGenerationService().Run(request));
        Assert.Contains("save file", error.Message);
        Assert.False(File.Exists(temp.File("DYNASTY-OUT")));
    }

    [Fact]
    public void TheSidecarShipsWithTheApplication()
    {
        // The scripts are what make a save readable at all; a build that drops
        // them turns every save into an unexplained failure.
        var tools = NativeSave.FindToolsDirectory();
        Assert.NotNull(tools);
        foreach (var script in new[] { "extract.mjs", "apply.mjs", "save.mjs", "csv.mjs", "package.json" })
        {
            Assert.True(File.Exists(Path.Combine(tools!, script)), $"{script} is missing from {tools}");
        }
    }

    // ---- The whole way through, when a real save is available -------------

    /// <summary>
    /// Set <c>CFB27_TEST_SAVE</c> to a dynasty save to run the end-to-end
    /// check: read the save, generate a roster into it, and read the result
    /// back out.
    /// </summary>
    private static string? RealSave =>
        Environment.GetEnvironmentVariable("CFB27_TEST_SAVE") is { Length: > 0 } path && File.Exists(path)
            ? path
            : null;

    [Fact]
    public void ADynastyGoesInAsASaveAndComesBackAsASave()
    {
        // No save to hand it, or no Node to read one with: there is nothing to
        // assert, and inventing a 9.6 MB fixture would assert the wrong thing.
        if (RealSave is null || !NativeSave.IsAvailable(out _))
        {
            return;
        }

        using var temp = new TempDirectory();
        var destination = temp.File("DYNASTY-TESTOUT");
        var before = new FileInfo(RealSave!).Length;

        var result = new RosterGenerationService().Run(new RosterGenerationRequest
        {
            DynastyPath = RealSave!,
            RosterPath = TestsPath("2023_FSU_Input.csv"),
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
            OutputPath = temp.File("player.csv"),
            ReportPath = temp.File("report.txt"),
            EquipmentOutputPath = temp.File("visuals.csv"),
            SaveOutputPath = destination,
        });

        var save = result.SaveOutput;
        Assert.NotNull(save);
        Assert.True(File.Exists(destination));
        Assert.True(save!.CellsChanged > 0, "A generated roster must write something.");

        // The pre-allocated slots the game keeps are not ours to write into.
        Assert.True(save.EmptyRecordsSkipped > 0);

        // And the dynasty that came in is still exactly what came in.
        Assert.Equal(before, new FileInfo(RealSave!).Length);
    }
}
