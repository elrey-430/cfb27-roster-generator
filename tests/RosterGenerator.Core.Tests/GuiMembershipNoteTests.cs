using System.Reflection;

using Avalonia.Controls;
using Avalonia.VisualTree;

using RosterGenerator.Gui;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The app says when a school had not reached the FBS in the season being
/// recreated.
///
/// <para>CFB27 ships the 138 teams of <em>today</em>, so a 2010 roster for
/// Sacramento State, James Madison or Liberty builds perfectly and is wrong,
/// and nothing in the game says so. The command line's <c>validate</c> has
/// reported this since Milestone 13; the desktop app never asked the question
/// at all, and the desktop app is where the team and the season are actually
/// chosen.</para>
///
/// <para>It is a note, never a block. The dates are this project's reading of
/// the record, they live in a JSON file the user can edit, and refusing to
/// build somebody's roster over a date this project got wrong would be the
/// worse failure.</para>
/// </summary>
public sealed class GuiMembershipNoteTests
{
    // Spelled the way CFB27 spells it, which is what the team dropdown shows
    // and what the membership file is keyed on. The game's LongName for this
    // school is "Sacramento State" and its DisplayName is "Sac State"; keying
    // on the wrong one silently checks nothing.
    private const string BeforeItJoined = "Sac State";

    private static void OnUiThread(Action<MainWindow> body) => HeadlessGui.Run(body);

    private static TextBlock Note(MainWindow window) =>
        (TextBlock)typeof(MainWindow)
            .GetField("_membershipNote", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    /// <summary>
    /// Sets the two boxes the note watches. The headless platform does not
    /// raise the events a real click would, so the handler is called directly.
    /// </summary>
    private static void Choose(MainWindow window, string? team, string season)
    {
        var box = (ComboBox)typeof(MainWindow)
            .GetField("_teamBox", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;
        if (team is not null)
        {
            box.ItemsSource = new[] { team };
            box.SelectedItem = team;
        }

        var seasonBox = (TextBox)typeof(MainWindow)
            .GetField("_seasonBox", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;
        seasonBox.Text = season;

        typeof(MainWindow)
            .GetMethod("UpdateMembershipNote", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
    }

    [Fact]
    public void ASchoolThatWasStillFcsThatYearIsNamed()
    {
        OnUiThread(window =>
        {
            Choose(window, BeforeItJoined, "2010");

            var note = Note(window);
            Assert.True(note.IsVisible, "nothing on screen said the school was not in the FBS.");
            Assert.Contains(BeforeItJoined, note.Text ?? "");
        });
    }

    [Fact]
    public void TheNoteSaysTheRosterIsStillGenerated()
    {
        OnUiThread(window =>
        {
            Choose(window, BeforeItJoined, "2010");

            // Advisory, and it has to read that way. A user who knows better
            // than the data must not think the tool has refused them.
            var text = Note(window).Text ?? "";
            Assert.Contains("still generated", text);
            Assert.Contains("FbsMembership.json", text);
        });
    }

    [Fact]
    public void ASchoolThatWasThereSaysNothing()
    {
        OnUiThread(window =>
        {
            Choose(window, "Florida State", "2010");

            Assert.False(Note(window).IsVisible);
        });
    }

    [Fact]
    public void TheSameSchoolInALaterSeasonSaysNothing()
    {
        OnUiThread(window =>
        {
            Choose(window, BeforeItJoined, "2010");
            Assert.True(Note(window).IsVisible);

            // The note follows the season box, because the season is something
            // the user changes after the roster has already been checked.
            Choose(window, team: null, season: "2026");

            Assert.False(Note(window).IsVisible);
        });
    }

    [Fact]
    public void NoSeasonAsksNoQuestion()
    {
        OnUiThread(window =>
        {
            Choose(window, BeforeItJoined, "");

            // Without a year there is nothing to check, and a warning about a
            // season nobody has chosen would be noise.
            Assert.False(Note(window).IsVisible);
        });
    }

    [Fact]
    public void TheNoteIsNotABlocker()
    {
        OnUiThread(window =>
        {
            Choose(window, BeforeItJoined, "2010");

            // Generate's own explanation line is the one that gates the run;
            // membership must never appear there.
            var blocker = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? "")
                .FirstOrDefault(t => t.StartsWith("Cannot generate", StringComparison.Ordinal)) ?? "";
            Assert.DoesNotContain("FBS", blocker);
        });
    }
}
