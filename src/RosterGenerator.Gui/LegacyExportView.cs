using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Legacy;
using RosterGenerator.Core.Mapping;

namespace RosterGenerator.Gui;

/// <summary>
/// What the export tab needs from the window around it.
///
/// <para>The dynasty is chosen once and shared, rather than loaded a second
/// time on this tab: opening a save means unpacking 30 MB of bit-packed tables,
/// and asking somebody to sit through that twice because they changed tab would
/// be a poor way to repay them for finding the feature.</para>
/// </summary>
internal interface IDynastyHost
{
    /// <summary>The dynasty currently open, or null.</summary>
    DynastyPackage? Package { get; }

    /// <summary>Asks for a dynasty save, an export folder, or a .zip of one.</summary>
    Task PickSaveAsync();

    /// <summary>Asks for a folder of exported CSVs.</summary>
    Task PickFolderAsync();

    /// <summary>Asks for a .zip of exported CSVs.</summary>
    Task PickArchiveAsync();
}

/// <summary>
/// The other direction: a CFB27 dynasty written back out into a PS2-era NCAA
/// Football roster file, to be played on the console it came from.
///
/// <para>Its own tab because it shares nothing with the rest of the window. The
/// generator reads a roster CSV and writes a dynasty; this reads a dynasty and
/// writes a console roster file, and needs neither a roster CSV, a season, nor
/// any of the four options that govern how players are rated. Bolting it onto
/// the main panel would have meant five controls that grey themselves out.</para>
///
/// <para>Three things are said plainly on the tab rather than left to be
/// discovered, because each one changes what somebody gets: a PS2 squad is
/// smaller than a CFB27 one so the depth chart cuts a sixth of the roster,
/// nobody changes position on the way across, and ratings go through a five-bit
/// scale that can move one by half a step.</para>
/// </summary>
internal sealed class LegacyExportView : UserControl
{
    /// <summary>The team-picker entry meaning "all of them".</summary>
    private const string EveryTeam = "Every school both games have";

    private readonly IDynastyHost _host;

    private readonly TextBox _dynastyBox = new()
    {
        Watermark = "Your CFB27 dynasty save, or a folder of exported CSVs",
        IsReadOnly = true,
    };
    private readonly TextBox _legacyBox = new()
    {
        Watermark = "Your PS2 memory-card save (.psu), or a bare roster file",
        IsReadOnly = true,
    };

