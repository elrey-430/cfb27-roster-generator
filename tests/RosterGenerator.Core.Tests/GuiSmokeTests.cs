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
