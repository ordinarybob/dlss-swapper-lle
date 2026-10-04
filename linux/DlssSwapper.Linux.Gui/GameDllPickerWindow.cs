using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class GameDllPickerWindow : Window
{
    private sealed record Family(DllType Type) { public override string ToString() => DllTypes.Get(Type).DisplayName; }
    private sealed record Release(DllCatalogEntry Entry, bool Current, bool Original)
    { public override string ToString() => DllReleaseDisplay.Name(Entry)
        + (Current ? LanguageAppearance.Get("Linux_InstalledSuffix", " — installed") : "")
        + (Original ? LanguageAppearance.Get("Linux_OriginalSuffix", " — original") : ""); }
    private readonly SelectedGame _game;
    private readonly DllType? _initialFamily;
    private readonly DllCatalog _catalog;
    private readonly DownloadCache _cache;
    private readonly ComboBox _family = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ListBox _releases = new();
    private readonly ComboBox _locations = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _openFolder = new() { Content = LanguageAppearance.Get("Linux_GameDllPickerWindow_84", "Open containing folder") };
    private readonly Button _cancel = new() { Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_7", "Cancel operation"), IsVisible = false };
    private readonly TextBlock _empty = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private CancellationTokenSource? _operation;
    private readonly TextBox _installed = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 120 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _downloaded = new() { Content = LanguageAppearance.Get("Linux_GameDllPickerWindow_83", "Show downloaded releases only") };
    private readonly Button _apply = new() { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Apply") }, _restore = new() { Content = LanguageAppearance.Get("Linux_GameDllPickerWindow_82", "Restore this family") }, _info = new() { Content = LanguageAppearance.Get("Linux_GameDllPickerWindow_81", "DLL information") }, _close = new() { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Close") };
    private ScanResult? _scan;
    private DllRestoreInspection? _originals;
    private bool _busy = true;
    public IReadOnlyList<OperationResult> Results { get; private set; } = [];

    public GameDllPickerWindow(SelectedGame game, DllCatalog catalog, string? cacheRoot = null, bool downloadedOnly = false, DllType? family = null)
    {
        _game = game; _catalog = catalog; _initialFamily = family; Title = LanguageAppearance.Format("Linux_GameDllPickerWindow_80", "DLL versions — {0}", game.Name);
        _cache = new DownloadCache(cacheRoot: cacheRoot);
        _downloaded.IsChecked = downloadedOnly;
        Width = 820; Height = 700; MinWidth = 560; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var grid = new Grid { RowDefinitions = new("Auto,Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 10 };
        grid.Children.Add(_family); Grid.SetRow(_installed, 1); grid.Children.Add(_installed);
        var releaseArea = new Grid(); releaseArea.Children.Add(_releases); releaseArea.Children.Add(_empty);
        Grid.SetRow(releaseArea, 2); grid.Children.Add(releaseArea);
        var footer = new StackPanel { Spacing = 8 }; footer.Children.Add(_downloaded);
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 60 });
        footer.Children.Add(_locations);
        var actions = new WrapPanel(); foreach (var button in new[] { _apply, _restore, _info, _openFolder, _cancel, _close }) { button.Margin = new Thickness(0, 0, 8, 4); actions.Children.Add(button); }
        footer.Children.Add(actions); Grid.SetRow(footer, 3); grid.Children.Add(footer); Content = grid;
        _family.SelectionChanged += (_, _) => Render(); _downloaded.IsCheckedChanged += (_, _) => Render();
        _releases.SelectionChanged += (_, _) => UpdateActions();
        _info.Click += async (_, _) => { if (_releases.SelectedItem is Release release) await new DllRecordDetailsWindow(release.Entry).ShowDialog(this); };
        _apply.Click += async (_, _) => await ApplyAsync(false); _restore.Click += async (_, _) => await ApplyAsync(true);
        _locations.SelectionChanged += (_, _) => UpdateActions();
        _openFolder.Click += (_, _) =>
        {
            try
            {
                if (_locations.SelectedItem is not string path) return;
                var folder = FileLocation.ContainingFolder(path);
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _status.Text = LanguageAppearance.Format("Linux_GameDllPickerWindow_79", "Could not open the folder: {0}", ex.Message); }
        };
        _cancel.Click += (_, _) => CancelOperation();
        _close.Click += (_, _) => { if (!_busy) Close(); else CancelOperation(); };
        Closing += (_, e) => { if (_busy) { e.Cancel = true; CancelOperation(); } }; Closed += (_, _) => _cache.Dispose();
        Opened += async (_, _) => await LoadAsync(); UpdateActions();
    }
    private async Task LoadAsync()
    {
        try
        {
            var oldFamily = (_family.SelectedItem as Family)?.Type ?? _initialFamily;
            _scan = await Task.Run(() => new DllScanner().Scan(_game, _catalog));
            _originals = await Task.Run(() => DllRestoreWorkflow.Inspect(_game, LanguageAppearance.Current));
            var families = _scan.Dlls.Select(dll => dll.Type).Concat(_originals.Files.Select(file => file.Item.Family.Type)).Distinct().Select(type => new Family(type)).ToArray();
            _family.ItemsSource = families; _family.SelectedItem = families.FirstOrDefault(item => item.Type == oldFamily) ?? families.FirstOrDefault();
            _status.Text = string.Join("\n", _scan.Warnings.Concat(_originals.Warnings));
            if (families.Length == 0) _status.Text = LanguageAppearance.Get("Linux_GameDllPickerWindow_78", "No readable DLLs or original backups found.\n") + _status.Text;
            Render();
        }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _status.Text = LanguageAppearance.Format("Linux_GameDllPickerWindow_77", "Could not inspect DLLs: {0}", ex.Message); }
        finally { _busy = false; UpdateActions(); }
    }
    private void Render()
    {
        if (_scan is null || _family.SelectedItem is not Family family) return;
        var currentFiles = _scan.Dlls.Where(dll => dll.Type == family.Type).ToArray();
        var originals = _originals?.Files.Where(file => file.Item.Family.Type == family.Type).ToArray() ?? [];
        _locations.ItemsSource = currentFiles.Select(file => file.Path).Concat(originals.Select(file => file.Item.BackupPath)).Distinct().ToArray();
        _locations.SelectedIndex = 0;
        _installed.Text = string.Join("\n", currentFiles.Select(dll => LanguageAppearance.Format("Linux_InstalledFile", "Installed: {0}\n{1}", dll.Version, dll.Path)))
            + "\n" + string.Join("\n", originals.Select(file => LanguageAppearance.Format("Linux_OriginalFile", "Original: {0}\n{1}", file.OriginalVersion, file.Item.BackupPath)));
        var entries = GameDllChoices.Eligible(_scan, family.Type, _catalog);
        var current = GameDllChoices.Current(_scan, family.Type, entries);
        var releases = entries.Where(entry => _downloaded.IsChecked != true || _cache.IsCached(entry) || currentFiles.Any(file => file.Md5 == entry.Md5))
            .Select(entry => new Release(entry, currentFiles.Any(file => file.Md5 == entry.Md5), originals.Any(file => file.OriginalHash == entry.Md5))).ToArray();
        _releases.ItemsSource = releases;
        _empty.IsVisible = releases.Length == 0;
        _empty.Text = _downloaded.IsChecked == true ? LanguageAppearance.Get("Linux_GameDllPickerWindow_76", "No releases match this filter. Clear downloaded-only to see available versions.")
            : LanguageAppearance.Get("Linux_GameDllPickerWindow_75", "No compatible catalog releases are available for these installed files. Original backups can still be restored when present.");
        _releases.SelectedItem = releases.FirstOrDefault(item => item.Entry == current);
        UpdateActions();
    }
    private void UpdateActions()
    {
        _family.IsEnabled = _releases.IsEnabled = _downloaded.IsEnabled = _close.IsEnabled = !_busy;
        _locations.IsEnabled = !_busy; _openFolder.IsEnabled = !_busy && _locations.SelectedItem is string;
        _cancel.IsVisible = _operation is not null; _cancel.IsEnabled = _operation is { IsCancellationRequested: false };
        _info.IsEnabled = !_busy && _releases.SelectedItem is Release;
        _apply.IsEnabled = !_busy && _releases.SelectedItem is Release release && _scan is not null
            && new UpdatePlanner().Plan([_scan], new Dictionary<DllType, DllCatalogEntry> { [release.Entry.Type] = release.Entry }).Any(item => item.Status == UpdatePlanStatus.Ready);
        _restore.IsEnabled = !_busy && _family.SelectedItem is Family family && _originals?.Files.Any(file => file.Item.Family.Type == family.Type) == true;
    }
    private async Task ApplyAsync(bool restore)
    {
        if (_busy || _scan is null || _family.SelectedItem is not Family family) return;
        var release = _releases.SelectedItem as Release;
        if (!restore && release is null) return;
        _operation = new CancellationTokenSource();
        var token = _operation.Token;
        _busy = true; UpdateActions();
        try
        {
            var originals = _originals!.Files.Where(file => file.Item.Family.Type == family.Type).ToArray();
            var message = restore ? string.Join("\n", originals.Select(file => LanguageAppearance.Format("Linux_RestoreFile", "{0} → original {1}", file.Item.RelativeTargetPath, file.OriginalVersion)))
                : LanguageAppearance.Format("Linux_ApplyDllPrompt", "Apply {0} to every detected {1} file in {2}? Missing downloads are acquired automatically.", DllReleaseDisplay.Name(release!.Entry), family, _game.Name);
            var warnings = _scan.Warnings.Concat(_originals.Warnings).ToArray();
            var warning = ConfirmationDialog.GameFileWriteWarning + (warnings.Length > 0 ? "\n" + LanguageAppearance.Get("Linux_IncompleteUpdate", "Inspection was incomplete. Only listed files are included.") + "\n" + string.Join("\n", warnings) : "");
            if (!await new ConfirmationDialog(restore ? LanguageAppearance.Get("Linux_RestoreOriginalsTitle", "Restore originals?") : LanguageAppearance.Get("Linux_ApplyDllTitle", "Apply DLL version?"), message, warning).ShowDialog<bool>(this)) return;
            token.ThrowIfCancellationRequested();
            IReadOnlyList<OperationResult> results;
            if (restore) results = await Task.Run(() => DllRestoreWorkflow.Apply(originals, token, LanguageAppearance.Current));
            else
            {
                _status.Text = LanguageAppearance.Get("Linux_GameDllPickerWindow_74", "Acquiring the selected DLL and applying it…");
                var plan = await BatchUpdateWorkflow.PrepareAsync([_scan], new Dictionary<DllType, DllCatalogEntry> { [family.Type] = release!.Entry }, [], _ => throw new InvalidOperationException(), token, LanguageAppearance.Current);
                results = await Task.Run(() => BatchUpdateWorkflow.ApplyAsync(plan, _cache.GetAsync, token, translations: LanguageAppearance.Current));
            }
            Results = Results.Concat(results).ToArray(); await LoadAsync();
            _status.Text += "\n" + string.Join("\n", results.Select(result => $"{result.Target}: {result.Message}"));
        }
        catch (OperationCanceledException) { _status.Text = LanguageAppearance.Get("Linux_GameDllPickerWindow_73", "Cancelled before applying changes."); }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _status.Text = LanguageAppearance.Format("Linux_GameDllPickerWindow_72", "Operation failed: {0}", ex.Message); }
        finally { _operation.Dispose(); _operation = null; _busy = false; UpdateActions(); }
    }
    private void CancelOperation()
    {
        _operation?.Cancel(); _cancel.IsEnabled = false;
        _status.Text = _operation is null ? LanguageAppearance.Get("Linux_GameDllPickerWindow_71", "Waiting for inspection to finish.") : LanguageAppearance.Get("Linux_GameDllPickerWindow_70", "Stopping after the current file operation. Completed changes are kept.");
    }
}