    /// <summary>
    /// What the chosen file turned out to be, read off the file rather than
    /// asked for. Shown because it decides what the user gets back.
    /// </summary>
    private readonly TextBlock _legacyNote = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        Opacity = 0.75,
        IsVisible = false,
    };

    /// <summary>
    /// Offered only when the source is a save, because for a bare roster file
    /// the ordinary output already <em>is</em> the database.
    /// </summary>
    private readonly CheckBox _databaseBox = new()
    {
        Content = "Also write the roster on its own, for a database editor",
        IsChecked = false,
        IsVisible = false,
    };
    private readonly ComboBox _teamBox = new()
    {
        PlaceholderText = "Choose your dynasty first",
        MinWidth = 320,
        IsEnabled = false,
    };
    private readonly TextBox _outputBox = new()
    {
        Watermark = "Where the new file goes",
        IsReadOnly = true,
    };
    private readonly Button _exportButton = new() { Content = "Write it", IsEnabled = false };
    private readonly TextBlock _blocker = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly SelectableTextBlock _output = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas, Menlo, monospace"),
    };

    /// <summary>Where data/LegacyTeamIds.json is, or null when it is missing.</summary>
    private static string? TeamIdsPath
    {
        get
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", "LegacyTeamIds.json");
            return File.Exists(path) ? path : null;
        }
    }

    public LegacyExportView(IDynastyHost host)
    {
        _host = host;
        Content = BuildLayout();
        UpdateButtons();
    }

    /// <summary>
    /// Called by the window when a dynasty is opened or cleared, so the team
    /// picker lists what can actually be written rather than what the PS2 file
    /// happens to carry.
    /// </summary>
    public void DynastyChanged()
    {
        _dynastyBox.Text = _host.Package?.Export.PlayerTablePath is { Length: > 0 } table
            ? Path.GetDirectoryName(table)
            : "";

        _teamBox.ItemsSource = null;
        _teamBox.IsEnabled = false;

        if (_host.Package is null || TeamIdsPath is not { } teamIds)
        {
            _teamBox.PlaceholderText = TeamIdsPath is null
                ? "data\\LegacyTeamIds.json is missing from this installation"
                : "Choose your dynasty first";
            UpdateButtons();
            return;
        }

        try
        {
            var mappings = _host.Package.Export.Teams.Count > 0
                ? _host.Package.Export.BuildTeamMappings()
                : TeamMappingSet.Load(Path.Combine(AppContext.BaseDirectory, "data", "TeamMappings.json"));
            var paired = LegacyTeamPairing.Pair(teamIds, mappings, out var unpaired);

            _teamBox.ItemsSource = new[] { EveryTeam }
                .Concat(paired.Select(p => p.School).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                .ToList();
            _teamBox.SelectedIndex = 0;
            _teamBox.IsEnabled = true;

            // Said now rather than after the export: a user looking for their
            // school in the list deserves to know why it is not there.
            SetStatus(
                unpaired.Count == 0
                    ? $"{paired.Count} school(s) can be written."
                    : $"{paired.Count} school(s) can be written; {unpaired.Count} the PS2 file has cannot " +
                      "— they are listed below.",
                ok: true);
            if (unpaired.Count > 0)
            {
                _output.Text = "Schools the PS2 file carries that this dynasty cannot fill:\n\n" +
                               string.Concat(unpaired.Select(u => $"  - {u}\n"));
            }
        }
        catch (Exception ex)
        {
            _teamBox.PlaceholderText = "The team lists could not be read";
            SetStatus($"The team lists could not be read: {ex.Message}", ok: false);
        }

        UpdateButtons();
    }

    private Control BuildLayout()
    {
        var pickSave = new Button { Content = "Save file…" };
        pickSave.Click += async (_, _) => await _host.PickSaveAsync();
        var pickFolder = new Button { Content = "Browse…" };
        pickFolder.Click += async (_, _) => await _host.PickFolderAsync();
        var pickZip = new Button { Content = ".zip…" };
        pickZip.Click += async (_, _) => await _host.PickArchiveAsync();

        var pickLegacy = new Button { Content = "Browse…" };
        pickLegacy.Click += async (_, _) => await PickLegacyAsync();

        var pickOutput = new Button { Content = "Change…" };
        pickOutput.Click += async (_, _) => await PickOutputAsync();

        _exportButton.Click += async (_, _) => await ExportAsync();
        _teamBox.SelectionChanged += (_, _) => UpdateButtons();

        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Put your CFB27 teams into a PS2-era roster file.",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This is the reverse of \"Import old roster\": today's squads written back over the " +
                   "ones in an NCAA Football roster file from the PS2 era, to be played on the console " +
                   "it came from. You always get a NEW file — the one you point at is never touched.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        });

        panel.Children.Add(Labelled(
            "1.  Your dynasty",
            Row(_dynastyBox, pickSave, pickFolder, pickZip),
            "The same dynasty as the other tab — choosing one here chooses it there too, so a save only " +
            "ever has to be opened once."));
        panel.Children.Add(Labelled(
            "2.  The PS2 side",
            new StackPanel { Spacing = 4, Children = { Row(_legacyBox, pickLegacy), _legacyNote, _databaseBox } },
            "Your memory-card save (.psu) — what uLaunchELF writes and PS2 Save Builder reads — or the " +
            "bare roster file out of one. You get back the same kind you give it, so a save in means a " +
            "save out, ready to go straight back on the card. Your teams are written over the squads " +
            "already in it, which is what keeps its depth charts and captains valid."));
        panel.Children.Add(Labelled(
            "3.  Which teams",
            _teamBox,
            "One school, or every school both games have in one pass."));
        panel.Children.Add(Labelled(
            "4.  Where it goes",
            Row(_outputBox, pickOutput),
            "A new file in the Output folder beside this application unless you say otherwise."));

        panel.Children.Add(new TextBlock
        {
            Text = "Three things are worth knowing. A PS2 squad holds about 69 players against CFB27's " +
                   "85, so your dynasty's own depth chart decides who comes and everyone cut is named. " +
                   "Nobody changes position on the way across, so a slot your team has nobody for keeps " +
                   "the player it had. And that generation stores a rating in five bits — 32 steps " +
                   "across 0-99 — so a rating can move by half a step: an 84 stays an 84 and a 77 " +
                   "becomes a 76.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.7,
            Margin = new Thickness(0, 6, 0, 0),
        });

        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { _exportButton },
        });
        panel.Children.Add(_blocker);
        panel.Children.Add(_status);
        panel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            MinHeight = 200,
            Child = new ScrollViewer { Content = _output },
        });

        return new ScrollViewer { Content = panel };
    }

    private static Control Labelled(string label, Control content, string? hint = null)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold });
        if (hint is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = hint,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.7,
            });
        }

        panel.Children.Add(content);
        return panel;
    }

    private static Control Row(TextBox box, params Button[] buttons)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(box, 0);
        grid.Children.Add(box);

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(6, 0, 0, 0),
        };
        foreach (var button in buttons)
        {
            stack.Children.Add(button);
        }

        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);
        return grid;
    }

    private async Task PickLegacyAsync()
    {
        // A PS2 roster file has no extension worth filtering on — offering one
        // would hide the very file the user came to choose.
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose your PS2 memory-card save, or a bare roster file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PS2 save or roster file")
                {
                    Patterns = new[] { "*.psu", "*.db", "*" },
                },
            },
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is not { Length: > 0 } path)
        {
            return;
        }

        _legacyBox.Text = path;

        // Identified now rather than at export time: a user who picked the
        // wrong file should find out while they can still change it, and
        // whether it is a save decides what they are about to get back.
        var isSave = Ps2MemoryCardSave.LooksLikeSave(path);
        _legacyNote.IsVisible = true;
        _databaseBox.IsVisible = isSave;
        try
        {
            var source = await Task.Run(() => LegacyRosterSource.Open(path));
            _legacyNote.Text = $"Read as {source.Describe()}. " + (source.InSave
                ? "You will get a save back — put it straight on your memory card."
                : "You will get a bare roster file back, the kind a database editor opens.");
            _legacyNote.Foreground = null;
        }
        catch (Exception ex)
        {
            _legacyNote.Text = ex.Message;
            _legacyNote.Foreground = Brushes.IndianRed;
            _legacyBox.Text = "";
            _databaseBox.IsVisible = false;
        }

        if (_legacyBox.Text is { Length: > 0 })
        {
            _outputBox.Text = Path.Combine(
                AppContext.BaseDirectory, "Output", Path.GetFileName(path));
        }

        UpdateButtons();
    }

    private async Task PickOutputAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var suggested = _outputBox.Text is { Length: > 0 } current
            ? Path.GetFileName(current)
            : "PS2_Roster";
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the PS2 roster file",
            SuggestedFileName = suggested,
        });

        if (file?.TryGetLocalPath() is { Length: > 0 } path)
        {
            _outputBox.Text = path;
        }

        UpdateButtons();
    }

    private async Task ExportAsync()
    {
        if (_host.Package is not { } package || TeamIdsPath is not { } teamIds ||
            _legacyBox.Text is not { Length: > 0 } legacyPath ||
            _outputBox.Text is not { Length: > 0 } outputPath)
        {
            return;
        }

        _exportButton.IsEnabled = false;
        SetStatus("Writing the roster file…", ok: null);
        var wanted = _teamBox.SelectedItem as string ?? EveryTeam;

        try
        {
            var scale = LegacyRatingScale.Load(
                Path.Combine(AppContext.BaseDirectory, "data", "LegacyRatingScale.json"));
            var mappings = package.Export.Teams.Count > 0
                ? package.Export.BuildTeamMappings()
                : TeamMappingSet.Load(Path.Combine(AppContext.BaseDirectory, "data", "TeamMappings.json"));

            IReadOnlyList<LegacyExportTeam> teams;
            IReadOnlyList<string> unpaired = Array.Empty<string>();
            if (wanted == EveryTeam)
            {
                teams = LegacyTeamPairing.Pair(teamIds, mappings, out unpaired);
            }
            else
            {
                var one = LegacyTeamPairing.Find(teamIds, mappings, wanted)
                    ?? throw new InvalidDataException(
                        $"'{wanted}' is not a school both games have.");
                teams = new[] { one };
            }

            // Beside the save, with the extension swapped, because somebody
            // asking for both wants them in the same place.
            var databaseOut = _databaseBox is { IsVisible: true, IsChecked: true }
                ? Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(outputPath))!,
                    Path.GetFileNameWithoutExtension(outputPath) + ".db")
                : null;

            var result = await Task.Run(() =>
            {
                var roster = package.Export.LoadPlayerRoster();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
                return LegacyRosterExporter.Export(
                    legacyPath, outputPath, roster, teams, scale,
                    LegacyDepthChart.For(package.Export), databaseOut);
            });

            _output.Text = Describe(result, unpaired);
            SetStatus(
                $"Done — {result.Teams.Sum(t => t.Written.Count)} player(s) written to " +
                $"{Path.GetFileName(result.Path)}.",
                ok: true);
        }
        catch (Exception ex)
        {
            _output.Text = ex.Message;
            SetStatus("The roster file could not be written; nothing was changed.", ok: false);
        }
        finally
        {
            UpdateButtons();
        }
    }

    private static string Describe(LegacyExportResult result, IReadOnlyList<string> unpaired)
    {
        var written = result.Teams.Sum(t => t.Written.Count);
        var cut = result.Teams.Sum(t => t.Cut.Count);
        var kept = result.Teams.Sum(t => t.Unfilled.Count);
        var charted = result.Teams.Count(t => t.DepthChartDecided);

        var text = new System.Text.StringBuilder();
        text.AppendLine($"Written to: {Path.GetFullPath(result.Path)}");
        text.AppendLine(result.WroteSave
            ? "  A PS2 memory-card save — put it straight on your card with uLaunchELF or PS2 Save\n" +
              "  Builder. No database editor needed. Every other file in the save came through\n" +
              "  untouched."
            : "  A bare roster file, the kind a database editor opens. Choose a .psu save instead\n" +
              "  and you get a save back.");
        if (result.DatabasePath is { } databasePath)
        {
            text.AppendLine($"  Roster on its own also written to {Path.GetFullPath(databasePath)}.");
        }

        text.AppendLine();
        text.AppendLine($"  {written} player(s) written across {result.Teams.Count} team(s).");
        text.AppendLine($"  {cut} cut — the squads had no room.");
        text.AppendLine($"  {kept} slot(s) left exactly as they were, your team having nobody for them.");
        text.AppendLine(charted == result.Teams.Count
            ? "  Your dynasty's own depth chart decided who came, at every team."
            : charted == 0
                ? "  This dynasty carries no depth chart, so the cut fell back to overall."
                : $"  The depth chart decided {charted} of {result.Teams.Count} team(s); the rest fell " +
                  "back to overall, having no chart in this dynasty.");

        // One team's cuts are worth reading in full; a hundred teams' are not.
        var detailed = result.Teams.Count == 1 ? result.Teams : Array.Empty<LegacyExportedTeam>();
        foreach (var team in detailed.Where(t => t.Cut.Count > 0))
        {
            text.AppendLine();
            text.AppendLine($"{team.Team} — cut, deepest on the chart first:");
            foreach (var player in team.Cut)
            {
                text.AppendLine($"  - {player}");
            }
        }

        foreach (var team in detailed.Where(t => t.Unfilled.Count > 0))
        {
            text.AppendLine();
            text.AppendLine($"{team.Team} — slots kept as they were:");
            text.AppendLine($"  {string.Join(", ", team.Unfilled)}");
        }

        var notes = result.Teams.SelectMany(t => t.Notes.Select(n => $"{t.Team}: {n}")).ToList();
        if (notes.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Names the old format could not hold in full:");
            foreach (var note in notes)
            {
                text.AppendLine($"  - {note}");
            }
        }

        var skipped = result.Skipped.Concat(unpaired).ToList();
        if (skipped.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Left with the squads they had:");
            foreach (var line in skipped)
            {
                text.AppendLine($"  - {line}");
            }
        }

        text.AppendLine();
        text.AppendLine(
            "Ratings went through the measured five-bit scale, so one can move by half a step. " +
            "Everything that format cannot hold — the other 39 rating columns, and anything past 13 " +
            "characters of a surname — is listed above rather than silently dropped.");
        return text.ToString();
    }

    private void UpdateButtons()
    {
        _exportButton.IsEnabled =
            _host.Package is not null &&
            !string.IsNullOrWhiteSpace(_legacyBox.Text) &&
            !string.IsNullOrWhiteSpace(_outputBox.Text) &&
            _teamBox.SelectedItem is string;

        _blocker.Text = WhyExportIsUnavailable();
        _blocker.IsVisible = _blocker.Text is { Length: > 0 };
    }

    /// <summary>
    /// What is standing between the user and the export, in their own terms. A
    /// greyed-out button with nothing saying why is the worst thing a window
    /// can do.
    /// </summary>
    private string WhyExportIsUnavailable()
    {
        if (TeamIdsPath is null)
        {
            return "Cannot write: data\\LegacyTeamIds.json is missing from this installation, and " +
                   "without it there is no way to tell which PS2 team is which school.";
        }

        if (_host.Package is null)
        {
            return "Cannot write yet — choose your CFB27 dynasty in step 1.";
        }

        if (string.IsNullOrWhiteSpace(_legacyBox.Text))
        {
            return "Cannot write yet — choose the PS2 roster file in step 2.";
        }

        return "";
    }

    private void SetStatus(string message, bool? ok)
    {
        _status.Text = message;
        _status.Foreground = ok switch
        {
            true => Brushes.SeaGreen,
            false => Brushes.IndianRed,
            _ => Brushes.Gray,
        };
    }
}
