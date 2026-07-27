using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

using RosterGenerator.Core.Dynasty;
using RosterGenerator.Core.Equipment;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Pipeline;
using RosterGenerator.Core.Rating;

namespace RosterGenerator.Gui;

/// <summary>
/// The whole application in one window, laid out as the job is actually done:
/// point at the exported CSVs, point at the roster, check it, choose the team,
/// generate.
///
/// Every decision it makes goes through <see cref="RosterGenerationService"/>
/// and <see cref="RosterCsvValidator"/> — the same code the command line runs.
/// The window's job is to ask the questions and show the answers, never to
/// decide anything about a roster itself.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly TextBox _dynastyBox = new()
    {
        Watermark = "Folder of CSV files exported from your dynasty",
        IsReadOnly = true,
    };
    private readonly TextBox _rosterBox = new() { Watermark = "Your roster CSV", IsReadOnly = true };
    private readonly ComboBox _teamBox = new() { PlaceholderText = "Choose a team", MinWidth = 260 };
    private readonly TextBox _seasonBox = new() { Watermark = "Season (optional)", Width = 160 };
    private readonly CheckBox _ratingsBox = new() { Content = "Generate ratings from the roster CSV", IsChecked = true };
    private readonly CheckBox _archetypesBox = new() { Content = "Choose each player's archetype", IsChecked = true };
    private readonly CheckBox _fillBox = new() { Content = "Fill the rest of the roster as depth", IsChecked = true };
    private readonly CheckBox _facesBox = new()
    {
        Content = "Replace real players' faces on slots you take over",
        IsChecked = true,
    };
    private readonly CheckBox _equipmentBox = new()
    {
        Content = "Use period-correct helmets for the season",
        IsChecked = true,
    };
    private readonly Button _checkButton = new() { Content = "Check roster file", IsEnabled = false };
    private readonly Button _generateButton = new() { Content = "Generate", IsEnabled = false };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly SelectableTextBlock _output = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas, Menlo, monospace"),
    };

    private DynastyExport? _dynasty;

    /// <summary>Builds the window.</summary>
    public MainWindow()
    {
        Title = "Historical CFB27 Roster Generator";
        Width = 900;
        Height = 760;
        MinWidth = 720;
        MinHeight = 560;

        _dynastyBox.TextChanged += (_, _) => UpdateButtons();
        _rosterBox.TextChanged += (_, _) => UpdateButtons();
        _ratingsBox.IsCheckedChanged += (_, _) => OnRatingsToggled();
        _checkButton.Click += async (_, _) => await CheckAsync();
        _generateButton.Click += async (_, _) => await GenerateAsync();

        Content = BuildLayout();
        UpdateButtons();
    }

    private Control BuildLayout()
    {
        var browseDynasty = new Button { Content = "Browse…" };
        browseDynasty.Click += async (_, _) => await PickDynastyAsync();

        // A user who moved their export between machines has a .zip, not the
        // folder that was inside it. Making them unpack it first is a step
        // that only ever existed because the tool could not read an archive.
        var browseDynastyZip = new Button { Content = ".zip…" };
        browseDynastyZip.Click += async (_, _) => await PickDynastyArchiveAsync();

        var browseRoster = new Button { Content = "Browse…" };
        browseRoster.Click += async (_, _) => await PickRosterAsync();

        var openTemplates = new Button { Content = "Where do I start?" };
        openTemplates.Click += (_, _) => ShowGettingStarted();

        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16) };

        panel.Children.Add(new TextBlock
        {
            Text = "Recreate a historical roster inside your CFB27 dynasty.",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Works on the CSV files the community tool exports from your dynasty, and writes a " +
                   "new CSV for you to import with the roster editor — your save is never opened or " +
                   "changed. Only a name and a position are required per player; anything you leave " +
                   "out is filled in for you and listed in the report.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        });

        panel.Children.Add(Labelled(
            "1.  Exported dynasty CSVs",
            Row(_dynastyBox, browseDynasty, browseDynastyZip),
            "This tool does not read your save file. Export your dynasty with the community " +
            "export tool first — it writes a folder of CSV files, one per table — and choose that " +
            "folder here, or the .zip of it if that is what you have. The Player and Team tables " +
            "are found for you, and nothing you choose here is ever modified."));
        panel.Children.Add(Labelled(
            "2.  Roster CSV",
            Row(_rosterBox, browseRoster, openTemplates),
            "The historical roster you filled in yourself, from templates\\."));

        var teamRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _teamBox, _seasonBox },
        };
        panel.Children.Add(Labelled("3.  Team and season", teamRow));

        var options = new StackPanel
        {
            Spacing = 4,
            Children = { _ratingsBox, _archetypesBox, _fillBox, _equipmentBox, _facesBox },
        };
        panel.Children.Add(Labelled("4.  Options", options));

        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { _checkButton, _generateButton },
        });

        panel.Children.Add(_status);
        panel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            MinHeight = 220,
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

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(6, 0, 0, 0) };
        foreach (var button in buttons)
        {
            stack.Children.Add(button);
        }

        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);
        return grid;
    }

    private async Task PickDynastyAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the folder of CSV files exported from your dynasty",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return;
        }

        _dynastyBox.Text = folders[0].Path.LocalPath;
        LoadDynasty();
    }

    private async Task PickDynastyArchiveAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select the .zip of the CSV files exported from your dynasty",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Zipped dynasty export") { Patterns = new[] { "*.zip" } },
            },
        });

        if (files.Count == 0)
        {
            return;
        }

        _dynastyBox.Text = files[0].Path.LocalPath;
        LoadDynasty();
    }

    private void LoadDynasty()
    {
        _teamBox.ItemsSource = null;
        _dynasty = null;

        try
        {
            _dynasty = RosterGenerationService.OpenDynasty(_dynastyBox.Text ?? "");
            _teamBox.ItemsSource = _dynasty.Teams.Select(t => t.DisplayName).ToList();
            SetStatus(
                $"Read {_dynasty.Teams.Count} teams from {Path.GetFileName(_dynasty.PlayerTablePath)}.",
                ok: true);
        }
        catch (Exception ex)
        {
            SetStatus(
                "No player table was found there. Choose the folder the dynasty export tool wrote " +
                $"its CSV files into, or a .zip of that folder. ({ex.Message})",
                ok: false);
        }

        UpdateButtons();
    }

    private async Task PickRosterAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select your roster CSV",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Roster files") { Patterns = new[] { "*.csv", "*.json" } },
            },
        });

        if (files.Count == 0)
        {
            return;
        }

        _rosterBox.Text = files[0].Path.LocalPath;
        UpdateButtons();

        // Checking straight away is the whole point of having the check: the
        // user finds out now, not after a 27 MB file has been written.
        await CheckAsync();
    }

    private async Task CheckAsync()
    {
        var rosterPath = _rosterBox.Text;
        if (string.IsNullOrWhiteSpace(rosterPath))
        {
            return;
        }

        SetStatus("Checking…", ok: null);
        var team = _teamBox.SelectedItem as string;
        var season = ParseSeason();

        try
        {
            var report = await Task.Run(() =>
            {
                var positions = PositionMappingSet.Load(
                    RosterGenerationService.FindDataFile(null, "PositionMappings.json"));
                var ratings = RatingEngine.Load(
                    RosterGenerationService.FindDataFile(null, "RatingModels.json"),
                    RosterGenerationService.FindDataFile(null, "OverallFormulas.json"),
                    RosterGenerationService.FindOptionalDataFile(null, "ArchetypeProfiles.json"));
                return RosterCsvValidator.Check(rosterPath, positions, _dynasty, team, season, ratings);
            });

            _output.Text = report.ToText();

            // Preselect the team the file names, so the common case needs no
            // extra click.
            if (team is null && report.Roster is not null && _teamBox.ItemsSource is IEnumerable<string> names)
            {
                var match = names.FirstOrDefault(n =>
                    string.Equals(n, report.Roster.School, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    _teamBox.SelectedItem = match;
                }
            }

            if (season is null && report.Roster is { Season: > 0 })
            {
                _seasonBox.Text = report.Roster.Season.ToString();
            }

            var blocking = report.OfSeverity(RosterCsvSeverity.Blocking).Count();
            var warnings = report.OfSeverity(RosterCsvSeverity.Warning).Count();
            SetStatus(
                blocking > 0
                    ? $"{blocking} problem(s) must be fixed before generating."
                    : warnings > 0
                        ? $"Ready — {report.UsablePlayers} players, {warnings} thing(s) worth a look."
                        : $"Ready — {report.UsablePlayers} players, nothing to fix.",
                ok: blocking == 0);
        }
        catch (Exception ex)
        {
            _output.Text = ex.Message;
            SetStatus("The roster file could not be read.", ok: false);
        }

        UpdateButtons();
    }

    private async Task GenerateAsync()
    {
        var request = new RosterGenerationRequest
        {
            DynastyPath = _dynastyBox.Text ?? "",
            RosterPath = _rosterBox.Text ?? "",
            Team = _teamBox.SelectedItem as string,
            Season = ParseSeason(),
            Ratings = _ratingsBox.IsChecked == true ? RatingsMode.Generate : RatingsMode.Inherit,
            SelectArchetypes = _ratingsBox.IsChecked == true && _archetypesBox.IsChecked == true,
            FillRoster = _ratingsBox.IsChecked == true && _fillBox.IsChecked == true,
            ApplyEquipment = _equipmentBox.IsChecked == true,
            ReplaceRealPersonFaces = _facesBox.IsChecked == true,
        };

        _generateButton.IsEnabled = false;
        _checkButton.IsEnabled = false;
        SetStatus("Generating… this takes a few seconds for a 16,500-row player table.", ok: null);

        try
        {
            var result = await Task.Run(() => new RosterGenerationService().Run(request));
            _output.Text = Describe(result);
            SetStatus(
                $"Done — {result.Converted} players written, {result.Filled} slots filled. " +
                $"Import {Path.GetFileName(result.OutputPath)} with your roster editor.",
                ok: true);
        }
        catch (Exception ex)
        {
            _output.Text = ex.Message;
            SetStatus("Generation failed; nothing was written.", ok: false);
        }
        finally
        {
            UpdateButtons();
        }
    }

    private static string Describe(RosterGenerationResult result)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"Players written:   {result.Converted}");
        text.AppendLine($"Players skipped:   {result.Skipped}");
        text.AppendLine($"Slots filled:      {result.Filled}");
        text.AppendLine($"Validation:        0 errors, {result.Export.Report.Warnings.Count()} warnings");
        text.AppendLine();
        text.AppendLine($"Roster written to: {Path.GetFullPath(result.OutputPath)}");
        text.AppendLine($"Report written to: {Path.GetFullPath(result.ReportPath)}");
        text.AppendLine();
        text.AppendLine("The report lists every value that was filled in or corrected for you.");

        if (result.Equipment is { } equipment)
        {
            text.AppendLine();
            text.AppendLine(equipment.Describe());
            if (result.EquipmentOutputPath is { } equipmentPath)
            {
                text.AppendLine($"Equipment written to: {Path.GetFullPath(equipmentPath)}");
                text.AppendLine("Import that alongside the roster.");
            }
        }

        if (result.CsvCorrections.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Values cleaned up while reading your file:");
            foreach (var correction in result.CsvCorrections.Take(20))
            {
                text.AppendLine($"  - {correction}");
            }
        }

        if (result.CsvWarnings.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Values that could not be used as written:");
            foreach (var warning in result.CsvWarnings.Take(20))
            {
                text.AppendLine($"  - {warning}");
            }
        }

        return text.ToString();
    }

    private void ShowGettingStarted()
    {
        var templates = Path.Combine(AppContext.BaseDirectory, "templates");
        _output.Text =
            "Start from the basics template and fill in whatever you can find:\n\n" +
            $"  {Path.Combine(templates, "HistoricalRosterTemplate_Basics.csv")}\n\n" +
            "  FirstName,LastName,Position,Number,Class,Role,Team,Season\n" +
            "  Jordan,Travis,QB,13,RS Senior,Starter,Florida State,2023\n\n" +
            "Only FirstName, LastName and Position are required. Role (Starter / Backup / Reserve / " +
            "Walk-on) is the single most useful thing you can add — without it, players you supply " +
            "nothing else for all come out within a couple of points of each other.\n\n" +
            "If you have statistics, draft positions or awards, use the fuller template in the same " +
            "folder. More detail makes better ratings, but none of it is required.\n\n" +
            $"  {Path.Combine(templates, "HistoricalRosterTemplate.csv")}";
        SetStatus("Copy a template, fill it in, then come back and choose it above.", ok: null);
    }

    private int? ParseSeason() =>
        int.TryParse(_seasonBox.Text, out var season) ? season : null;

    private void OnRatingsToggled()
    {
        // Both of these write ratings, so neither can run without them. The
        // service refuses the combination; the window simply does not offer it.
        var on = _ratingsBox.IsChecked == true;
        _archetypesBox.IsEnabled = on;
        _fillBox.IsEnabled = on;
        if (!on)
        {
            _archetypesBox.IsChecked = false;
            _fillBox.IsChecked = false;
        }
    }

    private void UpdateButtons()
    {
        var hasRoster = !string.IsNullOrWhiteSpace(_rosterBox.Text);
        var hasDynasty = _dynasty is not null;
        _checkButton.IsEnabled = hasRoster;
        _generateButton.IsEnabled = hasRoster && hasDynasty;
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
