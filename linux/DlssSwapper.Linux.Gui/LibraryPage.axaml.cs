using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public sealed partial class LibraryPage : UserControl
{
    private readonly ListBox _familyComboBox;
    private readonly TextBox _searchTextBox;
    private readonly ListBox _entryListBox;
    private readonly TextBlock _statusText;
    private readonly Button _downloadLatestButton;
    private readonly DownloadCache _cache;
    private readonly CancellationTokenSource _lifetime = new();
    private DllCatalog? _catalog;
    private DllLibraryEntryViewModel[] _allEntries = [];
    private bool _isBusy;
    private bool _closeRequested;
    private Task _versionCheck = Task.CompletedTask;
    private PersistentLibrary? _library;
    private readonly Button _importButton;
    private readonly Button _exportAllButton;
    private readonly Button _refreshButton;
    public DllCatalog? CurrentCatalog => _catalog;
    private readonly HttpClient _sdkHttp;
    private readonly StreamlineLibraryService _sdk;
    private readonly TextBlock _sdkStatus;
    private readonly Button _sdkDownloadButton;

    public LibraryPage() : this((DownloadCache?)null, (HttpClient?)null) { }

    private LibraryPage(DownloadCache? cache, HttpClient? sdkHttp, string? sdkCacheRoot = null)
    {
        _cache = cache ?? new DownloadCache();
        _sdkHttp = sdkHttp ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        AvaloniaXamlLoader.Load(this);
        _sdkHttp.DefaultRequestHeaders.UserAgent.ParseAdd("DLSS-Swapper-LLE-Linux");
        _sdk = new StreamlineLibraryService(_sdkHttp, sdkCacheRoot);
        _sdkStatus = FindRequired<TextBlock>("StreamlineStatusText");
        _sdkDownloadButton = FindRequired<Button>("DownloadStreamlineButton");
        _familyComboBox = FindRequired<ListBox>("FamilyComboBox");
        _searchTextBox = FindRequired<TextBox>("SearchTextBox");
        _entryListBox = FindRequired<ListBox>("EntryListBox");
        _statusText = FindRequired<TextBlock>("StatusText");
        _downloadLatestButton = FindRequired<Button>("DownloadLatestButton");
        _importButton = FindRequired<Button>("ImportButton");
        _exportAllButton = FindRequired<Button>("ExportAllButton");
        _refreshButton = FindRequired<Button>("RefreshButton");
        UpdateActionState();
        LanguageAppearance.Changed += RefreshLanguage;
    }

    public LibraryPage(DllCatalog catalog, PersistentLibrary? library = null, DownloadCache? cache = null, HttpClient? sdkHttp = null, string? sdkCacheRoot = null)
        : this(cache, sdkHttp, sdkCacheRoot)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _library = library;
        _familyComboBox.ItemsSource = DllTypes.All
            .Select(definition => definition.DisplayName)
            .Prepend(LanguageAppearance.Get("Linux_LibraryAllFamilies", "All families"))
            .Append("Streamline SDK")
            .ToArray();
        _familyComboBox.SelectedIndex = 1;
        _allEntries = catalog.GetEntries()
            .Select(entry => new DllLibraryEntryViewModel(entry, _cache.IsCached(entry)))
            .ToArray();
        ApplyFilter();
        _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_54", "{0:N0} release build{1} available under your current settings.", _allEntries.Length, Plural(_allEntries.Length));
        UpdateActionState();
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || _catalog is null || _library is null) return;
        SetBusy(true);
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LanguageAppearance.Get("Linux_DllLibraryWindow_53", "Import DLLs or ZIPs into the Library — no game files will change"),
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType(LanguageAppearance.Get("Linux_FileTypeDllZip", "DLL or ZIP")) { Patterns = ["*.dll", "*.DLL", "*.zip", "*.ZIP"] }],
            });
            if (_closeRequested || files.Count == 0) return;
            var paths = files.Select(file => file.TryGetLocalPath()
                ?? throw new IOException(LanguageAppearance.Get("Linux_LibraryImportLocal", "Import requires a local file."))).ToArray();
            _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_52", "Checking and importing files into the Library. No game files will change.");
            var workflow = new DllImportWorkflow(_catalog, _cache, _library, translations: LanguageAppearance.Current);
            var results = await DllImportSources.ReadAsync(paths, workflow.ImportAsync, _lifetime.Token, LanguageAppearance.Current);
            _allEntries = _catalog.GetEntries().Select(entry => new DllLibraryEntryViewModel(entry, _cache.IsCached(entry))).ToArray();
            ApplyFilter();
            _statusText.Text = DllImportSources.Describe(results, LanguageAppearance.Current);
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_51", "Import stopped: {0}. No game files were changed.", error.Message); }
        finally { SetBusy(false); }
    }

    private async void Models_Click(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || _catalog is null || _library is null) return;
        SetBusy(true);
        try
        {
            var choice = new Window { Title = LanguageAppearance.Get("Linux_DllLibraryWindow_50", "Import NVIDIA models"), Width = 430, Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var options = new StackPanel { Spacing = 12, Margin = new Avalonia.Thickness(18) };
            var server = new Button { Content = LanguageAppearance.Get("Linux_DllLibraryWindow_49", "Download from NVIDIA server…") };
            var local = new Button { Content = LanguageAppearance.Get("Linux_DllLibraryWindow_48", "Import from local model cache…") };
            server.Click += (_, _) => choice.Close(true);
            local.Click += (_, _) => choice.Close(false);
            options.Children.Add(server); options.Children.Add(local); choice.Content = options;
            var fromServer = await choice.ShowDialog<bool?>(DialogOwner);
            if (fromServer is null) return;
            await new NgxModelImportWindow(_catalog, _cache, _library, fromServer.Value).ShowDialog(DialogOwner);
            _allEntries = _catalog.GetEntries().Select(entry => new DllLibraryEntryViewModel(entry, _cache.IsCached(entry))).ToArray();
            ApplyFilter();
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_47", "Model import failed: {0}", error.Message); }
        finally { SetBusy(false); }
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || _catalog is null || _library is null) return;
        SetBusy(true);
        try
        {
            var selected = (_entryListBox.SelectedItem as DllLibraryEntryViewModel)?.Entry;
            _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_46", "Refreshing the DLL catalog…");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var refreshed = await DllCatalogRefresh.FetchAsync(_sdkHttp,
                Path.Combine(_library.StateDirectory, "manifest.json"), timeout.Token);
            refreshed.Policy = _catalog.Policy;
            foreach (var entry in _library.State.ImportedDlls ?? []) refreshed.AddImported(entry);
            _catalog = refreshed;
            CatalogChanged?.Invoke(refreshed);
            _allEntries = refreshed.GetEntries().Select(entry => new DllLibraryEntryViewModel(entry, _cache.IsCached(entry))).ToArray();
            ApplyFilter();
            if (selected is not null) _entryListBox.SelectedItem = _allEntries.FirstOrDefault(row => row.Entry.Type == selected.Type && row.Entry.Md5 == selected.Md5);
            _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_45", "DLL catalog refreshed. No DLLs were downloaded and no game files changed.");
            await RefreshSdkVersionAsync(showWhileBusy: true);
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_44", "Catalog refresh failed; the previous catalog is still available. {0}", error.Message); }
        finally { SetBusy(false); }
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isBusy && !HasRecordDownloads && TryGetSelected(out var row)) await ExportAsync([row.Entry]);
    }

    private async void ExportAll_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isBusy && _catalog is not null)
            await ExportAsync(_catalog.GetExportEntries().Where(entry => File.Exists(_cache.GetCachedPath(entry))).ToArray());
    }

    private async Task ExportAsync(IReadOnlyList<DllCatalogEntry> entries)
    {
        if (entries.Count == 0) { _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_43", "There are no downloaded DLLs to export."); return; }
        SetBusy(true);
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = LanguageAppearance.Get("Linux_DllLibraryWindow_42", "Export downloaded DLLs"), SuggestedFileName = "dlss_swapper_export.zip", DefaultExtension = "zip",
                FileTypeChoices = [new FilePickerFileType(LanguageAppearance.Get("Linux_FileTypeZip", "ZIP archive")) { Patterns = ["*.zip"] }],
            });
            if (file is null || _closeRequested) return;
            var path = file.TryGetLocalPath() ?? throw new IOException(LanguageAppearance.Get("Linux_LibraryExportLocal", "Export requires a local file path."));
            _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_41", "Exporting DLLs. No game files will change.");
            var count = await DllExportWorkflow.ExportAsync(entries, _cache, path, _lifetime.Token);
            _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_40", "Exported {0} DLL(s) to {1}. No game files were changed.", count, path);
        }
        catch (OperationCanceledException) { _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_39", "Export cancelled. The destination was not replaced."); }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_38", "Export failed: {0}", error.Message); }
        finally { SetBusy(false); }
    }

    private void Filter_Changed(object? sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void Search_Changed(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void EntrySelection_Changed(object? sender, SelectionChangedEventArgs e) =>
        UpdateActionState();

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelected(out var row))
        {
            return;
        }

        await DownloadAsync(row);
    }

    private async void Info_Click(object? sender, RoutedEventArgs e)
    {
        if (TryGetSelected(out var row)) await new DllRecordDetailsWindow(row.Entry).ShowDialog(DialogOwner);
    }

    private async void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || HasRecordDownloads || !TryGetSelected(out var row))
        {
            return;
        }

        SetBusy(true);
        try
        {
            var message = LanguageAppearance.Format("Linux_LibraryRemoveConfirm", "Remove {0} {1} ({2}) from the Library cache? No game files or game backups will change.", row.Family, row.Version, row.Build)
                + " " + (row.Entry.IsImported
                    ? LanguageAppearance.Get("Linux_LibraryRemoveImported", "This also removes its imported Library record. You will need the source DLL or ZIP to import it again.")
                    : LanguageAppearance.Get("Linux_LibraryRemoveDownloadable", "You can download it again later."));
            if (!await new ConfirmationDialog(LanguageAppearance.Get("Linux_LibraryRemoveTitle", "Remove Library DLL?"), message).ShowDialog<bool>(DialogOwner) || _closeRequested) return;
            var removed = _cache.Remove(row.Entry);
            row.IsCached = false;
            row.TransferStatus = null;
            if (row.Entry.IsImported)
            {
                if (_library is null) throw new IOException(LanguageAppearance.Get("Linux_LibraryStorageUnavailable", "Cache removed, but imported Library storage is unavailable."));
                _library.UpdateState(state => state.ImportedDlls.RemoveAll(entry => entry.Type == row.Entry.Type && entry.Md5 == row.Entry.Md5));
                _catalog!.RemoveImported(row.Entry);
                _allEntries = _allEntries.Where(item => item.Entry != row.Entry).ToArray();
                ApplyFilter();
                _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_37", "Removed the imported DLL and its Library record. No game files were changed.");
                return;
            }
            _statusText.Text = removed
                ? LanguageAppearance.Format("Linux_DllLibraryWindow_36", "Removed {0} {1} ({2}) from the shared cache.", row.Family, row.Version, row.Build)
                : LanguageAppearance.Get("Linux_DllLibraryWindow_35", "That release is not downloaded.");
        }
        catch (Exception exception)
        { AppLog.Write(ApplicationLogLevel.Error, exception.Message);
            _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_34", "Removal did not finish: {0}. If the cache file was removed, the remaining imported record can be removed again or recovered by reimporting.", exception.Message);
        }
        finally { SetBusy(false); }
    }

    private async void DownloadLatest_Click(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || _catalog is null)
        {
            return;
        }

        BeginDownload();
        try
        {
            var latest = LibraryDownloadWorkflow.SelectLatestEligible(_catalog.GetEntries());
            _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_33", "Checking and downloading latest releases…");
            var results = await DownloadLatestEntriesAsync(latest);
            foreach (var result in results)
            {
                var row = _allEntries.First(item => ReferenceEquals(item.Entry, result.Entry));
                if (result.Status is LibraryDownloadStatus.Downloaded or LibraryDownloadStatus.Cached) { row.IsCached = true; row.TransferStatus = null; }
            }
            var acquired = new List<string>();
            var sdkError = string.Empty;
            if (!DownloadToken.IsCancellationRequested)
            {
                try
                {
                    _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_32", "Checking/downloading Streamline SDK…");
                    var package = await PrepareSdkWithProgressAsync(DownloadToken);
                    _sdkStatus.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_27", "Streamline SDK {0} — ready", package.Tag);
                    if (package.WasDownloaded) acquired.Add($"Streamline SDK {package.Tag}");
                }
                catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); sdkError = "\n" + (DownloadToken.IsCancellationRequested
                    ? LanguageAppearance.Get("Linux_DllLibraryWindow_26", "Streamline SDK download cancelled.")
                    : LanguageAppearance.Format("Linux_DllLibraryWindow_25", "Streamline SDK: {0}", error.Message)); }
            }
            _statusText.Text = LibraryDownloadWorkflow.Describe(results, acquired, LanguageAppearance.Current) + sdkError;
        }
        catch (Exception exception)
        { AppLog.Write(ApplicationLogLevel.Error, exception.Message);
            _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_31", "Latest-family download stopped: {0}", exception.Message);
        }
        finally
        {
            EndDownload();
            await RefreshSdkVersionAsync();
        }
    }

    private async Task RefreshSdkVersionAsync(bool showWhileBusy = false)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var releases = await _sdk.FetchReleasesAsync(timeout.Token);
            if (!_lifetime.IsCancellationRequested && (!_isBusy || showWhileBusy))
            {
                RenderSdkReleases(releases);
                _sdkStatus.Text = $"{releases.Count} SDK versions available. Downloads do not change game files.";
            }
        }
        catch (Exception error)
        { AppLog.Write(ApplicationLogLevel.Error, error.Message);
            if (!_lifetime.IsCancellationRequested && (!_isBusy || showWhileBusy))
            {
                RenderSdkReleases(_sdk.CachedReleases());
                _sdkStatus.Text = $"Release history unavailable: {error.Message}. Showing downloaded packages.";
            }
        }
    }

    private void RenderSdkReleases(IReadOnlyList<DLSS_Swapper.Data.Streamline.StreamlineSdkRelease> releases)
    {
        var rows = FindRequired<StackPanel>("StreamlineReleaseRows");
        rows.Children.Clear();
        foreach (var release in releases)
        {
            var cached = _sdk.FindCached(release.Tag) is not null;
            var button = new Button { Content = cached ? "Downloaded" : "Download", IsEnabled = !cached, Tag = release };
            button.Click += DownloadStreamline_Click;
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 16 };
            row.Children.Add(new TextBlock { Text = release.Tag, FontSize = 20, MinWidth = 120 });
            row.Children.Add(button); rows.Children.Add(row);
        }
    }

    private async void DownloadStreamline_Click(object? sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        BeginDownload();
        try
        {
            _sdkStatus.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_28", "Streamline SDK — downloading and checking package…");
            var package = sender is Button { Tag: DLSS_Swapper.Data.Streamline.StreamlineSdkRelease selected }
                ? await WithTransferProgressAsync($"Streamline SDK {selected.Tag}", progress => _sdk.PrepareAsync(selected, DownloadToken, progress), DownloadToken)
                : await PrepareSdkWithProgressAsync(DownloadToken);
            _sdkStatus.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_27", "Streamline SDK {0} — ready", package.Tag);
            _statusText.Text = LibraryDownloadWorkflow.Describe([], package.WasDownloaded ? [$"Streamline SDK {package.Tag}"] : [], LanguageAppearance.Current);
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _sdkStatus.Text = DownloadToken.IsCancellationRequested ? LanguageAppearance.Get("Linux_DllLibraryWindow_26", "Streamline SDK download cancelled.") : LanguageAppearance.Format("Linux_DllLibraryWindow_25", "Streamline SDK: {0}", error.Message); }
        finally { EndDownload(); await RefreshSdkVersionAsync(); }
    }


    private void ApplyFilter()
    {
        if (_entryListBox is null)
        {
            return;
        }

        if (ShowSdkFamily()) { _entryListBox.ItemsSource = Array.Empty<DllLibraryEntryViewModel>(); UpdateActionState(); return; }
        IEnumerable<DllLibraryEntryViewModel> entries = _allEntries;
        var familyIndex = _familyComboBox.SelectedIndex;
        if (familyIndex > 0 && familyIndex <= DllTypes.All.Count)
        {
            var type = DllTypes.All[familyIndex - 1].Type;
            entries = entries.Where(row => row.Entry.Type == type);
        }

        var search = _searchTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            entries = entries.Where(row => row.Version.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Entry.Md5.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _entryListBox.ItemsSource = entries.ToArray();
    }

    private async Task<bool> DownloadAsync(DllLibraryEntryViewModel row)
    {
        return await DownloadRecordAsync(row);
    }

    private bool TryGetSelected(out DllLibraryEntryViewModel row)
    {
        row = _entryListBox.SelectedItem as DllLibraryEntryViewModel ?? null!;
        if (row is not null)
        {
            return true;
        }

        _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_24", "Select one release first.");
        return false;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (!busy) ApplyPendingCatalog();
        UpdateActionState();
        if (!busy && _closeRequested) FinishStopping();
    }

    private void UpdateActionState()
    {
        _entryListBox.IsEnabled = !_isBusy;
        _downloadLatestButton.IsEnabled = !_isBusy && !HasRecordDownloads;
        _sdkDownloadButton.IsEnabled = !_isBusy && !HasRecordDownloads;
        _importButton.IsEnabled = !_isBusy && !HasRecordDownloads && _library is not null;
        _exportAllButton.IsEnabled = !_isBusy && !HasRecordDownloads && _catalog is not null;
        _refreshButton.IsEnabled = !_isBusy && !HasRecordDownloads && _library is not null;
        FindRequired<Button>("CancelDownloadButton").IsEnabled = HasRecordDownloads || _downloadCancellation is not null;
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Required control '{name}' is missing.");

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
