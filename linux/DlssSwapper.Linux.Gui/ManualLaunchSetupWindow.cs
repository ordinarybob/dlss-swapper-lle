using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DLSS_Swapper.Data.ManuallyAdded;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class ManualLaunchSetupWindow : Window
{
    private sealed class Row(ManualGameState game)
    {
        internal ManualGameState Game { get; } = game;
        internal ManualGameLaunch Draft { get; set; } = game.Launch is { } saved
            ? saved with { Arguments = saved.Arguments.ToArray() }
            : new("", "", [], ManualLaunchKind.Native);
        internal List<ManualLaunchManifest.Candidate> Candidates { get; } = [];
        internal ComboBox Choice { get; } = new() { Name = "LaunchChoice", Tag = game.RootPath, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };
        internal string Error { get; set; } = "";
    }

    private readonly PersistentLibrary _library;
    private readonly Row[] _rows;
    private readonly StackPanel _list = new() { Spacing = 8 };
    private readonly TextBlock _status = Text("");
    private readonly Button _apply = new() { Content = "Apply", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
    private readonly Button _saveClose = new() { Content = "Save and close", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
    private readonly CancellationTokenSource _scan = new();
    private bool _closed, _saving, _scanFinished;

    public static async Task OfferAsync(Window owner, PersistentLibrary library, IReadOnlyList<ManualGameState> games)
    {
        if (games.Count == 0) return;
        var setup = library.State.SetupManualLaunchOnImport;
        if (!library.State.DontShowManualLaunchPrompt)
        {
            var prompt = new Window { Title = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_122", "Set up game launching?"), Width = 600, Height = 370, MinWidth = 460, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var grid = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(Text(LanguageAppearance.Get("Linux_ManualLaunchListOffer", "Do you want a manifest for launching these games?\n\nThe app will scan all added games, then show their suggested executables together for review. Suggestions are best-effort and may need correcting. Saving changes only this app's library and does not launch the game.\n\nNo skips setup. Remembering Yes opens setup automatically on future imports.")));
            var remember = new CheckBox { Content = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_120", "Don't show this again") }; content.Children.Add(remember);
            var error = Text("");
            content.Children.Add(error);
            grid.Children.Add(new ScrollViewer { Content = content });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            var yes = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Yes"), MinWidth = 100 }; yes.Classes.Add("primary");
            var no = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_No"), MinWidth = 100 };
            void CompleteOffer(bool proceed)
            {
                try
                {
                    if (remember.IsChecked == true) ManualLaunchSetupWorkflow.RememberOffer(library, proceed);
                    prompt.Close(new ImportNoticeResult(proceed, remember.IsChecked == true));
                }
                catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); error.Text = LanguageAppearance.Format("Linux_ManualLaunchSetupWindow_119", "Could not save your preference: {0}. Try again, or clear Don't show this again to continue without saving it.", ex.Message); }
            }
            yes.Click += (_, _) => CompleteOffer(true);
            no.Click += (_, _) => CompleteOffer(false);
            actions.Children.Add(yes); actions.Children.Add(no); Grid.SetRow(actions, 1); grid.Children.Add(actions); prompt.Content = grid;
            var result = await prompt.ShowDialog<ImportNoticeResult?>(owner);
            if (result is null) return;
            setup = result.Proceed;
        }
        if (setup) await ConfigureAsync(owner, library, games);
    }

    public static async Task ConfigureAsync(Window owner, PersistentLibrary library, IReadOnlyList<ManualGameState> games)
    {
        if (games.Count > 0) await new ManualLaunchSetupWindow(library, games).ShowDialog(owner);
    }

    private ManualLaunchSetupWindow(PersistentLibrary library, IReadOnlyList<ManualGameState> games)
    {
        _library = library;
        _rows = games.Select(game => new Row(game)).ToArray();
        Title = $"Game launch setup ({games.Count} games)";
        Width = 720; Height = 700; MinWidth = 620; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
        layout.Children.Add(Text("Confirm the launch executable for each game. Best-effort suggestion selected. Verify it is the correct game executable before saving"));
        var headings = MakeRow();
        headings.Children.Add(Text("Game"));
        var exe = Text("Launch executable"); Grid.SetColumn(exe, 1); headings.Children.Add(exe);
        Grid.SetRow(headings, 1); layout.Children.Add(headings);
        foreach (var row in _rows) AddRow(row);
        var scroll = new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 10 };
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 64 });
        var actions = new Grid { ColumnDefinitions = new("*,*,*"), ColumnSpacing = 8 };
        _apply.Classes.Add("primary");
        var skip = new Button { Content = "Skip and close", HorizontalAlignment = HorizontalAlignment.Stretch };
        _apply.Click += (_, _) => Save(close: false);
        _saveClose.Click += (_, _) => Save(close: true);
        skip.Click += (_, _) => { if (!_saving) Close(); };
        actions.Children.Add(_apply); Grid.SetColumn(_saveClose, 1); actions.Children.Add(_saveClose);
        Grid.SetColumn(skip, 2); actions.Children.Add(skip); footer.Children.Add(actions);
        Grid.SetRow(footer, 3); layout.Children.Add(footer); Content = layout;
        Closing += (_, e) => e.Cancel = _saving;
        Closed += (_, _) => { _closed = true; if (!_scanFinished) _scan.Cancel(); };
        Opened += async (_, _) => await PrepareAsync();
    }

    private static Grid MakeRow() => new() { ColumnDefinitions = new("2*,3*,90"), ColumnSpacing = 12 };

    private void AddRow(Row row)
    {
        var grid = MakeRow();
        var name = new TextBlock { Text = row.Game.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(name, row.Game.Name);
        row.Choice.PlaceholderText = "Scanning…";
        row.Choice.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        row.Choice.SelectionBoxItemTemplate = new FuncDataTemplate<ManualLaunchManifest.Candidate>((item, _) =>
            new TextBlock { Text = item?.FileName, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.LeftToRight });
        row.Choice.ItemTemplate = new FuncDataTemplate<ManualLaunchManifest.Candidate>((item, _) =>
        {
            var path = new TextBlock { Text = item?.Path, TextWrapping = TextWrapping.Wrap,
                MaxWidth = Math.Clamp(ClientSize.Width - 80, 200, 640), FlowDirection = FlowDirection.LeftToRight };
            ToolTip.SetTip(path, item?.Path);
            return path;
        });
        row.Choice.SelectionChanged += (_, _) =>
        {
            if (row.Choice.SelectedItem is not ManualLaunchManifest.Candidate selected) return;
            row.Draft = row.Draft with { Executable = selected.Path,
                Kind = selected.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? ManualLaunchKind.Wine : ManualLaunchKind.Native };
            row.Error = "";
            row.Choice.ClearValue(BorderBrushProperty);
            ToolTip.SetTip(row.Choice, selected.Path);
        };
        var browse = new Button { Content = "Browse", Name = "BrowseExecutable", Tag = row.Game.RootPath, HorizontalAlignment = HorizontalAlignment.Stretch };
        browse.Click += async (_, _) =>
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose the game executable", AllowMultiple = false });
                if (!_closed && files.Count > 0)
                {
                    var path = files[0].Path.LocalPath;
                    if (ManualLaunchManifest.IsExcluded(path)) throw new IOException("That executable is excluded from game launching.");
                    SelectPath(row, path);
                }
            }
            catch (Exception ex) { row.Error = ex.Message; ShowErrors(); }
        };
        var options = new MenuItem { Header = "Launch options…", Name = "LaunchOptions" };
        options.Click += async (_, _) =>
        {
            var draft = await new ManualLaunchOptionsWindow(row.Game.Name, row.Draft).ShowDialog<ManualGameLaunch?>(this);
            if (draft is not null) row.Draft = draft;
        };
        grid.ContextMenu = new ContextMenu { ItemsSource = new[] { options } };
        ToolTip.SetTip(grid, "Right-click for launch arguments, working folder and Wine settings.");
        grid.Children.Add(name); Grid.SetColumn(row.Choice, 1); grid.Children.Add(row.Choice);
        Grid.SetColumn(browse, 2); grid.Children.Add(browse);
        _list.Children.Add(grid);
    }

    private static void SelectPath(Row row, string path)
    {
        var selected = row.Candidates.FirstOrDefault(item => item.Path == path);
        if (selected is null && !string.IsNullOrWhiteSpace(path))
        {
            selected = new(path, Path.GetFileName(path));
            row.Candidates.Add(selected);
        }
        row.Choice.ItemsSource = row.Candidates.ToArray();
        row.Choice.SelectedItem = selected;
        row.Choice.PlaceholderText = "Not found — Browse";
        ToolTip.SetTip(row.Choice, row.Error.Length > 0 ? row.Error : path);
    }

    private async Task PrepareAsync()
    {
        _list.IsEnabled = false;
        var token = _scan.Token;
        try
        {
            var patterns = _library.State.CustomScanPatterns.ToArray();
            var results = new (List<ManualLaunchManifest.Candidate> Candidates, string Error)[_rows.Length];
            var completed = 0;
            var progress = new Progress<int>(count => { if (!_closed && !_scanFinished) _status.Text = $"Scanning game executables: {count}/{_rows.Length}"; });
            var wine = await Task.Run(() => LaunchSuggestions.FindWine(Environment.GetEnvironmentVariable("PATH")));
            await Parallel.ForEachAsync(Enumerable.Range(0, _rows.Length), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token },
                async (index, cancellation) =>
                {
                    try
                    {
                        var game = _rows[index].Game;
                        var candidates = await Task.Run(() => LaunchSuggestions.Find(game.RootPath, game.Name, patterns), cancellation);
                        results[index] = (candidates.ToList(), "");
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception ex) { results[index] = ([], ex.Message); }
                    ((IProgress<int>)progress).Report(Interlocked.Increment(ref completed));
                });
            if (_closed) return;
            for (var index = 0; index < _rows.Length; index++)
            {
                var row = _rows[index];
                row.Candidates.AddRange(results[index].Candidates);
                row.Error = results[index].Error;
                if (string.IsNullOrWhiteSpace(row.Draft.Runner)) row.Draft = row.Draft with { Runner = wine };
                var path = row.Draft.Executable;
                if (string.IsNullOrWhiteSpace(path) || ManualLaunchManifest.IsExcluded(path))
                    path = row.Candidates.FirstOrDefault()?.Path ?? "";
                SelectPath(row, path);
            }
            _status.Text = "";
            _list.IsEnabled = _apply.IsEnabled = _saveClose.IsEnabled = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_closed) { _status.Text = ex.Message; _list.IsEnabled = _apply.IsEnabled = _saveClose.IsEnabled = true; }
        }
        finally { _scanFinished = true; _scan.Dispose(); }
    }

    private void Save(bool close)
    {
        if (_saving || !_apply.IsEnabled) return;
        _saving = true;
        try
        {
            foreach (var row in _rows)
            {
                if (close && string.IsNullOrWhiteSpace(row.Draft.Executable))
                {
                    row.Error = "";
                    continue;
                }
                try
                {
                    var validated = row.Draft.Validate();
                    var current = _library.State.ManualGames.Single(game => game.RootPath == row.Game.RootPath).Launch;
                    if (current is null || current.Executable != validated.Executable || current.WorkingDirectory != validated.WorkingDirectory
                        || !current.Arguments.SequenceEqual(validated.Arguments) || current.Kind != validated.Kind
                        || current.Runner != validated.Runner || current.WinePrefix != validated.WinePrefix)
                        ManualLaunchSetupWorkflow.Save(_library, row.Game.RootPath, validated);
                    row.Draft = validated;
                    row.Error = "";
                }
                catch (Exception ex) { row.Error = ex.Message; }
            }
            ShowErrors();
        }
        finally { _saving = false; }
        if (_rows.All(row => row.Error.Length == 0))
        {
            _status.Text = "Launch settings saved.";
            if (close) Close();
        }
    }

    private void ShowErrors()
    {
        _status.Text = string.Join("\n", _rows.Where(row => row.Error.Length > 0).Select(row => $"{row.Game.Name}: {row.Error}"));
        foreach (var row in _rows)
        {
            if (row.Error.Length > 0) row.Choice.BorderBrush = Brushes.IndianRed;
            else row.Choice.ClearValue(BorderBrushProperty);
            ToolTip.SetTip(row.Choice, row.Error.Length > 0 ? row.Error : row.Draft.Executable);
        }
    }

    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
