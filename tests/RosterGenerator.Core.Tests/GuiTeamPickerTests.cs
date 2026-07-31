using System.Reflection;

using Avalonia.Controls;

using RosterGenerator.Gui;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The team picker gets out of the way when the roster file names its teams.
///
/// <para>Reported: a roster covering every team could not be generated because
/// the run was limited to one selected team. The window sent its selection on
/// every run, and an explicit team used to beat each row's own — so a
/// whole-season file was silently written onto one school.</para>
///
/// <para>The picker is now a fallback for files with no Team column, and the
/// window says which of the two is happening rather than leaving the user to
/// infer it from a player count.</para>
/// </summary>
public sealed class GuiTeamPickerTests
{
    private static void OnUiThread(Action<MainWindow> body) => HeadlessGui.Run(body);

    private static T Field<T>(MainWindow window, string name) =>
        (T)typeof(MainWindow)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    /// <summary>Sets the teams the file named and refreshes the note.</summary>
    private static void FileNames(MainWindow window, params string[] teams)
    {
        typeof(MainWindow)
            .GetField("_rosterTeams", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(window, teams);
        typeof(MainWindow)
            .GetMethod("UpdateTeamNote", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
    }

    [Fact]
    public void AWholeSeasonFileTakesThePickerOutOfPlay()
    {
        OnUiThread(window =>
        {
            FileNames(window, "Florida State", "Alabama", "Michigan", "Ohio State");

            var picker = Field<ComboBox>(window, "_teamBox");
            var note = Field<TextBlock>(window, "_teamNote");

            Assert.False(picker.IsEnabled, "the picker still limits a file that names its own teams.");
            Assert.Null(picker.SelectedItem);
            Assert.Contains("4 teams", note.Text ?? "");
        });
    }

    [Fact]
    public void ASingleTeamFileAlsoUsesItsOwnTeam()
    {
        OnUiThread(window =>
        {
            FileNames(window, "Florida State");

            var note = Field<TextBlock>(window, "_teamNote");
            Assert.False(Field<ComboBox>(window, "_teamBox").IsEnabled);
            Assert.Contains("Florida State", note.Text ?? "");
        });
    }

    [Fact]
    public void AFileWithNoTeamColumnStillGetsThePicker()
    {
        OnUiThread(window =>
        {
            FileNames(window);

            var picker = Field<ComboBox>(window, "_teamBox");
            var note = Field<TextBlock>(window, "_teamNote");

            // Removing the picker outright would break the one case it exists
            // for: a file that genuinely does not say where its players go.
            Assert.True(picker.IsEnabled);
            Assert.Contains("no Team column", note.Text ?? "");
        });
    }

    [Fact]
    public void TheWindowSaysWhichOfTheTwoIsHappening()
    {
        OnUiThread(window =>
        {
            FileNames(window);
            var withoutTeams = Field<TextBlock>(window, "_teamNote").Text ?? "";

            FileNames(window, "Alabama", "Michigan");
            var withTeams = Field<TextBlock>(window, "_teamNote").Text ?? "";

            Assert.NotEqual(withoutTeams, withTeams);
            Assert.True(Field<TextBlock>(window, "_teamNote").IsVisible);
        });
    }
}
