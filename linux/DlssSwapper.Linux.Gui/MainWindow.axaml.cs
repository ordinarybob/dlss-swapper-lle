using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow : Window
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly MainWindowViewModel _viewModel = new();
    private readonly SteamDiscovery _steamDiscovery = new();
    private readonly DllScanner _scanner = new();
    private readonly UpdatePlanner _planner = new();
    private readonly HashSet<string> _knownPaths = new(PathComparer);
    private readonly List<GameRowViewModel> _allRows = [];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HttpClient _artworkHttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(2),
    };
    private readonly TextBox _searchTextBox;
    private int _sortMode;
    private readonly ComboBox _gridCardSizeInput;
    private readonly ScrollViewer _gameGridViewport;

    private DllCatalog? _catalog;
    private PersistentLibrary? _library;
    private LibraryScanService? _scanService;
    private ArtworkService? _artworkService;
    private IReadOnlyList<SteamGame> _lastSteamGames = [];
    private CancellationTokenSource? _artworkCancellation;
    private Task? _artworkTask;
    private Task? _deepScanTask;
    private long _libraryViewGeneration;
    private Task? _customCoverTask;
    private Task? _startupTask;
    private bool _opened;
    internal WindowState StateBeforeMinimizing { get; private set; } = WindowState.Normal;

    private readonly MainWindowServices? _services;

    public MainWindow() : this(null) { }

    internal MainWindow(MainWindowServices? services)
    {
        _services = services;
        AvaloniaXamlLoader.Load(this);
        DataContext = _viewModel;

        _searchTextBox = FindRequiredTextBox("SearchTextBox");
        _gridCardSizeInput = this.FindControl<ComboBox>("GridCardSizeInput")
            ?? throw new InvalidOperationException("Required grid-card-size control is missing.");
        _gameGridViewport = this.FindControl<ScrollViewer>("GameGridViewport")
            ?? throw new InvalidOperationException("Required game-grid viewport is missing.");
        _gridCardSizeInput.ItemsSource = Enumerable.Range(
            ResponsiveGridLayout.MinimumCardSize,
            ResponsiveGridLayout.MaximumCardSize
                - ResponsiveGridLayout.MinimumCardSize
                + 1).ToArray();

        TryInitializeLibrary();
        if (_library is { } savedLibrary) WindowPlacement.Attach(this, savedLibrary, message => Console.Error.WriteLine(message));
        StateBeforeMinimizing = WindowState;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState != WindowState.Minimized)
                StateBeforeMinimizing = WindowState;
        };
        SizeChanged += (_, _) => UpdateHeaderLayout();
        LanguageAppearance.Changed += RefreshLanguage;
        Opened += MainWindow_Opened;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, CoverDragOver);
        AddHandler(DragDrop.DropEvent, CoverDrop);
    }

    private bool TryInitializeLibrary()
    {
        try
        {
            var manifestPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "static_manifest.json");
            var store = new LibraryStateStore();
            var savedCatalog = Path.Combine(store.StateDirectory, "manifest.json");
            var result = _services is null ? LibraryStartup.Load(savedCatalog, store, manifestPath) : _services.Load();
            if (!result.Succeeded)
            {
                ShowStartupError(result.Error!);
                return false;
            }
            _catalog = result.Catalog!;
            _library = result.Library!;
            if (_services is null)
            {
                AppLog.Configure(_library.StateDirectory, _library.State.ApplicationLoggingLevel);
                AppLog.Write(ApplicationLogLevel.Info, DiagnosticsReport.BuildIdentity());
            }
            ThemeAppearance.Apply(_library.State.AppTheme);
            LanguageAppearance.Apply(_library.State.Language);
            _catalog.Policy = new(_library.State.AllowDebugDlls, _library.State.AllowUntrustedDlls);
            foreach (var imported in _library.State.ImportedDlls ?? []) _catalog.AddImported(imported);
            _scanService = new LibraryScanService(_catalog, _scanner);
            _artworkHttpClient.DefaultRequestHeaders.UserAgent.Clear();
            _artworkHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "DLSS-Swapper-LLE-Linux/1.0");
            _artworkService?.Dispose();
            _artworkService = _services is not null ? _services.CreateArtwork() : new ArtworkService(
                _artworkHttpClient,
                new AvaloniaArtworkImageProcessor());
            _includeHidden = _library.State.ShowHiddenGames;
            _sortMode = Math.Clamp(_library.State.GameSortMode, 0, 2);
            _viewModel.IsGridView = _library.State.GridView;
            _viewModel.GridCardSize = _library.State.CardSize;
            _gridCardSizeInput.SelectedItem = _library.State.CardSize;
            _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage1", "Loading the persistent game library…");
            _viewModel.StartupError = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            ShowStartupError(LanguageAppearance.Format("Linux_GuiRemainingStartupRetry", "Library startup could not finish. No reset was performed. Retry after resolving this error:\n{0}", exception.Message));
            return false;
        }
    }

    private void ShowStartupError(string error)
    {
        AppLog.Write(ApplicationLogLevel.Error, error);
        _library = null;
        _scanService = null;
        _viewModel.IsLoadingLibrary = false;
        _viewModel.StartupError = error;
        _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage2", "Startup needs attention. See the details and Retry loading.");
    }

    private async void RetryStartup_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy || !_viewModel.HasStartupError || _lifetime.IsCancellationRequested) return;
        if (TryInitializeLibrary())
        {
            _startupTask = FinishStartupAsync();
            await _startupTask;
        }
    }

    private bool _libraryPageStopped;
    private Task? _libraryStopping;
    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_streamlineMutationActive)
        {
            e.Cancel = true;
            _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage3", "Wait for the Streamline file operation to finish before closing.");
            return;
        }
        if (_libraryPage is not null && !_libraryPageStopped)
        {
            e.Cancel = true;
            if (_libraryStopping is not null) return;
            try
            {
                _libraryStopping = _libraryPage.StopAsync();
                await _libraryStopping;
                _libraryPageStopped = true;
                Dispatcher.UIThread.Post(() => Close());
            }
            catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _viewModel.StatusText = error.Message; }
            return;
        }
        _lifetime.Cancel();
        ++_publicationGeneration;
        _artworkCancellation?.Cancel();
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        LanguageAppearance.Changed -= RefreshLanguage;
        try
        {
            var backgroundTasks = new[] { _startupTask, _artworkTask, _deepScanTask, _customCoverTask, _viewPublication, _rowPreparation }
                .Where(task => task is not null)
                .Cast<Task>()
                .ToArray();
            if (backgroundTasks.Length > 0)
            {
                await Task.WhenAll(backgroundTasks);
            }
        }
        catch (OperationCanceledException)
        {
            // Closing cancels outstanding background work.
        }
        catch
        {
            // Background tasks already report operational failures in the UI.
        }
        finally
        {
            foreach (var row in _allRows)
            {
                row.PropertyChanged -= GameRow_PropertyChanged;
                row.ClearArtwork();
            }

            _artworkCancellation?.Dispose();
            _artworkService?.Dispose();
            _artworkHttpClient.Dispose();
            _lifetime.Dispose();
        }
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (_opened)
        {
            return;
        }

        _opened = true;
        if (!_viewModel.HasStartupError)
        {
            _startupTask = FinishStartupAsync();
            await _startupTask;
        }
    }

    private void RefreshLanguage()
    {
        _viewModel.RefreshLanguage();
        foreach (var row in _allRows) row.RefreshLanguage();
    }

    private async Task FinishStartupAsync()
    {
        try
        {
            if (_services is null && _library?.State.Proxy is { } proxy)
            {
                HttpClient.DefaultProxy = new System.Net.WebProxy(proxy.Address());
                var password = proxy.CredentialId is { } id ? await ProxyKeyring.ReadAsync(id, _lifetime.Token) : null;
                HttpClient.DefaultProxy = proxy.CreateProxy(password);
            }
            if (_library is { } library && !library.State.HasSelectedStorageProfile)
            {
                var hddMode = await new StorageProfileDialog().ShowDialog<bool?>(GameDialogOwner);
                if (_lifetime.IsCancellationRequested) return;
                if (hddMode.HasValue) library.UpdateState(state =>
                {
                    state.ApplyStorageProfile(hddMode.Value);
                });
            }

            UpdateGridGeometry();
            if (_library is { } savedLibrary)
            {
                try { await ReplaceScansAsync(DiscoverySnapshot.ReadScans(savedLibrary)); }
                catch (Exception error) { AddAlert("Warning", LanguageAppearance.Format("Linux_ScanLoadFailed", "Saved scan results could not be loaded: {0}", error.Message)); }
            }
            await RefreshLibraryAsync(runInitialDeepScan: true);
            if (_services is null && _library is { } versionLibrary)
            {
                try
                {
                    var version = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
                    var refreshed = await LibraryStartup.RefreshForVersionAsync(_artworkHttpClient, versionLibrary, version, _lifetime.Token);
                    if (refreshed is not null)
                    {
                        refreshed.Policy = new(versionLibrary.State.AllowDebugDlls, versionLibrary.State.AllowUntrustedDlls);
                        foreach (var imported in versionLibrary.State.ImportedDlls) refreshed.AddImported(imported);
                        _catalog = refreshed;
                        _scanService = new LibraryScanService(refreshed, _scanner);
                    }
                }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
                catch (Exception error)
                {
                    AddAlert("Warning", LanguageAppearance.Format("Linux_VersionCatalogRefreshFailed",
                        "The catalog refresh for this app version could not finish. The cached catalog remains available; the app will retry next launch. {0}", error.Message));
                }
            }
        }
        catch (Exception error)
        {
            ShowStartupError(LanguageAppearance.Format("Linux_GuiRemainingStartupFailed", "Library startup could not finish. No reset was performed.\n{0}", error.Message));
        }
    }

    private async void AddOneGame_Click(object? sender, RoutedEventArgs e) =>
        await ImportGamesAsync(ManualImportKind.Single);

    private async void AddSeparateGames_Click(object? sender, RoutedEventArgs e) =>
        await ImportGamesAsync(ManualImportKind.Multiple);

    private async void AddParentDirectory_Click(object? sender, RoutedEventArgs e) =>
        await ImportGamesAsync(ManualImportKind.Parent);

    private async Task ImportGamesAsync(ManualImportKind kind)
    {
        if (!TryGetLibrary(out var library))
        {
            return;
        }

        var (suppressed, body, action, pickerTitle) = kind switch
        {
            ManualImportKind.Single => (
                library.State.SuppressSingleFolderNotice,
                LanguageAppearance.Get("Linux_GuiRemainingImportSingle", "Select a single games installation folder."),
                LanguageAppearance.Get("Linux_GuiRemainingSelectSingle", "Select Game Folder"),
                LanguageAppearance.Get("Linux_GuiRemainingSelectSingle", "Select Game Folder")),
            ManualImportKind.Multiple => (
                library.State.SuppressMultipleFoldersNotice,
                LanguageAppearance.Get("Linux_GuiRemainingImportMultiple", "Select multiple separate game installation folders."),
                LanguageAppearance.Get("Linux_GuiRemainingSelectMultiple", "Select Game Folders"),
                LanguageAppearance.Get("Linux_GuiRemainingSelectMultiple", "Select Game Folders")),
            ManualImportKind.Parent => (
                library.State.SuppressMultiGameDirectoryNotice,
                LanguageAppearance.Get("Linux_GuiRemainingImportParent", "Select the main folder where the games you want to add are installed. Each immediate child folder will be added as a separate manually added game. The parent folder itself is not added, and nested folders are not searched."),
                LanguageAppearance.Get("Linux_GuiRemainingSelectParent", "Select Multi-Game Directory"),
                LanguageAppearance.Get("Linux_GuiRemainingSelectParent", "Select Multi-Game Directory")),
            _ => throw new InvalidOperationException("Unknown manual import kind."),
        };

        if (!suppressed)
        {
            var notice = await new ImportNoticeDialog(body, action)
                .ShowDialog<ImportNoticeResult>(GameDialogOwner);
            if (notice is not { Proceed: true })
            {
                return;
            }

            if (notice.DontShowAgain)
            {
                library.UpdateState(state =>
                {
                    switch (kind)
                    {
                        case ManualImportKind.Single:
                            state.SuppressSingleFolderNotice = true;
                            break;
                        case ManualImportKind.Multiple:
                            state.SuppressMultipleFoldersNotice = true;
                            break;
                        case ManualImportKind.Parent:
                            state.SuppressMultiGameDirectoryNotice = true;
                            break;
                    }
                });
            }
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = pickerTitle,
                AllowMultiple = kind == ManualImportKind.Multiple,
            });
        if (folders.Count == 0)
        {
            return;
        }

        var localPaths = folders
            .Select(folder => folder.Path.LocalPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        var previousPaths = library.State.ManualGames.Select(game => game.RootPath).ToHashSet(PathComparer);
        if (kind == ManualImportKind.Single && !await EditSingleImportAsync(library, localPaths)) return;
        var added = kind == ManualImportKind.Parent
            ? library.AddImmediateChildren(localPaths[0])
            : kind == ManualImportKind.Single ? 1 : library.AddManualGames(localPaths);
        _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesOperation1", "Persisted {0} new game path{1}.", added, Plural(added));
        await RefreshLibraryAsync(runInitialDeepScan: false);
        var newGames = library.State.ManualGames.Where(game => !previousPaths.Contains(game.RootPath)).ToArray();
        try { await ManualLaunchSetupWindow.OfferAsync(this, library, newGames); }
        catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage4", "Games were imported, but launch setup could not finish: {0}", ex.Message); }
    }

    private async void DeepScan_Click(object? sender, RoutedEventArgs e)
    {
        if (_deepScanTask is { IsCompleted: false })
        {
            _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage5", "Deep Scan is already running in the background.");
            return;
        }

        var confirmed = await new ConfirmationDialog(
            LanguageAppearance.Get("GamesPage_DeepScan", "Deep Scan"),
            LanguageAppearance.Get("Linux_DeepScanPrompt", "Deep Scan runs automatically the first time you launch DLSS Swapper LLE and learns path patterns for Fast Scan. Run it again only after your library changes and a game is missing. If you know which game is missing, add that game directly instead."))
            .ShowDialog<bool>(GameDialogOwner);
        if (!confirmed)
        {
            return;
        }

        if (!TryGetLibrary(out var library)
            || !TryGetScanService(out var service))
        {
            return;
        }

        var games = await DiscoverMergedGamesAsync(library);
        await RunBusyAsync(LanguageAppearance.Get("Linux_GamesOperation2", "Deep Scan is checking every game directory…"), async () =>
        {
            var result = await service.ScanDeepAsync(
                games,
                library,
                CreateScanProgress(isDeepScan: true),
                _lifetime.Token);
            await ReplaceLibraryScansAsync(library, result.Games);
            StartArtworkHydration();
            _viewModel.StatusText =
                LanguageAppearance.Format("Linux_GamesOperation3", "Deep Scan inspected {0} game{1} and learned {2} new fast-scan pattern{3}.", _viewModel.GameCount, Plural(_viewModel.GameCount), result.LearnedPatternCount, Plural(result.LearnedPatternCount));
        });
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) =>
        await RefreshLibraryAsync(runInitialDeepScan: false);

    private async void RemoveSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedRows(out var rows) || !TryGetLibrary(out var library))
        {
            return;
        }

        var confirmed = await new ConfirmationDialog(
            LanguageAppearance.Get("Linux_RemoveGamesTitle", "Remove games"),
            LanguageAppearance.Format("Linux_RemoveGamesPrompt", "Remove {0} selected game{1} from this library?", rows.Length, Plural(rows.Length)) + "\n\n" + RemovalNotice)
            .ShowDialog<bool>(GameDialogOwner);
        if (!confirmed)
        {
            return;
        }

        foreach (var row in rows)
        {
            RemoveGameFromLibrary(row, library);
        }

        await RefreshLibraryAsync(runInitialDeepScan: false);
    }

    private async void RestoreExcluded_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetLibrary(out var library))
        {
            return;
        }

        var restored = library.RestoreSteamGames();
        _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesOperation4", "Restored {0} excluded Steam game{1}.", restored, Plural(restored));
        await RefreshLibraryAsync(runInitialDeepScan: false);
    }

    private async void RestoreExcludedProviders_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetLibrary(out var library)) return;
        try
        {
            library.UpdateState(state => state.ExcludedProviderGames = []);
            await RefreshLibraryAsync(runInitialDeepScan: false);
            _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage6", "Cleared launcher-game exclusions and refreshed the library. Unavailable installations remain absent.");
        }
        catch (Exception error) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage7", "Could not restore excluded launcher games: {0}", error.Message); }
    }

    private SettingsPage? _settingsPage;
    private LibraryPage? _libraryPage;

    private void GamesNavigation_Click(object? sender, RoutedEventArgs e) => ShowGamesPage();

    private void ShowGamesPage()
    {
        this.FindControl<Control>("GamesPageHost")!.IsVisible = true;
        this.FindControl<Control>("SettingsPageHost")!.IsVisible = false;
        this.FindControl<Control>("LibraryPageHost")!.IsVisible = false;
        this.FindControl<Button>("LibraryNavigationButton")!.Classes.Set("navSelected", false);
        this.FindControl<Button>("GamesNavigationButton")!.Classes.Set("navSelected", true);
        this.FindControl<Button>("SettingsNavigationButton")!.Classes.Set("navSelected", false);
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy || !TryGetLibrary(out var library))
        {
            return;
        }

        if (_settingsPage is null)
        {
            var page = new SettingsPage(library, () => DiagnosticsReport.Capture(library, _allRows.Select(row => row.Game).ToArray()));
            page.EnableImmediateSettings();
            page.Changed += () =>
            {
                if (_catalog is not null) _catalog.Policy = new(library.State.AllowDebugDlls, library.State.AllowUntrustedDlls);
                _viewModel.GridCardSize = library.State.CardSize;
                _gridCardSizeInput.SelectedItem = library.State.CardSize;
                UpdateGridGeometry();
                ApplyGameView();
            };
            page.Finished += async saved =>
            {
                _settingsPage = null;
                this.FindControl<ContentControl>("SettingsPageHost")!.Content = null;
                ShowGamesPage();
                await SettingsFinishedAsync(page, library, saved);
            };
            _settingsPage = page;
        }
        this.FindControl<ContentControl>("SettingsPageHost")!.Content = _settingsPage;
        this.FindControl<Control>("SettingsPageHost")!.IsVisible = true;
        this.FindControl<Control>("GamesPageHost")!.IsVisible = false;
        this.FindControl<Control>("LibraryPageHost")!.IsVisible = false;
        this.FindControl<Button>("LibraryNavigationButton")!.Classes.Set("navSelected", false);
        this.FindControl<Button>("GamesNavigationButton")!.Classes.Set("navSelected", false);
        this.FindControl<Button>("SettingsNavigationButton")!.Classes.Set("navSelected", true);
    }

    private async Task SettingsFinishedAsync(SettingsPage settings, PersistentLibrary library, bool saved)
    {
        if (saved || settings.LibrarySelectionChanged)
        {
            ApplyGameView();
            if (_catalog is not null) _catalog.Policy = new(library.State.AllowDebugDlls, library.State.AllowUntrustedDlls);
            _viewModel.GridCardSize = library.State.CardSize;
            _gridCardSizeInput.SelectedItem = library.State.CardSize;
            UpdateGridGeometry();
            _viewModel.StatusText = library.State.HddMode
                ? LanguageAppearance.Format("Linux_GuiRemainingHddSaved", "Settings saved. HDD profile: {0} scan workers, {1} artwork workers.", library.State.Performance.ScanConcurrency, library.State.Performance.ArtworkConcurrency)
                : LanguageAppearance.Format("Linux_GuiRemainingStandardSaved", "Settings saved. Standard profile: {0} scan workers, {1} artwork workers.", library.State.Performance.ScanConcurrency, library.State.Performance.ArtworkConcurrency);
            await RefreshLibraryAsync(runInitialDeepScan: settings.WasReset);
        }
    }

    private void DllLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetCatalog(out var catalog))
        {
            return;
        }

        if (_libraryPage is null)
        {
            var page = new LibraryPage(catalog, _library);
            page.CatalogChanged += refreshed => { _catalog = refreshed; _scanService = new LibraryScanService(refreshed, _scanner); };
            _libraryPage = page;
            this.FindControl<ContentControl>("LibraryPageHost")!.Content = page;
            if (_services is null) page.Start();
        }
        else _libraryPage.UpdateCatalog(catalog);
        this.FindControl<Control>("LibraryPageHost")!.IsVisible = true;
        this.FindControl<Control>("GamesPageHost")!.IsVisible = false;
        this.FindControl<Control>("SettingsPageHost")!.IsVisible = false;
        this.FindControl<Button>("LibraryNavigationButton")!.Classes.Set("navSelected", true);
        this.FindControl<Button>("GamesNavigationButton")!.Classes.Set("navSelected", false);
        this.FindControl<Button>("SettingsNavigationButton")!.Classes.Set("navSelected", false);
    }

    private async void GameLaunch_Click(object? sender, RoutedEventArgs e) => await GameLaunchAsync(sender);

    private async Task GameLaunchAsync(object? sender)
    {
        if (_viewModel.IsBusy) return;
        if (!TryGetMenuGame(sender, out var row))
        {
            return;
        }

        try
        {
            var installedSteamGame = _lastSteamGames.FirstOrDefault(game =>
                PathComparer.Equals(game.InstallDirectory, row.RootPath));
            if (installedSteamGame is not null)
            {
                using var process = Process.Start(new ProcessStartInfo { FileName = $"steam://rungameid/{installedSteamGame.AppId}", UseShellExecute = true });
                _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage8", "Sent {0} to Steam.", row.Name);
                return;
            }
            if (row.Game.ProviderLaunch is { } providerLaunch)
            {
                string? wineRunner = null;
                if (providerLaunch.Launcher == ProviderLauncher.Wine)
                {
                    if (_library is null) return;
                    wineRunner = _library.State.ProviderWineRunners.GetValueOrDefault(providerLaunch.ConfigurationDirectory)
                        ?? LaunchSuggestions.FindWine(Environment.GetEnvironmentVariable("PATH"));
                    string? setupError = null;
                    try { providerLaunch.CreateStartInfo(wineRunner: wineRunner); }
                    catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
                    { setupError = error.Message; }
                    if (setupError is not null)
                    {
                        wineRunner = await new ProviderWineLaunchDialog(row.Name, providerLaunch, wineRunner,
                            error: setupError).ShowDialog<string?>(GameDialogOwner);
                        if (wineRunner is null) return;
                        _library.UpdateState(state => state.ProviderWineRunners[providerLaunch.ConfigurationDirectory] = wineRunner);
                    }
                }
                using var request = Process.Start(providerLaunch.CreateStartInfo(_library?.State.HeroicExecutable, wineRunner));
                _viewModel.StatusText = request is null ? LanguageAppearance.Format("Linux_LaunchDispatchFailed", "Could not dispatch the launch request for {0}.", row.Name)
                    : LanguageAppearance.Format("Linux_LaunchDispatched", "Sent a launch request for {0} to {1}.", row.Name, providerLaunch.ClientName ?? providerLaunch.Launcher.ToString());
                return;
            }
            var manual = _library?.State.ManualGames.FirstOrDefault(game => PathComparer.Equals(game.RootPath, row.RootPath));
            if (manual is null) { _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage9", "No launch configuration is available for this game."); return; }
            if (manual.Launch is null)
            {
                await ManualLaunchSetupWindow.ConfigureAsync(this, _library!, [manual]);
                _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage10", "Launch setup closed. Use Launch when ready; saving did not start the game.");
                return;
            }
            using var launched = Process.Start(manual.Launch.CreateStartInfo());
            _viewModel.StatusText = launched is null ? LanguageAppearance.Format("Linux_LaunchStartFailed", "Could not start {0}.", row.Name) : LanguageAppearance.Format("Linux_LaunchStarted", "Started the configured executable for {0}.", row.Name);
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage11", "Could not launch {0}: {1}", row.Name, exception.Message);
        }
    }

    private async void GameNotes_Click(object? sender, RoutedEventArgs e) => await GameNotesAsync(sender);

    private async Task GameNotesAsync(object? sender)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        var notes = await new GameNotesDialog(
            row.Name,
            library.FindGamePreference(row.RootPath)?.Notes,
            draft => library.UpdateGamePreference(row.RootPath, preference => preference.Notes = draft))
            .ShowDialog<string?>(GameDialogOwner);
        if (notes is null)
        {
            return;
        }

        _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage12", "Saved notes for {0}.", row.Name);
    }

    private async void GameHistory_Click(object? sender, RoutedEventArgs e) => await GameHistoryAsync(sender);

    private async Task GameHistoryAsync(object? sender)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        var historyWindow = new GameHistoryWindow(
            row.Name,
            library.GetGameHistory(row.RootPath))
        {
            Width = Math.Clamp(ClientSize.Width * 0.82, 520, 1100),
            Height = Math.Clamp(ClientSize.Height * 0.78, 320, 760),
        };
        await historyWindow.ShowDialog(GameDialogOwner);
    }

    private void GameFavorite_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        GamePreferenceState preference;
        try { preference = library.UpdateGamePreference(row.RootPath, value => value.IsFavorite = !value.IsFavorite); }
        catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage13", "Could not save favorites for {0}: {1}", row.Name, ex.Message); return; }
        row.ApplyPreference(preference);
        ApplyGameView();
        _viewModel.StatusText = preference.IsFavorite
            ? LanguageAppearance.Format("Linux_FavoriteAdded", "Added {0} to favorites.", row.Name)
            : LanguageAppearance.Format("Linux_FavoriteRemoved", "Removed {0} from favorites.", row.Name);
    }

    private async void GameReload_Click(object? sender, RoutedEventArgs e) => await GameReloadAsync(sender);

    private async Task GameReloadAsync(object? sender)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetCatalog(out var catalog))
        {
            return;
        }

        await ReloadGameAsync(row, catalog, recordHistory: true);
    }

    private void GameHide_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        GamePreferenceState preference;
        try { preference = library.UpdateGamePreference(row.RootPath, value => value.IsHidden = !value.IsHidden); }
        catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage14", "Could not save visibility for {0}: {1}", row.Name, ex.Message); return; }
        row.ApplyPreference(preference);
        ApplyGameView();
        _viewModel.StatusText = preference.IsHidden
            ? LanguageAppearance.Format("Linux_GameHidden", "Hid {0}. Use the Hidden filter to show it again.", row.Name)
            : LanguageAppearance.Format("Linux_GameUnhidden", "Restored {0} to the library view.", row.Name);
    }

    private async void GameCustomCover_Click(object? sender, RoutedEventArgs e) => await GameCustomCoverAsync(sender);

    private async Task GameCustomCoverAsync(object? sender)
    {
        if (_viewModel.IsBusy || _lifetime.IsCancellationRequested) return;
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        if (library.FindGamePreference(row.RootPath)?.CustomArtworkPath is { } confirmedPath)
        {
            if (!await new ConfirmationDialog(LanguageAppearance.Get("Linux_RemoveCoverTitle", "Remove custom cover"),
                LanguageAppearance.Format("Linux_RemoveCoverPrompt", "Remove the custom cover for {0} and return to ordinary artwork? Your source image will not be deleted.", row.Name)).ShowDialog<bool>(GameDialogOwner)) return;
            if (_viewModel.IsBusy || _lifetime.IsCancellationRequested) return;
            try
            {
                CustomCoverWorkflow.Remove(library, row.RootPath, confirmedPath);
                row.ApplyPreference(library.FindGamePreference(row.RootPath) ?? new GamePreferenceState());
                row.ClearArtwork();
                StartArtworkHydration();
                _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage15", "Removed custom cover for {0}.", row.Name);
            }
            catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage16", "Could not remove custom cover: {0}", ex.Message); }
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LanguageAppearance.Format("Linux_MainWindow_96", "Select cover art for {0}", row.Name),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(LanguageAppearance.Get("Linux_FileTypeImages", "Image files"))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/bmp"],
                },
            ],
        });
        if (files.Count == 0)
        {
            return;
        }

        await ApplyCustomCoverAsync(row, library, files[0].Path.LocalPath);
    }

    private Task ApplyCustomCoverAsync(GameRowViewModel row, PersistentLibrary library, string path)
    {
        if (_viewModel.IsBusy || _lifetime.IsCancellationRequested) return Task.CompletedTask;
        return _customCoverTask = RunBusyAsync(LanguageAppearance.Format("Linux_GamesOperation10", "Saving custom cover for {0}…", row.Name),
            () => ApplyCustomCoverCoreAsync(row, library, path));
    }

    private async Task ApplyCustomCoverCoreAsync(GameRowViewModel row, PersistentLibrary library, string path)
    {
        if (!File.Exists(path))
        {
            _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage17", "The selected cover-art file is unavailable.");
            return;
        }

        var fullPath = Path.GetFullPath(path);
        try
        {
            fullPath = await CustomCoverWorkflow.SaveAsync(library, row.RootPath, fullPath,
                new AvaloniaArtworkImageProcessor(), _lifetime.Token);
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage18", "Could not apply custom cover art to {0}: {1}", row.Name, ex.Message);
            return;
        }

        var displayError = TrySetArtwork(row, fullPath);
        row.ApplyPreference(library.FindGamePreference(row.RootPath)!);
        if (displayError is not null)
        {
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage19", "Custom cover saved, but could not be displayed: {0}", displayError);
            return;
        }
        try { library.RecordHistory(row.RootPath, "Cover changed", "Artwork", detail: path); }
        catch (Exception ex)
        {
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage20", "Custom cover saved for {0}, but its history could not be saved: {1}", row.Name, ex.Message);
            return;
        }
        _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage21", "Applied custom cover art to {0}.", row.Name);
    }

    private async void GameUpdateLatest_Click(object? sender, RoutedEventArgs e) => await GameUpdateLatestAsync(sender);

    private async Task GameUpdateLatestAsync(object? sender)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetCatalog(out var catalog))
        {
            return;
        }

        if (row.ScanResult is null || row.ScanResult.CachedAtUtc is not null)
        {
            await ReloadGameAsync(row, catalog, recordHistory: false);
        }

        if (row.ScanResult is null)
        {
            return;
        }

        var candidates = row.ScanResult.Dlls
            .Select(dll => dll.Type)
            .Distinct()
            .ToDictionary(type => type, catalog.GetLatest);
        var plan = _planner.Plan([row.ScanResult], candidates, LanguageAppearance.Current);
        var ready = plan.Where(item => item.Status == UpdatePlanStatus.Ready).ToArray();
        var targetCount = ready.Sum(item => item.Targets.Count);
        if (targetCount == 0)
        {
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage22", "{0} already has the latest detected DLL versions.", row.Name);
            return;
        }

        var confirmed = await new ConfirmationDialog(
            LanguageAppearance.Get("Linux_ConfirmUpdateTitle", "Confirm DLL update"),
            LanguageAppearance.Format("Linux_ConfirmUpdatePrompt", "Update {0} detected DLL file{1} in {2}? An adjacent .dlsss backup is created when needed.", targetCount, Plural(targetCount), row.Name),
            ConfirmationDialog.GameFileWriteWarning)
            .ShowDialog<bool>(GameDialogOwner);
        if (!confirmed)
        {
            return;
        }

        await RunBusyAsync(LanguageAppearance.Format("Linux_GamesOperation11", "Updating detected DLLs in {0}…", row.Name), async () =>
        {
            using var cache = new DownloadCache();
            var results = await Task.Run(() => DllOperations.ApplyUpdatesAsync(
                plan,
                cache,
                _lifetime.Token, LanguageAppearance.Current));
            ShowOperationFailures(results);
            foreach (var result in results.Where(result => result.Success))
            {
                _library?.RecordHistory(
                    row.RootPath,
                    "DLL updated",
                    result.Family,
                    detail: result.Target);
            }

            foreach (var (_, scan) in await ScanRowsAsync([row], catalog))
            {
                row.SetScanResult(scan);
            }

            var succeeded = results.Count(result => result.Success);
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesOperation12", "Updated {0} DLL file{1} in {2}.", succeeded, Plural(succeeded), row.Name);
        });
    }

    private async void GameRemove_Click(object? sender, RoutedEventArgs e) => await GameRemoveAsync(sender);

    private async Task GameRemoveAsync(object? sender)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        var confirmed = await new ConfirmationDialog(
            LanguageAppearance.Get("Linux_RemoveGameTitle", "Remove game"),
            LanguageAppearance.Format("Linux_RemoveGamePrompt", "Remove {0} from this library?", row.Name) + "\n\n" + RemovalNotice)
            .ShowDialog<bool>(GameDialogOwner);
        if (!confirmed)
        {
            return;
        }

        RemoveGameFromLibrary(row, library);
        _gameDetails?.CloseAfterRemoval();
        await RefreshLibraryAsync(runInitialDeepScan: false);
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_opened)
        {
            ApplyGameView();
        }
    }

    private void SortMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value }
            && int.TryParse(value, out var index))
        {
            _sortMode = index;
            ApplyGameView();
            try { _library?.UpdateState(state => state.GameSortMode = index); }
            catch (Exception error) { _viewModel.StatusText = error.Message; }
        }
    }

    private void Batch_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy) return;
        _viewModel.IsBatchMode = !_viewModel.IsBatchMode;
        if (!_viewModel.IsBatchMode) foreach (var row in _allRows) row.IsSelected = false;
    }

    private void DismissAlert_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.AlertText = string.Empty;

    private void ListView_Click(object? sender, RoutedEventArgs e) => SetGridView(false);

    private void GridView_Click(object? sender, RoutedEventArgs e) => SetGridView(true);

    private void SetGridView(bool gridView)
    {
        _viewModel.IsGridView = gridView;
        if (gridView)
        {
            UpdateGridGeometry();
        }
        if (_library is not null && _library.State.GridView != gridView)
        {
            _library.UpdateState(state => state.GridView = gridView);
        }
    }

    private void GridCardSize_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_library is null
            || _gridCardSizeInput.SelectedItem is not int cardSize)
        {
            return;
        }

        _viewModel.GridCardSize = cardSize;
        UpdateGridGeometry();
        if (_library.State.CardSize != cardSize)
        {
            _library.UpdateState(state => state.CardSize = cardSize);
        }
    }

    private void GameGridViewport_PointerWheelChanged(
        object? sender,
        PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0)
        {
            return;
        }

        var nextSize = Math.Clamp(
            _viewModel.GridCardSize + (e.Delta.Y > 0 ? 1 : -1),
            ResponsiveGridLayout.MinimumCardSize,
            ResponsiveGridLayout.MaximumCardSize);
        _gridCardSizeInput.SelectedItem = nextSize;
        e.Handled = true;
    }

    private void GameGridViewport_SizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateGridGeometry();

    private void UpdateGridGeometry()
    {
        var width = _gameGridViewport.Bounds.Width - 8;
        if (width <= 0)
        {
            return;
        }

        var scale = TopLevel.GetTopLevel(_gameGridViewport)?.RenderScaling ?? 1d;
        var metrics = ResponsiveGridLayout.Calculate(
            width,
            _viewModel.GridCardSize,
            scale);
        _viewModel.GridItemWidth = metrics.CardWidth;
        _viewModel.GridItemHeight = metrics.CardHeight;
        foreach (var row in _allRows)
        {
            row.SetCardSize(metrics.CardWidth, metrics.CardHeight);
        }
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GameViewPolicy.ShouldSelectAll(_viewModel.Games.Select(game => game.IsSelected));
        foreach (var game in _viewModel.Games)
        {
            game.IsSelected = selected;
        }
    }

    private async Task RefreshLibraryAsync(bool runInitialDeepScan)
    {
        if (_viewModel.IsBusy || !TryGetLibrary(out var library)
            || !TryGetScanService(out var service))
        {
            return;
        }

        IReadOnlyList<SelectedGame> games = [];
        var generation = ++_libraryViewGeneration;
        var succeeded = false;
        _viewModel.IsLoadingLibrary = true;
        await RunBusyAsync(LanguageAppearance.Get("Linux_DiscoveringGames", "Discovering and fast-scanning the game library…"), async () =>
        {
            games = await DiscoverMergedGamesAsync(library);
            var result = await service.ScanFastAsync(
                games,
                library.State,
                CreateScanProgress(isDeepScan: false),
                _lifetime.Token);
            await ReplaceLibraryScansAsync(library, result.Games);
            StartArtworkHydration();
            _viewModel.IsLoadingLibrary = false;
            _viewModel.StatusText =
                LanguageAppearance.Format("Linux_FastScanFinished", "Fast Scan inspected {0} game{1} in {2:F2} seconds.", _viewModel.GameCount, Plural(_viewModel.GameCount), result.Elapsed.TotalSeconds);
            succeeded = true;
        });

        if (!succeeded)
        {
            _viewModel.IsLoadingLibrary = false;
            return;
        }

        if (runInitialDeepScan
            && !library.State.HasCompletedInitialDeepScan
            && games.Count > 0
            && !_lifetime.IsCancellationRequested)
        {
            _deepScanTask = RunInitialDeepScanAsync(games, library, service, generation);
        }
    }

    private async Task<IReadOnlyList<SelectedGame>> DiscoverMergedGamesAsync(
        PersistentLibrary library)
    {
        var discovery = _services is not null ? await _services.DiscoverSteam(_lifetime.Token) : await Task.Run(
            () => !LibrarySelection.Enabled(library.State, "Steam") ? new SteamDiscoveryResult([], []) : _steamDiscovery.Discover(new SteamDiscoveryOptions
            {
                AdditionalRoots = library.State.AdditionalSteamRoots,
            }),
            _lifetime.Token);
        var mergedSteam = DiscoveryCacheReconciliation.Steam(library.State.DiscoverySnapshot?.SteamGames ?? [], discovery);
        _lastSteamGames = mergedSteam.Games;
        foreach (var warning in discovery.Warnings)
        {
            AddAlert("Warning", warning);
        }

        var providers = await Task.Run(() => _services is null
            ? ProviderDiscovery.Discover(library.State, _lifetime.Token, respectLibrarySelection: true)
            : _services.DiscoverProviders(library.State, _lifetime.Token), _lifetime.Token);
        foreach (var warning in providers.Warnings) AddAlert("Warning", warning);
        var mergedProviders = DiscoveryCacheReconciliation.Providers(library.State.DiscoverySnapshot?.ProviderGames ?? [], providers);
        var games = library.Merge(new(mergedSteam.Games, discovery.Warnings), mergedProviders.Games)
            .Where(game => LibrarySelection.Includes(library.State, game)).ToArray();
        try { DiscoverySnapshot.Save(library, discovery, providers); }
        catch (Exception error) { AddAlert("Warning", LanguageAppearance.Format("Linux_DiscoverySaveFailed", "Discovery results could not be saved: {0}", error.Message)); }
        foreach (var filesystem in FilesystemInspector.InspectPaths(
            games.Select(game => game.RootPath))
            .GroupBy(item => (item.MountPoint, item.Type))
            .Select(group => group.First()))
        {
            var detail = filesystem.Warning
                ?? LanguageAppearance.Format("Linux_GuiRemainingFilesystem", "{0} mounted at {1}", filesystem.Type, filesystem.MountPoint);
            AddAlert(filesystem.Warning is null ? "Detected" : "Warning", detail);
        }

        return games;
    }

    private async Task RunInitialDeepScanAsync(
        IReadOnlyList<SelectedGame> games,
        PersistentLibrary library,
        LibraryScanService service, long generation)
    {
        try
        {
            _viewModel.StatusText =
                LanguageAppearance.Format("Linux_FastScanReady", "Fast Scan is ready with {0} game{1}. Initial Deep Scan is learning any missing layouts in the background…", _viewModel.GameCount, Plural(_viewModel.GameCount));
            var result = await service.ScanDeepAsync(
                games,
                library,
                CreateScanProgress(isDeepScan: true),
                _lifetime.Token);
            if (generation != _libraryViewGeneration) return;
            await ReplaceLibraryScansAsync(library, result.Games);
            StartArtworkHydration();
            _viewModel.StatusText =
                LanguageAppearance.Format("Linux_InitialScanFinished", "Initial Deep Scan completed in {0:F2} seconds; learned {1} new fast-scan pattern{2}.", result.Elapsed.TotalSeconds, result.LearnedPatternCount, Plural(result.LearnedPatternCount));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Completion remains false, so the next launch retries.
        }
        catch (Exception exception)
        {
            _viewModel.StatusText =
                LanguageAppearance.Format("Linux_InitialScanFailed", "Initial Deep Scan did not complete and will retry next launch: {0}", exception.Message);
            AddAlert("Warning", exception.Message);
        }
    }

    private Progress<LibraryScanProgress> CreateScanProgress(bool isDeepScan) =>
        new Progress<LibraryScanProgress>(progress =>
        {
            var label = isDeepScan ? LanguageAppearance.Get("GamesPage_DeepScan", "Deep Scan") : LanguageAppearance.Get("Linux_GuiRemainingFastScan", "Fast Scan");
            _viewModel.StatusText =
                LanguageAppearance.Format("Linux_ScanProgress", "{0}: {1:N0} / {2:N0} game roots processed…", label, progress.ProcessedGames, progress.TotalGames);
        });

    private async Task ReplaceLibraryScansAsync(PersistentLibrary library, IReadOnlyList<ScanResult> scans)
    {
        IReadOnlyList<ScanResult> previous = [];
        try { previous = DiscoverySnapshot.ReadScans(library); }
        catch (Exception error) { AddAlert("Warning", LanguageAppearance.Format("Linux_ScanLoadFailed", "Saved scan results could not be loaded: {0}", error.Message)); }
        try { DiscoverySnapshot.SaveScans(library, scans); }
        catch (Exception error) { AddAlert("Warning", LanguageAppearance.Format("Linux_ScanSaveFailed", "Scan results could not be saved: {0}", error.Message)); }
        await ReplaceScansAsync(DiscoverySnapshot.ReconcileScans(previous, scans));
    }

    private async Task ReplaceScansAsync(IReadOnlyList<ScanResult> scans)
    {
        var token = _lifetime.Token;
        ++_publicationGeneration;
        await _viewPublication;
        token.ThrowIfCancellationRequested();
        var selectedRoots = _allRows.Where(row => row.IsSelected).Select(row => row.RootPath).ToHashSet(PathComparer);
        _preparingRows = true;
        _viewModel.IsPublishingView = true;
        try
        {
            _rowPreparation = PrepareRowsAsync();
            await _rowPreparation;

            async Task PrepareRowsAsync()
            {
                await RunUiBatchesAsync(_allRows.ToArray(), row =>
                {
                    row.PropertyChanged -= GameRow_PropertyChanged;
                    row.ClearArtwork();
                });
                _viewModel.Games.Clear();
                _allRows.Clear();
                _knownPaths.Clear();
                await RunUiBatchesAsync(scans, scan =>
                {
                    if (!AddGame(scan.Game)) return;
                    _allRows[^1].SetScanResult(scan);
                    _allRows[^1].IsSelected = selectedRoots.Contains(_allRows[^1].RootPath);
                });
            }
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            _preparingRows = false;
            _viewModel.IsPublishingView = false;
        }

        _viewModel.GameCount = _allRows.Count;
        _viewModel.SelectedCount = _allRows.Count(row => row.IsSelected);
        ApplyGameView();
        await _viewPublication;
    }

    private void ApplyGameView()
    {
        if (_preparingRows || _lifetime.IsCancellationRequested) return;
        IEnumerable<GameRowViewModel> rows = _allRows.Where(row => _library is null || LibrarySelection.Includes(_library.State, row.Game));
        var hideEmpty = _library?.State.HideNonSwappableGames ?? true;
        if (this.FindControl<MenuItem>("SwappableFilterMenu") is { } option)
            option.Header = hideEmpty ? LanguageAppearance.Get("Linux_MainWindow_95", "Show games without swappable items") : LanguageAppearance.Get("Linux_MainWindow_94", "Hide games without swappable items");
        if (hideEmpty) rows = rows.Where(row => row.ScanResult is { } scan && (scan.Dlls.Count > 0 || scan.StreamlineFiles.Count > 0 || scan.Warnings.Count > 0));
        var search = _searchTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows.Where(row => row.Name.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || row.RootPath.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var manualPaths = _library?.State.ManualGames
            .Select(game => game.RootPath)
            .ToHashSet(PathComparer)
            ?? new HashSet<string>(PathComparer);
        rows = rows.Where(row => GameViewPolicy.MatchesHidden(row.IsHidden, false, _includeHidden));

        rows = _sortMode switch
        {
            1 => rows.OrderByDescending(GetHighestVersion, DlssSwapper.Shared.VersionTextComparer.Instance)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            2 => rows.OrderBy(GetHighestVersion, DlssSwapper.Shared.VersionTextComparer.Instance)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
        };

        var visible = rows.ToArray();
        var grouped = _library?.State.GroupGameLibrariesTogether ?? true;
        var groups = DlssSwapper.Shared.GameGrouping.Build(visible, row => row.IsFavorite,
            row => GameViewPolicy.LibraryName(row.Game,
                manualPaths.Contains(row.RootPath)), grouped, _library is null ? null : LibrarySelection.Read(_library.State).Select(entry => entry.Id).ToArray());
        _viewPublication = PublishViewAsync(visible, groups);
    }

    private static string GetHighestVersion(GameRowViewModel row) =>
        row.ScanResult?.Dlls
            .Where(dll => dll.Type == DllType.Dlss)
            .Select(dll => dll.Version)
            .OrderByDescending(version => version, DlssSwapper.Shared.VersionTextComparer.Instance)
            .FirstOrDefault()
        ?? string.Empty;

    private void StartArtworkHydration()
    {
        if (_artworkService is null || _library is null || _allRows.Count == 0)
        {
            return;
        }

        _artworkCancellation?.Cancel();
        _artworkCancellation?.Dispose();
        _artworkCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime.Token);
        _artworkTask = HydrateArtworkAsync(
            _allRows.ToArray(),
            _artworkService,
            _library,
            _artworkCancellation.Token);
    }

    private async Task HydrateArtworkAsync(
        IReadOnlyList<GameRowViewModel> rows,
        ArtworkService service,
        PersistentLibrary library,
        CancellationToken cancellationToken)
    {
        var resolvedMappings = new ConcurrentDictionary<string, string>(PathComparer);
        var warnings = new ConcurrentBag<(string Game, string Message)>();
        try
        {
            await Parallel.ForEachAsync(
                rows,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = library.State.Performance.ArtworkConcurrency,
                },
                async (row, token) =>
                {
                    var customArtworkPath = library.FindGamePreference(row.RootPath)?
                        .CustomArtworkPath;
                    if (customArtworkPath is not null && File.Exists(customArtworkPath))
                    {
                        var error = await Dispatcher.UIThread.InvokeAsync(
                            () => library.FindGamePreference(row.RootPath)?.CustomArtworkPath == customArtworkPath
                                ? TrySetArtwork(row, customArtworkPath) : null);
                        if (error is not null)
                        {
                            warnings.Add((row.Name, error));
                        }
                        return;
                    }

                    var result = await service.ResolveAsync(
                        row.Game,
                        library.State,
                        _lastSteamGames,
                        token).ConfigureAwait(false);
                    if (result.ResolvedSteamAppId is not null
                        && row.Game.SteamAppId is null)
                    {
                        resolvedMappings[row.RootPath] = result.ResolvedSteamAppId;
                    }

                    if (result.Warning is not null)
                    {
                        warnings.Add((row.Name, result.Warning));
                    }

                    if (result.Path is not null && File.Exists(result.Path))
                    {
                        var error = await Dispatcher.UIThread.InvokeAsync(
                            () => library.FindGamePreference(row.RootPath)?.CustomArtworkPath == customArtworkPath
                                ? TrySetArtwork(row, result.Path) : null);
                        if (error is not null)
                        {
                            warnings.Add((row.Name, error));
                        }
                    }
                });

            if (!resolvedMappings.IsEmpty)
            {
                library.UpdateState(state =>
                {
                    foreach (var manualGame in state.ManualGames)
                    {
                        if (resolvedMappings.TryGetValue(manualGame.RootPath, out var appId)
                            && manualGame.SteamAppId != appId)
                        {
                            manualGame.SteamAppId = appId;
                        }
                    }
                });
            }

            if (!warnings.IsEmpty)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var warning in warnings)
                    {
                        AddAlert("Warning", warning.Message);
                    }
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A refresh or shutdown replaced this hydration queue.
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => AddAlert("Warning", exception.Message));
        }
    }

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        _viewModel.IsBusy = true;
        _viewModel.StatusText = status;
        AppLog.Write(ApplicationLogLevel.Info, status);
        try
        {
            await action();
            AppLog.Write(ApplicationLogLevel.Debug, "Operation completed: " + status);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The window is closing.
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage23", "Operation failed: {0}", exception.Message);
            AddAlert("Error", exception.Message);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private async Task<IReadOnlyList<(GameRowViewModel Row, ScanResult Scan)>> ScanRowsAsync(
        IReadOnlyList<GameRowViewModel> rows,
        DllCatalog catalog)
    {
        if (_library is null)
        {
            return await Task.Run(() => rows
                .Select(row => (row, _scanner.Scan(row.Game, catalog)))
                .ToArray());
        }

        var service = _scanService ?? new LibraryScanService(catalog, _scanner);
        var result = await service.ScanFastAsync(
            rows.Select(row => row.Game).ToArray(),
            _library.State,
            cancellationToken: _lifetime.Token);
        try { DiscoverySnapshot.SaveScans(_library, result.Games); }
        catch (Exception error) { AddAlert("Warning", LanguageAppearance.Format("Linux_ScanSaveFailed", "Scan results could not be saved: {0}", error.Message)); }
        var byPath = result.Games.ToDictionary(
            scan => scan.Game.RootPath,
            PathComparer);
        return rows
            .Where(row => byPath.ContainsKey(row.RootPath))
            .Select(row => (row, byPath[row.RootPath]))
            .ToArray();
    }

    private bool AddGame(SelectedGame game)
    {
        var normalizedPath = NormalizeExistingDirectory(game.RootPath, allowMissing: true);
        if (!_knownPaths.Add(normalizedPath))
        {
            return false;
        }

        var normalizedGame = game with { RootPath = normalizedPath };
        var row = new GameRowViewModel(normalizedGame);
        row.SetCardSize(_viewModel.GridItemWidth, _viewModel.GridItemHeight);
        if (_library is not null)
        {
            var preference = _library.FindGamePreference(normalizedPath);
            if (preference is not null)
            {
                row.ApplyPreference(preference);
            }
            if (preference?.CustomArtworkPath is not null
                && File.Exists(preference.CustomArtworkPath))
            {
                var error = TrySetArtwork(row, preference.CustomArtworkPath);
                if (error is not null)
                {
                    AddAlert("Warning", error);
                }
            }
        }
        row.PropertyChanged += GameRow_PropertyChanged;
        _allRows.Add(row);
        return true;
    }

    private void GameRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameRowViewModel.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    private void ShowOperationFailures(IReadOnlyList<OperationResult> results)
    {
        foreach (var result in results.Where(result => !result.Success))
        {
            AddAlert("Failed", result.Message);
        }
    }

    private void AddAlert(string outcome, string message)
    {
        AppLog.Write(outcome is "Error" or "Failed" ? ApplicationLogLevel.Error :
            outcome == "Warning" ? ApplicationLogLevel.Warning : ApplicationLogLevel.Info, message);
        if (outcome.Equals("Warning", StringComparison.OrdinalIgnoreCase)
            || outcome.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.AlertText = message;
        }
    }

    private static string? TrySetArtwork(GameRowViewModel row, string path)
    {
        try
        {
            row.SetArtwork(path);
            return null;
        }
        catch (Exception exception)
        {
            return LanguageAppearance.Format("Linux_GuiRemainingArtworkOpen", "Could not open artwork for {0}: {1}", row.Name, exception.Message);
        }
    }

    private bool TryGetSelectedRows(out GameRowViewModel[] rows)
    {
        rows = _viewModel.Games.Where(game => game.IsSelected && (_library is null || LibrarySelection.Includes(_library.State, game.Game))).ToArray();
        if (rows.Length != 0)
        {
            return true;
        }

        _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage24", "Select at least one game first.");
        return false;
    }

    private static bool TryGetMenuGame(object? sender, out GameRowViewModel row)
    {
        row = null!;
        if (sender is Control { Tag: GameRowViewModel taggedRow })
        {
            row = taggedRow;
            return true;
        }

        return false;
    }

    private async Task ReloadGameAsync(
        GameRowViewModel row,
        DllCatalog catalog,
        bool recordHistory)
    {
        await RunBusyAsync(LanguageAppearance.Format("Linux_GamesOperation13", "Reloading {0}…", row.Name), async () =>
        {
            var scans = await ScanRowsAsync([row], catalog);
            if (scans.Count == 0)
            {
                _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage25", "{0} is no longer available for Fast Scan.", row.Name);
                return;
            }

            row.SetScanResult(scans[0].Scan);
            if (recordHistory && _library is not null)
            {
                var summaries = scans[0].Scan.Dlls
                    .GroupBy(dll => dll.Type)
                    .Select(group => new
                    {
                        Family = DllTypes.Get(group.Key).DisplayName,
                        Version = string.Join(", ", group
                            .Select(dll => dll.Version)
                            .Distinct(StringComparer.OrdinalIgnoreCase)),
                    })
                    .ToArray();
                foreach (var summary in summaries)
                {
                    _library.RecordHistory(
                        row.RootPath,
                        "DLL detected",
                        summary.Family,
                        summary.Version);
                }
            }

            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesOperation14", "Reloaded {0}; found {1} supported DLL file{2}.", row.Name, scans[0].Scan.Dlls.Count, Plural(scans[0].Scan.Dlls.Count));
        });
    }

    private static string RemovalNotice => LanguageAppearance.Get("Linux_RemovalNotice", "Launcher games are excluded; their notes, preferences and history are kept. Manually added entries and their saved details/history are removed. Game files are not deleted.");

    private static void RemoveGameFromLibrary(
        GameRowViewModel row,
        PersistentLibrary library) => LibraryRemovalWorkflow.Remove(library, row.Game);

    private bool TryGetCatalog(out DllCatalog catalog)
    {
        catalog = _catalog!;
        if (_catalog is not null)
        {
            return true;
        }

        _viewModel.StatusText =
            LanguageAppearance.Get("Linux_CatalogUnavailable", "The DLL catalog is unavailable. Verify Assets/static_manifest.json and restart.");
        return false;
    }

    private bool TryGetLibrary(out PersistentLibrary library)
    {
        library = _library!;
        if (_library is not null)
        {
            return true;
        }

        _viewModel.StatusText =
            LanguageAppearance.Get("Linux_LibraryUnavailable", "The persistent Linux library state is unavailable. Review the startup error.");
        return false;
    }

    private bool TryGetScanService(out LibraryScanService service)
    {
        service = _scanService!;
        if (_scanService is not null)
        {
            return true;
        }

        _viewModel.StatusText =
            LanguageAppearance.Get("Linux_ScannerUnavailable", "The Linux scan service is unavailable. Verify the DLL catalog and restart.");
        return false;
    }

    private TextBox FindRequiredTextBox(string name) =>
        this.FindControl<TextBox>(name)
        ?? throw new InvalidOperationException($"Required text box '{name}' is missing.");

    private static string[] ParseLines(string? value) =>
        (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static string NormalizeExistingDirectory(string value, bool allowMissing = false)
    {
        var expanded = ExpandHome(value.Trim());
        var fullPath = Path.GetFullPath(expanded);
        if (IsFileSystemRoot(fullPath))
        {
            throw new ArgumentException(LanguageAppearance.Format("Linux_GuiRemainingRootRejected", "Filesystem roots cannot be selected: {0}", value));
        }

        if (!Directory.Exists(fullPath))
        {
            if (allowMissing) return Path.TrimEndingDirectorySeparator(fullPath);
            throw new DirectoryNotFoundException(LanguageAppearance.Format("Linux_GuiRemainingDirectoryMissing", "Directory does not exist: {0}", value));
        }

        var directory = new DirectoryInfo(fullPath);
        directory = directory.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo
            ?? directory;
        if (IsFileSystemRoot(directory.FullName))
        {
            throw new ArgumentException(LanguageAppearance.Format("Linux_GuiRemainingRootRejected", "Filesystem roots cannot be selected: {0}", value));
        }

        return Path.TrimEndingDirectorySeparator(directory.FullName);
    }

    private static bool IsFileSystemRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is not null
            && PathComparer.Equals(
                Path.TrimEndingDirectorySeparator(path),
                Path.TrimEndingDirectorySeparator(root));
    }

    private static string ExpandHome(string value)
    {
        if (value != "~" && !value.StartsWith("~/", StringComparison.Ordinal))
        {
            return value;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME")
                ?? throw new InvalidOperationException("Could not locate the home directory.");
        }

        return value.Length == 1 ? home : Path.Combine(home, value[2..]);
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private enum ManualImportKind
    {
        Single,
        Multiple,
        Parent,
    }

}
