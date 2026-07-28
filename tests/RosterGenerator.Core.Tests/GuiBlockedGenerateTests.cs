using Avalonia.Controls;
using Avalonia.VisualTree;

using RosterGenerator.Gui;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// A greyed-out Generate button with nothing on screen explaining it.
///
/// <para>Reported from a real build: the status line read "Ready — 75
/// players, nothing to fix" and Generate would not light up. The status was
/// telling the truth about the roster and nothing at all about the dynasty —
/// <c>CheckAsync</c> reads only the roster file, and its message overwrote
/// whatever the dynasty had failed with. What was left on screen was a
/// reassuring sentence, a dead button, and no way to find out why.</para>
///
/// <para>What is pinned here is that the window always says what is missing,
/// and never claims a readiness it cannot know about.</para>
/// </summary>
public sealed class GuiBlockedGenerateTests
{
    private static void OnUiThread(Action<MainWindow> body) => HeadlessGui.Run(body);

    private static string BlockerText(MainWindow window) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "")
            .FirstOrDefault(t => t.StartsWith("Cannot generate", StringComparison.Ordinal)) ?? "";

    [Fact]
    public void AnEmptyWindowSaysWhichStepIsMissing()
    {
        OnUiThread(window =>
        {
            // Not merely disabled — disabled *and explained*. The first thing
            // a new user meets should not be a dead button.
            var blocker = BlockerText(window);
            Assert.NotEqual("", blocker);
            Assert.Contains("dynasty", blocker, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void WithADynastyButNoRosterItAsksForTheRoster()
    {
        OnUiThread(window =>
        {
            SetDynasty(window, TestsPath("DonorDynasty"));

            var blocker = BlockerText(window);
            Assert.Contains("roster", blocker, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void AFailedDynastyIsStillExplainedAfterARosterIsChosen()
    {
        OnUiThread(window =>
        {
            // A path that is not there at all, so loading it plainly fails.
            // (A stray CSV would not do: DynastyExport accepts any single file
            // as the Player table and only discovers the mistake later.)
            SetDynasty(window, TestsPath("no-such-dynasty"));
            var afterDynasty = BlockerText(window);
            Assert.NotEqual("", afterDynasty);

            // Choosing a roster runs the roster check, whose message used to
            // overwrite the only thing saying what had gone wrong.
            SetText(window, "_rosterBox", TestsPath("2023_FSU_Input.csv"));

            Assert.Equal(afterDynasty, BlockerText(window));
            Assert.False(Generate(window).IsEnabled);
        });
    }

    [Fact]
    public void GenerateLightsUpWhenBothArePresent()
    {
        OnUiThread(window =>
        {
            SetDynasty(window, TestsPath("DonorDynasty"));
            SetText(window, "_rosterBox", TestsPath("2023_FSU_Input.csv"));

            Assert.True(Generate(window).IsEnabled);
            Assert.Equal("", BlockerText(window));
        });
    }

    // ---- Driving the window the way its own handlers do -------------------

    private static string TestsPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Tests", name);

    private static Button Generate(MainWindow window) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => (b.Content as string) == "Generate");

    private static void SetDynasty(MainWindow window, string path)
    {
        SetText(window, "_dynastyBox", path);

        // Opening a dynasty is asynchronous now, and its continuation runs on
        // this thread — so the dispatcher has to keep being pumped while we
        // wait, or the continuation never runs and the wait never ends.
        var loading = (Task)typeof(MainWindow)
            .GetMethod("LoadDynastyAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(window, null)!;

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!loading.IsCompleted && DateTime.UtcNow < deadline)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Assert.True(loading.IsCompleted, "the dynasty did not finish loading");
        loading.GetAwaiter().GetResult();
    }

    private static void SetText(MainWindow window, string field, string text)
    {
        var box = (TextBox)typeof(MainWindow)
            .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(window)!;
        box.Text = text;

        // The headless platform does not raise TextChanged for a programmatic
        // assignment the way a real file picker does, so the handler is called
        // directly rather than relied upon.
        Invoke(window, "UpdateButtons");
    }

    private static void Invoke(MainWindow window, string method) =>
        typeof(MainWindow)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(window, null);
}
