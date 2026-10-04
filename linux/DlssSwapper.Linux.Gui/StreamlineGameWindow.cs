using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    private readonly ProgressBar _downloadProgress = new() { Name = "SdkDownloadProgress", MinWidth = 0, Height = 4, IsVisible = false, Maximum = 100 };
    private readonly CheckBox _all = new() { Content = LanguageAppearance.Get("Linux_StreamlineGameWindow_202", "Select all components"), IsThreeState = true };
    private readonly List<Button> _buttons = [];
    private readonly Button _applySelected, _applyAll, _restoreSelected, _restoreAll;
    private StreamlinePreviewSnapshot? _preview;
    private string? _package;
    private string? _latest;
    private readonly ComboBox _versions = new() { PlaceholderText = "Streamline SDK version", HorizontalAlignment = HorizontalAlignment.Stretch };
    private StreamlineSdkRelease? _selectedRelease;
    private bool _loadingVersions;
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
        header.Children.Add(_versions);
        _versions.SelectionChanged += async (_, _) =>
        {
            if (_busy || _loadingVersions || _closed || _versions.SelectedItem is not StreamlineSdkRelease release) return;
            _selectedRelease = release; _package = _sdk.FindCached(release.Tag);
            _busy = true; UpdateActions();
            try { await RefreshAsync(); }
            catch (Exception error) { _status.Text = error.Message; }
            finally { _busy = false; if (!_closed) UpdateActions(); }
        };
        header.Children.Add(_all);
        var columns = new Grid { ColumnDefinitions = new("1.6*,*,*,*"), ColumnSpacing = 10, Margin = new Thickness(46, 0, 16, 0) };
        foreach (var (label, index) in new[] { ("Component (hover for details)", 0), ("Installed", 1), ("Available", 2), ("Original", 3) })
        {
            var title = Text(label); title.FontWeight = FontWeight.SemiBold;
            Grid.SetColumn(title, index); columns.Children.Add(title);
        }
        header.Children.Add(columns);
        layout.Children.Add(header);
        var scroll = new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8 };
        footer.Children.Add(_downloadProgress);
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 100 });
        var packageActions = new WrapPanel();
        packageActions.Children.Add(Action("Download selected package", async () => { var p = await PrepareSelectedAsync(); _package = p.DirectoryPath; _status.Text = p.WasDownloaded ? $"Downloaded SDK {p.Tag}. No game files changed." : "No new files downloaded."; await RefreshAsync(); }));
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
        Opened += async (_, _) => { _startup = LoadAsync(); await _startup; };
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
        try { await CheckVersionAsync(); if (_closed) return; _package = _selectedRelease is null ? null : await Task.Run(() => _sdk.FindCached(_selectedRelease.Tag)); await RefreshAsync(); }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _status.Text = error.Message; }
        finally { _busy = false; UpdateActions(); }
    }
    private async Task CheckVersionAsync()
    {
        _loadingVersions = true;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var releases = await _sdk.FetchReleasesAsync(timeout.Token);
            if (_closed) return;
            _latest = releases.FirstOrDefault()?.Tag;
            _versions.ItemsSource = releases.Where(release => _library?.State.OnlyShowDownloadedDlls != true || _sdk.FindCached(release.Tag) is not null).ToArray();
            _versions.SelectedIndex = 0; _selectedRelease = _versions.SelectedItem as StreamlineSdkRelease;
            UpdatePackageText();
            if (!_busy) RenderRows();
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); if (!_closed) { _versions.ItemsSource = _sdk.CachedReleases(); _versions.SelectedIndex = 0; _selectedRelease = _versions.SelectedItem as StreamlineSdkRelease; _latestError = error.Message; UpdatePackageText(); } }
        finally { _loadingVersions = false; }
    }
    private async Task<StreamlineSdkPackage> PrepareSelectedAsync()
    {
        var release = _selectedRelease ?? throw new InvalidOperationException("Select an SDK version first.");
        if (_library?.State.OnlyShowDownloadedDlls == true && _sdk.FindCached(release.Tag) is null)
            throw new InvalidOperationException("The selected SDK is not downloaded. Download it from Library first.");
        var token = OperationToken;
        var active = 1;
        long lastStep = -1;
        _status.Text = LanguageAppearance.Format("Linux_SdkPreparing", "Preparing Streamline SDK {0}…", release.Tag);
        _downloadProgress.Value = 0;
        _downloadProgress.IsIndeterminate = true;
        _downloadProgress.IsVisible = true;
        void Post(Action update) => Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref active) != 0 && !_closed && !token.IsCancellationRequested) update();
        });
        try
        {
            return await _sdk.PrepareAsync(release, token, (received, total) =>
            {
                var step = total is > 0 ? (long)(received * 100.0 / total.Value) : received / 1_048_576;
                if (step == lastStep) return;
                lastStep = step;
                Post(() =>
                {
                    _downloadProgress.IsIndeterminate = total is not > 0;
                    _downloadProgress.Value = total is > 0 ? Math.Clamp(received * 100.0 / total.Value, 0, 100) : 0;
                    var amount = total is > 0
                        ? LanguageAppearance.Format("Linux_LibraryTransferKnown", "{0:N0} / {1:N0} bytes ({2}%)", received, total.Value, step)
                        : LanguageAppearance.Format("Linux_LibraryTransferUnknown", "{0:N0} bytes (total size unknown)", received);
                    _status.Text = LanguageAppearance.Format("Linux_SdkDownloading", "Downloading Streamline SDK {0}: {1}", release.Tag, amount);
                });
            }, () => Post(() =>
            {
                _downloadProgress.IsIndeterminate = true;
                _status.Text = LanguageAppearance.Get("Linux_SdkExtracting", "Extracting and checking Streamline SDK…");
            }));
        }
        finally
        {
            Interlocked.Exchange(ref active, 0);
            _downloadProgress.IsVisible = false;
        }
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
            var grid = new Grid { ColumnDefinitions = new("36,1.6*,*,*,*"), RowDefinitions = new("Auto,Auto,Auto"), ColumnSpacing = 10, RowSpacing = 4, Margin = new Thickness(0, 4, 16, 8) };
            Grid.SetRowSpan(box, 3);
            grid.Children.Add(box);
            var filename = Text(row.FileName, 16); filename.FontWeight = FontWeight.SemiBold;
            ToolTip.SetTip(filename, StreamlineDisplay.Description(row.FileName));
            Grid.SetColumn(filename, 1); grid.Children.Add(filename);
            var available = _package is not null ? StreamlineDisplay.Text(row.PackageVersion) : _selectedRelease is not null ? $"{_selectedRelease.Tag} SDK"
                : _latestError is null ? LanguageAppearance.Get("Linux_VersionPending", "Version lookup pending")
                : LanguageAppearance.Get("Linux_VersionUnavailable", "Version unavailable");
            foreach (var (value, column) in new[] { (StreamlineDisplay.Text(row.InstalledVersion), 2), (available, 3), (StreamlineDisplay.Text(row.OriginalVersion), 4) })
            {
                var version = Text(value); Grid.SetColumn(version, column); grid.Children.Add(version);
            }
            var directory = string.IsNullOrEmpty(Path.GetDirectoryName(Path.GetRelativePath(_root, row.TargetPath)))
                ? StreamlineDisplay.Text("(game folder)") : row.RelativeDirectory;
            var restoreText = Text(LanguageAppearance.Format("Linux_StreamlineGameWindow_185", "Restore: {0}\n{1}", StreamlineDisplay.Text(row.RestoreText), directory), 12);
            Grid.SetRow(restoreText, 1); Grid.SetColumn(restoreText, 1); Grid.SetColumnSpan(restoreText, 4); grid.Children.Add(restoreText);
            if (_package is not null)
            {
                var update = Text(StreamlineDisplay.Text(row.UpdateText), 12);
                Grid.SetRow(update, 2); Grid.SetColumn(update, 1); Grid.SetColumnSpan(update, 4); grid.Children.Add(update);
            }
            _rows.Children.Add(grid);
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
        _versions.IsEnabled = !_busy && !_loadingVersions;
        _rows.IsEnabled = !_busy; _all.IsEnabled = !_busy && _preview?.Components.Count > 0;
        _refreshing = true;
        _all.IsChecked = _selected.Count == 0 ? false : _selected.Count == _preview?.Components.Count ? true : null;
        _refreshing = false;
        if (_applySelected is null) return;
        _applySelected.IsEnabled = !_busy && (_selectedRelease is not null || _package is not null) && _selected.Count > 0 && (_package is null || _preview?.SelectTargets(_selected).CanUpdate == true);
        _applyAll.IsEnabled = !_busy && (_selectedRelease is not null || _package is not null) && _preview?.Components.Count > 0 && (_package is null || _preview.CanUpdate);
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
            var package = await PrepareSelectedAsync();
            _package = package.DirectoryPath;
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
