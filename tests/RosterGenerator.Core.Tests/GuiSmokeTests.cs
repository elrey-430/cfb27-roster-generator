using Avalonia.Controls;
using Avalonia.VisualTree;

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
    [Fact]
    public void TheWindowBuildsAndShows()
    {
        HeadlessGui.Run(window =>
        {
            Assert.Equal("Historical CFB27 Roster Generator", window.Title);
            Assert.NotNull(window.Content);

            // Generation must be impossible until both files are chosen —
            // the button being live with nothing selected is the most
            // obvious way a first-time user gets an exception dialog.
            var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
            var generate = buttons.SingleOrDefault(b => (b.Content as string) == "Generate");
            Assert.NotNull(generate);
            Assert.False(generate!.IsEnabled);

            // The single most confusable thing about this tool is what step 1
            // wants. It now takes the dynasty save itself *or* a folder of
            // exported CSVs, and the window has to offer both — a user who has
            // only ever used the export tool must not think the save route
            // replaced it, and a user who has never exported anything must not
            // think they have to.
            var lines = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? "")
                .ToList();
            var buttonLabels = buttons.Select(b => b.Content as string ?? "").ToList();
            var text = string.Join(" ", lines);

            // Both routes have to be reachable, and the save one has to be a
            // button rather than something you can only type.
            Assert.Contains(buttonLabels, b => b.Contains("Save file", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(buttonLabels, b => b.Contains(".zip", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("save", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CSV", text, StringComparison.OrdinalIgnoreCase);

            // And the promise that makes the save route safe to try.
            Assert.Contains("never modified", text, StringComparison.OrdinalIgnoreCase);

            // The window must not still claim it cannot read a save.
            Assert.DoesNotContain("does not read your save", text, StringComparison.OrdinalIgnoreCase);
        });
    }
}
