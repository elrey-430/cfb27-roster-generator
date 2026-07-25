using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

using RosterGenerator.Gui;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The window is built in C# rather than markup, so a mistake in it is a
/// runtime failure on the user's machine with no compiler to catch it first.
/// These run the real window under Avalonia's headless platform, which is as
/// close to "it opens" as a test can get without a display.
/// </summary>
public sealed class GuiSmokeTests
{
    private static AppBuilder Headless() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    [Fact]
    public void TheWindowBuildsAndShows()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Headless().SetupWithoutStarting();
                var window = new MainWindow();
                window.Show();

                Assert.Equal("Historical CFB27 Roster Generator", window.Title);
                Assert.NotNull(window.Content);

                // Generation must be impossible until both files are chosen —
                // the button being live with nothing selected is the most
                // obvious way a first-time user gets an exception dialog.
                var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
                var generate = buttons.SingleOrDefault(b => (b.Content as string) == "Generate");
                Assert.NotNull(generate);
                Assert.False(generate!.IsEnabled);

                // The single most confusable thing about this tool is what
                // step 1 wants. It is a folder of CSVs the community export
                // tool produced, not a save file, and the window has to say
                // so — a user who points it at a save gets an error with no
                // idea what to do instead.
                var lines = window.GetVisualDescendants().OfType<TextBlock>()
                    .Select(t => t.Text ?? "")
                    .ToList();

                // The step-1 heading has to name CSVs; "Dynasty export" alone
                // reads as "your save" to someone who has not used the export
                // tool yet.
                Assert.Contains(lines, l => l.StartsWith("1.", StringComparison.Ordinal)
                    && l.Contains("CSV", StringComparison.OrdinalIgnoreCase));

                // And somewhere it has to say the save itself is not the input.
                var text = string.Join(" ", lines);
                Assert.Contains("save", text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("export tool", text, StringComparison.OrdinalIgnoreCase);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        // STA only exists on Windows; the headless platform does not need it.
        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "the window did not finish building in time");
        Assert.Null(failure);
    }
}
