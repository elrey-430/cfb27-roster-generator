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
        Watermark = "Your dynasty save, or a folder of exported CSVs",
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
    private readonly CheckBox _saveOutBox = new()
    {
        Content = "Write a new dynasty save (drop straight into the game)",
        IsChecked = true,
        IsVisible = false,
    };
    private readonly TextBlock _saveOutNote = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.75,
        IsVisible = false,
    };
    private readonly TextBlock _blocker = new()
    {
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
    };

    // CFB27 ships today's 138 teams, so nothing in the game says that the
    // school somebody just picked was still in the FCS in the year they just
    // typed. This sits under the two boxes that raise the question, and
    // answers it as they change rather than only when a roster is checked.
    private readonly TextBlock _membershipNote = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        Foreground = Brushes.DarkGoldenrod,
        IsVisible = false,
    };
    private readonly Button _checkButton = new() { Content = "Check roster file", IsEnabled = false };
    private readonly Button _generateButton = new() { Content = "Generate", IsEnabled = false };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly SelectableTextBlock _output = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas, Menlo, monospace"),
    };

    private readonly List<Button> _dynastyPickers = new();

    private DynastyExport? _dynasty;

    /// <summary>
    /// When each school reached the FBS. Loaded once; an absent or unreadable
    /// file simply means the question is never asked, because a missing data
    /// file must not stop somebody building a roster.
    /// </summary>
    private static readonly FbsMembership Membership = LoadMembership();

    // Held so the scratch folder an archive or a save is expanded into is
    // deleted when another dynasty is chosen, rather than left behind.
    private DynastyPackage? _package;

    /// <summary>True when the chosen dynasty is a save file rather than an export.</summary>
    private bool DynastyIsSave => _package?.IsNativeSave == true;

    // Why the last dynasty failed to open. Kept separately from the status
    // line because the status line is transient — checking a roster
    // afterwards used to overwrite it, leaving the user with "Ready" on
    // screen, Generate greyed out, and no trace of what had gone wrong.
    private string? _dynastyProblem;

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
        _teamBox.SelectionChanged += (_, _) => UpdateMembershipNote();
        _seasonBox.TextChanged += (_, _) => UpdateMembershipNote();
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

        var browseDynastySave = new Button { Content = "Save file…" };
        browseDynastySave.Click += async (_, _) => await PickDynastySaveAsync();

        _dynastyPickers.AddRange(new[] { browseDynastySave, browseDynasty, browseDynastyZip });

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
            Text = "Point it at your dynasty save and get a new save back, or work from exported " +
                   "CSVs if you prefer. Either way the dynasty you choose is never modified — what " +
                   "you get is always a new file. Only a name and a position are required per " +
                   "player; anything you leave out is filled in for you and listed in the report.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        });

        panel.Children.Add(Labelled(
            "1.  Your dynasty",
            Row(_dynastyBox, browseDynastySave, browseDynasty, browseDynastyZip),
            "Choose your dynasty save — \"Save file…\", from Documents\\EA SPORTS College " +
            "Football 27\\saves — and you can get a new save back at the end, with no exporting " +
            "and no separate importer. Everything needed to read one is included; there is " +
            "nothing to install. If you would rather work from exported CSVs, \"Browse…\" takes " +
            "that folder and \".zip…\" takes an archive of it. Nothing you choose here is ever " +
            "modified."));
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
        panel.Children.Add(Labelled(
            "3.  Team and season",
            new StackPanel { Spacing = 4, Children = { teamRow, _membershipNote } },
            "The season picks the era's helmets, and a roster CSV carrying a year per player puts each " +
            "of them in their own — so an all-time squad is not dressed in one decade. Leave both blank " +
            "to use what the roster file says."));

        var options = new StackPanel
        {
            Spacing = 4,
            Children = { _ratingsBox, _archetypesBox, _fillBox, _equipmentBox, _facesBox,
                         _saveOutBox, _saveOutNote },
        };
        panel.Children.Add(Labelled("4.  Options", options));

        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { _checkButton, _generateButton },
        });

        panel.Children.Add(_blocker);
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
        await LoadDynastyAsync();
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
        await LoadDynastyAsync();
    }

    private async Task PickDynastySaveAsync()
    {
        // A dynasty save has no extension, so there is no pattern to filter on
        // — offering one would hide the very file the user came to choose.
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select your dynasty save (Documents\\EA SPORTS College Football 27\\saves)",
            AllowMultiple = false,
            SuggestedStartLocation = await SavesFolderAsync(),
        });

        if (files.Count == 0)
        {
            return;
        }

        _dynastyBox.Text = files[0].Path.LocalPath;
        await LoadDynastyAsync();
    }

    /// <summary>
    /// The game's own saves folder, so the picker opens where the file is
    /// rather than wherever it was last. Null when it cannot be found, which
    /// simply leaves the picker's default.
    /// </summary>
    private async Task<IStorageFolder?> SavesFolderAsync()
    {
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (documents.Length == 0)
            {
                return null;
            }

            var saves = Path.Combine(documents, "EA SPORTS College Football 27", "saves");
            return Directory.Exists(saves)
                ? await StorageProvider.TryGetFolderFromPathAsync(saves)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Opens the chosen dynasty, off the UI thread.
    ///
    /// <para>Reading a dynasty save means unpacking 30 MB of bit-packed tables
    /// and writing the ones the generator needs back out as CSV — twenty to
    /// thirty seconds. Doing that inline froze the whole window for the
    /// duration, with nothing on screen to say anything was happening, so the
    /// app looked hung at exactly the moment it was working hardest.</para>
    /// </summary>
    private async Task LoadDynastyAsync()
    {
        var path = _dynastyBox.Text ?? "";

        _teamBox.ItemsSource = null;
        _dynasty = null;

        // An archive or a save was expanded into a scratch folder; disposing
        // the previous package is what deletes it. Without this, every
        // reselect leaks a copy of the dynasty's tables into the temp folder.
        _package?.Dispose();
        _package = null;
        _dynastyProblem = null;

        var isSave = NativeSave.LooksLikeSave(path);
        SetStatus(
            isSave
                ? $"Reading {Path.GetFileName(path)}… a dynasty save takes twenty seconds or so to open."
                : "Reading your dynasty…",
            ok: null);
        _blocker.IsVisible = false;
        SetPickersEnabled(false);

        try
        {
            var package = await Task.Run(() => DynastyPackage.Open(path));
            _package = package;
            _dynasty = package.Export;
            _teamBox.ItemsSource = _dynasty.Teams.Select(t => t.DisplayName).ToList();
            SetStatus(
                $"Read {_dynasty.Teams.Count} teams from {Path.GetFileName(_dynasty.PlayerTablePath)}." +
                (DynastyIsSave ? "  This is a dynasty save, so a new save can be written." : ""),
                ok: true);
        }
        catch (NativeSaveException ex)
        {
            _dynastyProblem = ex.Message;
            SetStatus(ex.Message, ok: false);
        }
        catch (Exception ex)
        {
            _dynastyProblem =
                "No player table was found there. Choose your dynasty save, the folder the export " +
                $"tool wrote its CSV files into, or a .zip of that folder. ({ex.Message})";
            SetStatus(_dynastyProblem, ok: false);
        }
        finally
        {
            SetPickersEnabled(true);
            UpdateSaveOption();
            UpdateButtons();
        }
    }

    /// <summary>
    /// Locks the file pickers while a dynasty is opening, so a second choice
    /// cannot start on top of the first and leave the two racing.
    /// </summary>
    private void SetPickersEnabled(bool enabled)
    {
        foreach (var button in _dynastyPickers)
        {
            button.IsEnabled = enabled;
        }
    }

    /// <summary>
    /// Shows the "write a new save" option only when there is a save to write
    /// into, and says why when the machine cannot do it.
    /// </summary>
    private void UpdateSaveOption()
    {
        _saveOutBox.IsVisible = DynastyIsSave;
        _saveOutNote.IsVisible = DynastyIsSave;
        if (!DynastyIsSave)
        {
            return;
        }

        if (NativeSave.IsAvailable(out var reason))
        {
            _saveOutBox.IsEnabled = true;
            _saveOutNote.Text =
                "The new save is written beside your original with \"-Recreated\" added to the name. " +
                "Your own save is never modified.";
        }
        else
        {
            _saveOutBox.IsEnabled = false;
            _saveOutBox.IsChecked = false;
            _saveOutNote.Text = reason;
        }
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
                // Membership was missing here, so the app never asked the one
                // question the command line's own check does: whether the
                // school existed in the season being recreated.
                return RosterCsvValidator.Check(
                    rosterPath, positions, _dynasty, team, season, ratings, Membership);
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

            // This check reads the roster file and nothing else, so it cannot
            // say the run is ready — only that the roster is. Saying "Ready"
            // while the dynasty had failed to load is what left users staring
            // at a greyed-out Generate with a reassuring message above it.
            var roster = blocking > 0
                ? $"{blocking} problem(s) must be fixed before generating."
                : warnings > 0
                    ? $"Roster is fine — {report.UsablePlayers} players, {warnings} thing(s) worth a look."
                    : $"Roster is fine — {report.UsablePlayers} players, nothing to fix.";
            SetStatus(roster, ok: blocking == 0);
        }
        catch (Exception ex)
        {
            _output.Text = ex.Message;
            SetStatus("The roster file could not be read.", ok: false);
        }

        UpdateButtons();
    }

    /// <summary>
    /// Where a written save goes: beside the user's own, with "-Recreated"
    /// added. Never over the original — the point of the whole feature is that
    /// the dynasty they gave us survives whatever they think of the result.
    /// </summary>
    private string RecreatedSavePath()
    {
        var source = _package?.SourceSavePath ?? _dynastyBox.Text ?? "";
        var folder = Path.GetDirectoryName(Path.GetFullPath(source)) ?? ".";
        var name = Path.GetFileName(source);
        return Path.Combine(folder, $"{name}-Recreated");
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
            SaveOutputPath = DynastyIsSave && _saveOutBox.IsChecked == true ? RecreatedSavePath() : null,
        };

        _generateButton.IsEnabled = false;
        _checkButton.IsEnabled = false;
        SetStatus("Generating… this takes a few seconds for a 16,500-row player table.", ok: null);

        try
        {
            // Progress arrives from the worker thread; SetStatus touches the
            // UI, so it is marshalled back rather than called from there.
            var service = new RosterGenerationService
            {
                Progress = line => Avalonia.Threading.Dispatcher.UIThread.Post(() => SetStatus(line, ok: null)),
            };
            var result = await Task.Run(() => service.Run(request));
            _output.Text = Describe(result);
            SetStatus(
                result.SaveOutput is { } written
                    ? $"Done — {result.Converted} players written, {result.Filled} slots filled. " +
                      $"Load {Path.GetFileName(written.Destination)} in the game."
                    : $"Done — {result.Converted} players written, {result.Filled} slots filled. " +
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
        if (result.Teams.Count > 1)
        {
            text.AppendLine($"Teams converted:   {result.Teams.Count}");
        }

        text.AppendLine($"Players written:   {result.Converted}");
        text.AppendLine($"Players skipped:   {result.Skipped}");
        text.AppendLine($"Slots filled:      {result.Filled}");
        text.AppendLine($"Validation:        0 errors, {result.Export.Report.Warnings.Count()} warnings");
        text.AppendLine();
        if (result.SaveOutput is { } save)
        {
            text.AppendLine();
            text.AppendLine($"Dynasty save:      {Path.GetFullPath(save.Destination)}");
            text.AppendLine($"                   {save.CellsChanged:N0} field(s) written; " +
                            $"{save.EmptyRecordsSkipped:N0} empty roster slot(s) left untouched.");
            text.AppendLine("                   Copy it into your saves folder. Your original is unchanged.");
            text.AppendLine();
        }

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

    private static FbsMembership LoadMembership()
    {
        try
        {
            var path = RosterGenerationService.FindOptionalDataFile(null, "FbsMembership.json");
            return path is null ? FbsMembership.Empty : FbsMembership.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            return FbsMembership.Empty;
        }
    }

    /// <summary>
    /// Says when the chosen school had not reached the FBS in the chosen
    /// season.
    ///
    /// <para>CFB27 carries the 138 teams of today, which is not who was playing
    /// in 1985 — so a 2010 roster for Sacramento State, James Madison or
    /// Liberty builds perfectly and is wrong, with nothing in the game to say
    /// so. This is the only warning the user gets, and it costs them
    /// nothing: it is a note, never a block, because the dates are this
    /// project's reading of the record and the file is theirs to correct.</para>
    /// </summary>
    private void UpdateMembershipNote()
    {
        var school = _teamBox.SelectedItem as string;
        var season = ParseSeason();
        if (school is null || season is not int year || year <= 0)
        {
            _membershipNote.IsVisible = false;
            return;
        }

        // Reason, not Detail: Detail drops the leading school name for callers
        // that have already printed it in a column of their own, and this note
        // is a lone sentence with nothing else naming the team.
        var problem = Membership.Check(school, year);
        _membershipNote.Text = problem is null
            ? ""
            : $"{problem.Reason}. The roster is still generated — correct data\\FbsMembership.json if " +
              "you know better.";
        _membershipNote.IsVisible = problem is not null;
    }

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

        // A greyed-out button with no explanation is the worst thing this
        // window can do: everything looks fine and nothing says why it will
        // not proceed. Whenever Generate is unavailable, this says what is
        // missing — and it stays on screen, unlike the status line.
        _blocker.Text = WhyGenerateIsUnavailable();
        _blocker.IsVisible = _blocker.Text is { Length: > 0 };
    }

    /// <summary>
    /// What is standing between the user and Generate, in their terms, or an
    /// empty string when nothing is.
    /// </summary>
    private string WhyGenerateIsUnavailable()
    {
        if (_dynasty is null)
        {
            return _dynastyProblem is { Length: > 0 } problem
                ? $"Cannot generate: {problem}"
                : "Cannot generate yet — choose your dynasty in step 1.";
        }

        if (string.IsNullOrWhiteSpace(_rosterBox.Text))
        {
            return "Cannot generate yet — choose your roster CSV in step 2.";
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
