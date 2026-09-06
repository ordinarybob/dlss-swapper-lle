using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DLSS_Swapper.Data.Streamline;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class StreamlineGameWindow : Window
{
    private readonly string _root;
    private readonly PersistentLibrary? _library;
    private readonly HttpClient _http;
    private readonly StreamlineLibraryService _sdk;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private Task _startup = Task.CompletedTask;
    private bool _closed;
    private readonly Button _cancel = new() { Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_7", "Cancel operation"), IsVisible = false };
    private readonly HashSet<string> _selected = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly StackPanel _rows = new() { Spacing = 12 };
    private readonly TextBlock _packageText = Text(LanguageAppearance.Get("Linux_BatchUpdateWindow_13", "Checking latest SDK version…"));
    private readonly TextBlock _status = Text("");
    private readonly CheckBox _all = new() { Content = LanguageAppearance.Get("Linux_StreamlineGameWindow_202", "Select all components"), IsThreeState = true };
    private readonly List<Button> _buttons = [];
    private readonly Button _applySelected, _applyAll, _restoreSelected, _restoreAll;
    private StreamlinePreviewSnapshot? _preview;
    private string? _package;
    private string? _latest;
    private string? _latestError;
    private bool _busy = true, _refreshing;

    public StreamlineGameWindow(string root, string name, PersistentLibrary? library, HttpMessageHandler? httpHandler = null, string? cacheRoot = null)
    {
        _root = StreamlineWorkflow.ValidateGameRoot(root);
        _library = library;
        _http = new HttpClient(httpHandler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DLSS-Swapper-LLE-Linux");
        _sdk = new(_http, cacheRoot);
        Title = LanguageAppearance.Format("Linux_StreamlineGameWindow_201", "Streamline components (experimental) — {0}", name);
        Width = 960; Height = 760; MinWidth = 740; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(Text(Title, 21));
        header.Children.Add(Text(LanguageAppearance.Get("Linux_StreamlineGameWindow_200", "Only existing components are replaced. First originals are kept for restoration. SDK release numbers and DLL versions may differ; game compatibility is not verified.")));
        header.Children.Add(new ScrollViewer { Content = _packageText, MaxHeight = 60,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        header.Children.Add(_all);
        layout.Children.Add(header);
        var scroll = new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8 };
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 100 });
        var packageActions = new WrapPanel();
        packageActions.Children.Add(Action(LanguageAppearance.Get("Linux_StreamlineGameWindow_199", "Get latest package"), async () => { var p = await _sdk.PrepareLatestAsync(OperationToken); _package = p.DirectoryPath; _latest = p.Tag; _status.Text = p.WasDownloaded ? LanguageAppearance.Format("Linux_StreamlineGameWindow_198", "Downloaded SDK {0}. No game files changed.", p.Tag) : LanguageAppearance.Get("Linux_StreamlineGameWindow_197", "No new files downloaded."); await RefreshAsync(); }));
        packageActions.Children.Add(Action(LanguageAppearance.Get("Linux_StreamlineGameWindow_196", "Use local package"), ChooseLocalAsync));
        _restoreAll = Action(LanguageAppearance.Get("Linux_StreamlineGameWindow_195", "Restore all originals"), () => ApplyAsync(true, true));
        packageActions.Children.Add(_restoreAll);
        footer.Children.Add(packageActions);
        var restoration = new WrapPanel();
        _restoreSelected = Action(LanguageAppearance.Get("Linux_StreamlineGameWindow_194", "Restore selected"), () => ApplyAsync(true, false));
        restoration.Children.Add(_restoreSelected);
        restoration.Children.Add(Action(LanguageAppearance.Get("Linux_StreamlineGameWindow_193", "Recover interrupted operation"), RecoverAsync));
        footer.Children.Add(restoration);
        var actions = new WrapPanel();
        _applySelected = Action(LanguageAppearance.Get("Linux_StreamlineGameWindow_192", "Apply selected"), () => ApplyAsync(false, false));
        _applyAll = Action(LanguageAppearance.Get("Linux_StreamlineGameWindow_191", "Apply all"), () => ApplyAsync(false, true));
        actions.Children.Add(_applySelected); actions.Children.Add(_applyAll);
        actions.Children.Add(_cancel);
        _cancel.Click += (_, _) => RequestCancellation();
        actions.Children.Add(Action(LanguageAppearance.Get("General_Close", "Close"), () => { Close(); return Task.CompletedTask; }, runBusy: false));
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;
        _all.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing || _busy || _preview is null) return;
            if (_selected.Count != _preview.Components.Count) foreach (var row in _preview.Components) _selected.Add(row.TargetPath);
            else _selected.Clear();
            RenderRows();
        };
        Opened += async (_, _) => { _startup = Task.WhenAll(CheckVersionAsync(), LoadAsync()); await _startup; };
        Closing += (_, e) => { if (_busy) { e.Cancel = true; RequestCancellation(); } };
        Closed += async (_, _) => { _closed = true; _lifetime.Cancel(); await _startup; _http.Dispose(); _lifetime.Dispose(); };
        UpdateActions();
    }

    private static TextBlock Text(string? text, double size = 14) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
    private Button Action(string title, Func<Task> action, bool runBusy = true)
    {
        var button = new Button { Content = title, Margin = new Thickness(0, 0, 8, 4) };
        _buttons.Add(button);
        button.Click += async (_, _) =>
        {
            if (_busy) return;
            if (runBusy) { _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _busy = true; UpdateActions(); }
            try { await action(); }
            catch (OperationCanceledException) { _status.Text = LanguageAppearance.Get("Linux_StreamlineGameWindow_190", "Cancelled before changing game files. Any downloaded package remains cached."); }
            catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _status.Text = error.Message; }
            finally { _operation?.Dispose(); _operation = null; _busy = false; if (!_closed) UpdateActions(); }
        };
        return button;
    }
    private async Task LoadAsync()
    {
        try { _package = await Task.Run(_sdk.FindNewestCached); await RefreshAsync(); }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _status.Text = error.Message; }
        finally { _busy = false; UpdateActions(); }
    }
    private async Task CheckVersionAsync()
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            _latest = (await _sdk.FetchLatestAsync(timeout.Token)).Tag;
            if (_closed) return;
            UpdatePackageText();
            if (!_busy) RenderRows();
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); if (!_closed) { _latestError = error.Message; UpdatePackageText(); } }
    }
    private async Task RefreshAsync()
    {
        _preview = await Task.Run(() => StreamlineWorkflow.Preview(_root, _package));
        _selected.IntersectWith(_preview.Components.Select(row => row.TargetPath));
        UpdatePackageText();
        RenderRows();
    }
    private void UpdatePackageText() => _packageText.Text =
        LanguageAppearance.Format("Linux_StreamlineGameWindow_189", "Latest SDK: {0}", _latest ?? (_latestError is null
            ? LanguageAppearance.Get("Linux_VersionChecking", "checking…")
            : LanguageAppearance.Format("Linux_VersionError", "unavailable — {0}", _latestError)))
        + (_package is null ? "" : LanguageAppearance.Format("Linux_StreamlineGameWindow_188", " • Selected package: {0}", Path.GetFileName(_package)));
    private void RenderRows()
    {
        if (_preview is null) return;
        _refreshing = true;
        _rows.Children.Clear();
        if (_preview.Components.Count == 0) _rows.Children.Add(Text(LanguageAppearance.Get("Linux_StreamlineGameWindow_187", "No Streamline components found.")));
        foreach (var row in _preview.Components)
        {
            var box = new CheckBox { IsChecked = _selected.Contains(row.TargetPath), VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(box, row.FileName);
            var grid = new Grid { ColumnDefinitions = new("36,*"), ColumnSpacing = 10 };
            grid.Children.Add(box);
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(Text(row.FileName, 16));
            content.Children.Add(Text(StreamlineDisplay.Description(row.FileName)));
            var available = _package is not null ? StreamlineDisplay.Text(row.PackageVersion) : _latest is not null ? $"{_latest} SDK"
                : _latestError is null ? LanguageAppearance.Get("Linux_VersionPending", "Version lookup pending")
                : LanguageAppearance.Get("Linux_VersionUnavailable", "Version unavailable");
            content.Children.Add(Text(LanguageAppearance.Format("Linux_StreamlineGameWindow_186", "Installed: {0}    Available: {1}    Original: {2}", StreamlineDisplay.Text(row.InstalledVersion), available, StreamlineDisplay.Text(row.OriginalVersion))));
            if (_package is not null) content.Children.Add(Text(StreamlineDisplay.Text(row.UpdateText)));
            var directory = string.IsNullOrEmpty(Path.GetDirectoryName(Path.GetRelativePath(_root, row.TargetPath)))
                ? StreamlineDisplay.Text("(game folder)") : row.RelativeDirectory;
            content.Children.Add(Text(LanguageAppearance.Format("Linux_StreamlineGameWindow_185", "Restore: {0}\n{1}", StreamlineDisplay.Text(row.RestoreText), directory)));
            Grid.SetColumn(content, 1); grid.Children.Add(content); _rows.Children.Add(grid);
            box.IsCheckedChanged += (_, _) => { if (box.IsChecked == true) _selected.Add(row.TargetPath); else _selected.Remove(row.TargetPath); UpdateActions(); };
        }
        _refreshing = false;
        UpdateActions();
    }
    private void UpdateActions()
    {
        _cancel.IsVisible = _operation is not null;
        _cancel.IsEnabled = _operation is { IsCancellationRequested: false };
        foreach (var button in _buttons) button.IsEnabled = !_busy;
        _rows.IsEnabled = !_busy; _all.IsEnabled = !_busy && _preview?.Components.Count > 0;
        _refreshing = true;
        _all.IsChecked = _selected.Count == 0 ? false : _selected.Count == _preview?.Components.Count ? true : null;
        _refreshing = false;
        if (_applySelected is null) return;
        _applySelected.IsEnabled = !_busy && _selected.Count > 0 && (_package is null || _preview?.SelectTargets(_selected).CanUpdate == true);
        _applyAll.IsEnabled = !_busy && _preview?.Components.Count > 0 && (_package is null || _preview.CanUpdate);
        _restoreAll.IsEnabled = !_busy && _preview?.CanRestore == true;
        _restoreSelected.IsEnabled = !_busy && _preview?.SelectTargets(_selected).CanRestore == true;
    }
    private async Task ApplyAsync(bool restore, bool all)
    {
        if (_preview is null) return;
        var paths = all ? _preview.Components.Select(row => row.TargetPath).ToArray() : _selected.ToArray();
        if (paths.Length == 0) return;
        if (!restore && _package is null)
        {
            _status.Text = LanguageAppearance.Get("Linux_StreamlineGameWindow_184", "Downloading SDK; game files have not changed yet…");
            var package = await _sdk.PrepareLatestAsync(OperationToken);
            _package = package.DirectoryPath; _latest = package.Tag;
        }
        await RefreshAsync();
        OperationToken.ThrowIfCancellationRequested();
        var confirmed = _preview!.SelectTargets(paths);
        if (restore ? !confirmed.CanRestore : !confirmed.CanUpdate) { _status.Text = LanguageAppearance.Get("Linux_StreamlineGameWindow_183", "No changes available for these components, or a required file is unavailable."); return; }
        var partial = paths.Length < _preview.Components.Count;
        var warning = ConfirmationDialog.GameFileWriteWarning + (partial ? " " + LanguageAppearance.Get("Linux_StreamlinePartialWarning", "Updating or restoring only part of the set may mix Streamline versions. This is not recommended and can break the game.") : "");
        if (!await new ConfirmationDialog(restore ? LanguageAppearance.Get("Linux_StreamlineRestoreTitle", "Restore components?") : LanguageAppearance.Get("Linux_StreamlineApplyTitle", "Apply components?"),
            string.Join('\n', confirmed.Components.Select(row => $"{row.FileName}: {row.InstalledVersion} → {(restore ? row.OriginalVersion : row.PackageVersion)}")), warning).ShowDialog<bool>(this)) return;
        OperationToken.ThrowIfCancellationRequested();
        var result = await Task.Run(() => StreamlineWorkflow.ApplySelection(_root, _package, confirmed, restore, LanguageAppearance.Current));
        Record(result, restore ? "restore" : "update");
        await RefreshAsync();
    }
    private async Task ChooseLocalAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = LanguageAppearance.Get("Linux_StreamlineGameWindow_182", "Select Streamline production package"), AllowMultiple = false });
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        await Task.Run(() => StreamlineComponentSet.ValidatePackage(path));
        _package = path; await RefreshAsync();
    }
    private async Task RecoverAsync()
    {
        if (!await new ConfirmationDialog(LanguageAppearance.Get("Linux_StreamlineRecoverTitle", "Recover Streamline?"), LanguageAppearance.Get("Linux_StreamlineRecoverPrompt", "Recover the interrupted operation from retained recovery files."), ConfirmationDialog.GameFileWriteWarning).ShowDialog<bool>(this)) return;
        OperationToken.ThrowIfCancellationRequested();
        Record(await Task.Run(() => StreamlineWorkflow.Execute(_root, "recover", translations: LanguageAppearance.Current)), "recover");
        await RefreshAsync();
    }
    private void Record(StreamlineComponentOperationResult result, string action)
    {
        _status.Text = result.Message;
        if (!result.Success || result.ComponentCount <= 0) return;
        try { _library?.RecordHistory(_root, $"Streamline {action}", "Streamline", detail: result.Message); }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _status.Text += LanguageAppearance.Format("Linux_StreamlineGameWindow_181", "\nFiles changed successfully, but history could not be saved: {0}", error.Message); }
    }

    private CancellationToken OperationToken => _operation?.Token ?? _lifetime.Token;
    private void RequestCancellation()
    {
        _operation?.Cancel();
        _cancel.IsEnabled = false;
        _status.Text = _operation is null ? LanguageAppearance.Get("Linux_GameDllPickerWindow_71", "Waiting for inspection to finish.") : LanguageAppearance.Get("Linux_StreamlineGameWindow_180", "Stopping after the current operation. A file replacement already in progress will finish safely.");
    }
}
