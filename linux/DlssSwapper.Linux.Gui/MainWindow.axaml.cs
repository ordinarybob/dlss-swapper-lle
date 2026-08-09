using System.ComponentModel;
using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HttpClient _artworkHttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(2),
    };
    private readonly TextBox _steamRootsTextBox;
    private readonly TextBox _explicitPathsTextBox;
    private readonly TextBox _gameRootsTextBox;

    private DllCatalog? _catalog;
    private PersistentLibrary? _library;
    private LibraryScanService? _scanService;
    private ArtworkService? _artworkService;
    private IReadOnlyList<UpdatePlanItem> _currentUpdatePlan = [];
    private IReadOnlyList<SteamGame> _lastSteamGames = [];
    private CancellationTokenSource? _artworkCancellation;
    private Task? _artworkTask;
    private Task? _deepScanTask;
    private bool _opened;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = _viewModel;

        _steamRootsTextBox = FindRequiredTextBox("SteamRootsTextBox");
        _explicitPathsTextBox = FindRequiredTextBox("ExplicitPathsTextBox");
        _gameRootsTextBox = FindRequiredTextBox("GameRootsTextBox");

        try
        {
            var manifestPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "static_manifest.json");
            _catalog = DllCatalog.Load(manifestPath);
            _library = new PersistentLibrary(new LibraryStateStore());
            _scanService = new LibraryScanService(_catalog, _scanner);
            _artworkHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "DLSS-Swapper-LLE-Linux/1.0");
            _artworkService = new ArtworkService(
                _artworkHttpClient,
                new AvaloniaArtworkImageProcessor());
            _steamRootsTextBox.Text = string.Join(
                Environment.NewLine,
                _library.State.AdditionalSteamRoots);
            _viewModel.StatusText = "Loading the persistent game library…";
        }
        catch (Exception exception)
        {
            _viewModel.StatusText =
                $"The bundled DLL catalog could not be loaded: {exception.Message}";
            AddResult("Application", "Catalog", "Assets/static_manifest.json", "Error", exception.Message);
        }

        Opened += MainWindow_Opened;
        Closing += (_, _) =>
        {
            _lifetime.Cancel();
            _artworkCancellation?.Cancel();
            if (_artworkTask?.IsFaulted == true)
            {
                _ = _artworkTask.Exception;
            }
            _artworkHttpClient.Dispose();
        };
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (_opened)
        {
            return;
        }

        _opened = true;
        await RefreshLibraryAsync(runInitialDeepScan: true);
    }

    private async void DiscoverSteam_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetLibrary(out var library))
        {
            return;
        }

        library.State.AdditionalSteamRoots = ParseLines(_steamRootsTextBox.Text).ToList();
        library.Save();
        await RefreshLibraryAsync(runInitialDeepScan: false);
    }

    private async void AddPaths_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetLibrary(out var library))
        {
            return;
        }

        var explicitPaths = ParseLines(_explicitPathsTextBox.Text);
        var roots = ParseLines(_gameRootsTextBox.Text);
        InvalidatePreview();
        await RunBusyAsync("Adding explicit game paths…", async () =>
        {
            var added = 0;
            var warnings = new List<string>();
            await Task.Run(() =>
            {
                foreach (var path in explicitPaths)
                {
                    try
                    {
                        added += library.AddManualGames([path]);
                    }
                    catch (Exception exception) when (exception is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException)
                    {
                        warnings.Add($"Could not add game path '{path}': {exception.Message}");
                    }
                }

                foreach (var root in roots)
                {
                    try
                    {
                        added += library.AddImmediateChildren(root);
                    }
                    catch (Exception exception) when (exception is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException)
                    {
                        warnings.Add($"Could not expand game root '{root}': {exception.Message}");
                    }
                }
            });

            _viewModel.Results.Clear();
            foreach (var warning in warnings)
            {
                AddResult("Path import", "Filesystem", "—", "Warning", warning);
            }

            _viewModel.StatusText =
                $"Persisted {added} new game path{Plural(added)}. Multi-game roots include immediate children only.";
        });
        await RefreshLibraryAsync(runInitialDeepScan: false);
    }

    private async void DeepScan_Click(object? sender, RoutedEventArgs e)
    {
        if (_deepScanTask is { IsCompleted: false })
        {
            _viewModel.StatusText = "Deep Scan is already running in the background.";
            return;
        }

        var confirmed = await new ConfirmationDialog(
            "Deep Scan",
            "Deep Scan runs automatically the first time you launch DLSS Swapper LLE and learns path patterns for Fast Scan. "
            + "Run it again only after your library changes and a game is missing. If you know which game is missing, use Add paths and roots instead.")
            .ShowDialog<bool>(this);
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
        await RunBusyAsync("Deep Scan is checking every game directory…", async () =>
        {
            var result = await service.ScanDeepAsync(
                games,
                library,
                CreateScanProgress(isDeepScan: true),
                _lifetime.Token);
            ReplaceScans(result.Games);
            StartArtworkHydration();
            _viewModel.StatusText =
                $"Deep Scan found {_viewModel.GameCount} swappable game{Plural(_viewModel.GameCount)} and learned {result.LearnedPatternCount} new fast-scan pattern{Plural(result.LearnedPatternCount)}.";
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
            "Remove games",
            $"Remove {rows.Length} selected game{Plural(rows.Length)} from this library? Steam games remain excluded during normal refresh; manually added games can be imported again.")
            .ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        var manualPaths = library.State.ManualGames
            .Select(game => game.RootPath)
            .ToHashSet(PathComparer);
        foreach (var row in rows)
        {
            if (manualPaths.Contains(row.RootPath))
            {
                library.RemoveManualGame(row.RootPath);
            }
            else if (row.Game.SteamAppId is not null)
            {
                library.ExcludeSteamGame(row.Game.SteamAppId);
            }
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
        _viewModel.StatusText = $"Restored {restored} excluded Steam game{Plural(restored)}.";
        await RefreshLibraryAsync(runInitialDeepScan: false);
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetLibrary(out var library))
        {
            return;
        }

        var saved = await new SettingsWindow(library).ShowDialog<bool>(this);
        if (saved)
        {
            _steamRootsTextBox.Text = string.Join(
                Environment.NewLine,
                library.State.AdditionalSteamRoots);
            _viewModel.StatusText = library.State.HddMode
                ? "Settings saved. HDD scan and artwork limits are active."
                : "Settings saved. Standard scan and artwork limits are active.";
            await RefreshLibraryAsync(runInitialDeepScan: false);
        }
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var game in _viewModel.Games)
        {
            game.IsSelected = true;
        }
    }

    private void SelectNone_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var game in _viewModel.Games)
        {
            game.IsSelected = false;
        }
    }

    private async void ScanSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedRows(out var rows) || !TryGetCatalog(out var catalog))
        {
            return;
        }

        InvalidatePreview();
        await RunBusyAsync($"Scanning {rows.Length} selected game{Plural(rows.Length)}…", async () =>
        {
            var scans = await ScanRowsAsync(rows, catalog);
            _viewModel.Results.Clear();
            var warningCount = 0;
            foreach (var (row, scan) in scans)
            {
                row.SetScanResult(scan);
                foreach (var warning in scan.Warnings)
                {
                    warningCount++;
                    AddResult(row.Name, "Scan", row.RootPath, "Warning", warning);
                }
            }

            var dllCount = scans.Sum(item => item.Scan.Dlls.Count);
            _viewModel.StatusText =
                $"Scanned {rows.Length} game{Plural(rows.Length)} and found {dllCount} supported DLL file{Plural(dllCount)}"
                + (warningCount == 0 ? "." : $" with {warningCount} warning{Plural(warningCount)}.");
        });
    }

    private async void PreviewLatest_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedRows(out var rows) || !TryGetCatalog(out var catalog))
        {
            return;
        }

        var unscanned = rows.Where(row => row.ScanResult is null).ToArray();
        if (unscanned.Length > 0)
        {
            _viewModel.StatusText =
                $"Scan all selected games first. {unscanned.Length} selected game{Plural(unscanned.Length)} remain unscanned.";
            return;
        }

        await RunBusyAsync("Planning detected-family latest updates (dry run)…", () =>
        {
            var scans = rows.Select(row => row.ScanResult!).ToArray();
            var candidates = scans
                .SelectMany(scan => scan.Dlls)
                .Select(dll => dll.Type)
                .Distinct()
                .ToDictionary(type => type, catalog.GetLatest);

            _currentUpdatePlan = _planner.Plan(scans, candidates);
            _viewModel.Results.Clear();
            foreach (var item in _currentUpdatePlan)
            {
                if (item.Status == UpdatePlanStatus.Ready)
                {
                    foreach (var target in item.Targets)
                    {
                        AddResult(
                            item.Game.Name,
                            item.Family.DisplayName,
                            target.RelativePath,
                            "Would update",
                            $"{target.Version} → {item.Candidate.Version}");
                    }
                }
                else
                {
                    AddResult(
                        item.Game.Name,
                        item.Family.DisplayName,
                        item.Game.RootPath,
                        item.Status == UpdatePlanStatus.AlreadyCurrent ? "Current" : "Skipped",
                        item.Message);
                }
            }

            var readyTargets = _currentUpdatePlan
                .Where(item => item.Status == UpdatePlanStatus.Ready)
                .Sum(item => item.Targets.Count);
            _viewModel.HasReadyPreview = readyTargets > 0;
            _viewModel.StatusText = readyTargets == 0
                ? "Dry run complete. No selected DLL files need updating."
                : $"Dry run complete. {readyTargets} DLL file{Plural(readyTargets)} would be updated; no files were downloaded or written.";
            return Task.CompletedTask;
        });
    }

    private async void ApplyPreview_Click(object? sender, RoutedEventArgs e)
    {
        var planToApply = _currentUpdatePlan;
        var readyPlan = planToApply
            .Where(item => item.Status == UpdatePlanStatus.Ready)
            .ToArray();
        var targetCount = readyPlan.Sum(item => item.Targets.Count);
        if (targetCount == 0)
        {
            _viewModel.StatusText = "Create a dry-run preview before applying updates.";
            return;
        }

        var gameCount = readyPlan.Select(item => item.Game.RootPath)
            .Distinct(PathComparer)
            .Count();
        var confirmed = await new ConfirmationDialog(
            "Confirm DLL update",
            $"Update {targetCount} DLL file{Plural(targetCount)} in {gameCount} game{Plural(gameCount)}? "
            + "An adjacent .dlsss backup is created when one does not already exist.")
            .ShowDialog<bool>(this);
        if (!confirmed)
        {
            _viewModel.StatusText = "Update cancelled; the dry-run preview remains available.";
            return;
        }

        InvalidatePreview();
        await RunBusyAsync("Downloading verified DLLs and applying the preview…", async () =>
        {
            using var cache = new DownloadCache();
            var results = await Task.Run(() => DllOperations.ApplyUpdatesAsync(
                planToApply,
                cache,
                CancellationToken.None));
            ShowOperationResults(results);

            if (_catalog is not null)
            {
                var affectedRows = _viewModel.Games
                    .Where(row => readyPlan.Any(item =>
                        PathComparer.Equals(item.Game.RootPath, row.RootPath)))
                    .ToArray();
                foreach (var (row, scan) in await ScanRowsAsync(affectedRows, _catalog))
                {
                    row.SetScanResult(scan);
                }
            }

            var succeeded = results.Count(result => result.Success);
            var failed = results.Count - succeeded;
            _viewModel.StatusText =
                $"Update finished: {succeeded} succeeded, {failed} failed. Review every result.";
        });
    }

    private async void RestoreBackups_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedRows(out var rows))
        {
            return;
        }

        InvalidatePreview();
        await RunBusyAsync("Finding restorable .dlsss backups…", async () =>
        {
            var plan = await Task.Run(() => rows
                .SelectMany(row => _scanner.PlanRestore(row.Game))
                .ToArray());
            if (plan.Length == 0)
            {
                _viewModel.Results.Clear();
                _viewModel.StatusText = "No supported .dlsss backups were found in the selected games.";
                return;
            }

            _viewModel.Results.Clear();
            foreach (var item in plan)
            {
                AddResult(
                    item.Game.Name,
                    item.Family.DisplayName,
                    item.RelativeTargetPath,
                    "Would restore",
                    "Restore the adjacent .dlsss backup.");
            }

            _viewModel.IsBusy = false;
            var confirmed = await new ConfirmationDialog(
                "Confirm DLL restore",
                $"Restore {plan.Length} DLL backup{Plural(plan.Length)}? "
                + "Each .dlsss backup will replace its corresponding DLL and then be consumed.")
                .ShowDialog<bool>(this);
            _viewModel.IsBusy = true;
            if (!confirmed)
            {
                _viewModel.StatusText = "Restore cancelled; no files were written.";
                return;
            }

            _viewModel.StatusText = "Restoring selected DLL backups…";
            var results = await Task.Run(() => DllOperations.ApplyRestores(plan));
            ShowOperationResults(results);

            if (_catalog is not null)
            {
                foreach (var (row, scan) in await ScanRowsAsync(rows, _catalog))
                {
                    row.SetScanResult(scan);
                }
            }

            var succeeded = results.Count(result => result.Success);
            var failed = results.Count - succeeded;
            InvalidatePreview();
            _viewModel.StatusText =
                $"Restore finished: {succeeded} succeeded, {failed} failed. Review every result.";
        });
    }

    private async Task RefreshLibraryAsync(bool runInitialDeepScan)
    {
        if (!TryGetLibrary(out var library)
            || !TryGetScanService(out var service))
        {
            return;
        }

        IReadOnlyList<SelectedGame> games = [];
        var succeeded = false;
        InvalidatePreview();
        _viewModel.IsLoadingLibrary = true;
        await RunBusyAsync("Discovering and fast-scanning the game library…", async () =>
        {
            games = await DiscoverMergedGamesAsync(library);
            var result = await service.ScanFastAsync(
                games,
                library.State,
                CreateScanProgress(isDeepScan: false),
                _lifetime.Token);
            ReplaceScans(result.Games);
            StartArtworkHydration();
            _viewModel.IsLoadingLibrary = false;
            _viewModel.StatusText =
                $"Fast Scan loaded {_viewModel.GameCount} swappable game{Plural(_viewModel.GameCount)} in {result.Elapsed.TotalSeconds:F2} seconds.";
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
            _deepScanTask = RunInitialDeepScanAsync(games, library, service);
        }
    }

    private async Task<IReadOnlyList<SelectedGame>> DiscoverMergedGamesAsync(
        PersistentLibrary library)
    {
        var discovery = await Task.Run(
            () => _steamDiscovery.Discover(new SteamDiscoveryOptions
            {
                AdditionalRoots = library.State.AdditionalSteamRoots,
            }),
            _lifetime.Token);
        _lastSteamGames = discovery.Games;
        foreach (var warning in discovery.Warnings)
        {
            AddResult("Steam discovery", "Steam", "—", "Warning", warning);
        }

        return library.Merge(discovery);
    }

    private async Task RunInitialDeepScanAsync(
        IReadOnlyList<SelectedGame> games,
        PersistentLibrary library,
        LibraryScanService service)
    {
        try
        {
            _viewModel.StatusText =
                $"Fast Scan is ready with {_viewModel.GameCount} game{Plural(_viewModel.GameCount)}. Initial Deep Scan is learning any missing layouts in the background…";
            var result = await service.ScanDeepAsync(
                games,
                library,
                CreateScanProgress(isDeepScan: true),
                _lifetime.Token);
            ReplaceScans(result.Games);
            StartArtworkHydration();
            _viewModel.StatusText =
                $"Initial Deep Scan completed in {result.Elapsed.TotalSeconds:F2} seconds; learned {result.LearnedPatternCount} new fast-scan pattern{Plural(result.LearnedPatternCount)}.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Completion remains false, so the next launch retries.
        }
        catch (Exception exception)
        {
            _viewModel.StatusText =
                $"Initial Deep Scan did not complete and will retry next launch: {exception.Message}";
            AddResult("Library", "Deep Scan", "—", "Warning", exception.Message);
        }
    }

    private IProgress<LibraryScanProgress> CreateScanProgress(bool isDeepScan) =>
        new Progress<LibraryScanProgress>(progress =>
        {
            var label = isDeepScan ? "Deep Scan" : "Fast Scan";
            _viewModel.StatusText =
                $"{label}: {progress.ProcessedGames:N0} / {progress.TotalGames:N0} game roots processed…";
        });

    private void ReplaceScans(IReadOnlyList<ScanResult> scans)
    {
        foreach (var row in _viewModel.Games)
        {
            row.PropertyChanged -= GameRow_PropertyChanged;
        }

        _viewModel.Games.Clear();
        _knownPaths.Clear();
        foreach (var scan in scans.Where(scan => scan.Dlls.Count > 0))
        {
            if (!AddGame(scan.Game))
            {
                continue;
            }

            _viewModel.Games[^1].SetScanResult(scan);
        }

        _viewModel.GameCount = _viewModel.Games.Count;
    }

    private void StartArtworkHydration()
    {
        if (_artworkService is null || _library is null || _viewModel.Games.Count == 0)
        {
            return;
        }

        _artworkCancellation?.Cancel();
        _artworkCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime.Token);
        _artworkTask = HydrateArtworkAsync(
            _viewModel.Games.ToArray(),
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
                        await Dispatcher.UIThread.InvokeAsync(() => row.SetArtwork(result.Path));
                    }
                });

            if (resolvedMappings.Count > 0)
            {
                var changed = false;
                foreach (var manualGame in library.State.ManualGames)
                {
                    if (resolvedMappings.TryGetValue(manualGame.RootPath, out var appId)
                        && manualGame.SteamAppId != appId)
                    {
                        manualGame.SteamAppId = appId;
                        changed = true;
                    }
                }

                if (changed)
                {
                    library.Save();
                }
            }

            if (!warnings.IsEmpty)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var warning in warnings)
                    {
                        AddResult(
                            warning.Game,
                            "Artwork",
                            "—",
                            "Warning",
                            warning.Message);
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
            await Dispatcher.UIThread.InvokeAsync(() => AddResult(
                "Library",
                "Artwork",
                "—",
                "Warning",
                exception.Message));
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
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = $"Operation failed: {exception.Message}";
            AddResult("Application", "Operation", "—", "Error", exception.Message);
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
        var normalizedPath = NormalizeExistingDirectory(game.RootPath);
        if (!_knownPaths.Add(normalizedPath))
        {
            return false;
        }

        var normalizedGame = game with { RootPath = normalizedPath };
        var row = new GameRowViewModel(normalizedGame);
        row.PropertyChanged += GameRow_PropertyChanged;
        _viewModel.Games.Add(row);
        InvalidatePreview();
        return true;
    }

    private void GameRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameRowViewModel.IsSelected))
        {
            InvalidatePreview();
        }
    }

    private void InvalidatePreview()
    {
        _currentUpdatePlan = [];
        _viewModel.HasReadyPreview = false;
    }

    private void ShowOperationResults(IReadOnlyList<OperationResult> results)
    {
        _viewModel.Results.Clear();
        foreach (var result in results)
        {
            AddResult(
                result.Game.Name,
                result.Family,
                result.Target,
                result.Success ? "Success" : "Failed",
                result.Message);
        }
    }

    private void AddResult(
        string game,
        string family,
        string target,
        string outcome,
        string message)
    {
        _viewModel.Results.Add(new ResultRowViewModel(
            game,
            family,
            target,
            outcome,
            message));
    }

    private bool TryGetSelectedRows(out GameRowViewModel[] rows)
    {
        rows = _viewModel.Games.Where(game => game.IsSelected).ToArray();
        if (rows.Length != 0)
        {
            return true;
        }

        _viewModel.StatusText = "Select at least one game first.";
        return false;
    }

    private bool TryGetCatalog(out DllCatalog catalog)
    {
        catalog = _catalog!;
        if (_catalog is not null)
        {
            return true;
        }

        _viewModel.StatusText =
            "The DLL catalog is unavailable. Verify Assets/static_manifest.json and restart.";
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
            "The persistent Linux library state is unavailable. Review the startup error.";
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
            "The Linux scan service is unavailable. Verify the DLL catalog and restart.";
        return false;
    }

    private TextBox FindRequiredTextBox(string name) =>
        this.FindControl<TextBox>(name)
        ?? throw new InvalidOperationException($"Required text box '{name}' is missing.");

    private static IReadOnlyList<string> ParseLines(string? value) =>
        (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static string NormalizeExistingDirectory(string value)
    {
        var expanded = ExpandHome(value.Trim());
        var fullPath = Path.GetFullPath(expanded);
        if (IsFileSystemRoot(fullPath))
        {
            throw new ArgumentException($"Filesystem roots cannot be selected: {value}");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory does not exist: {value}");
        }

        var directory = new DirectoryInfo(fullPath);
        directory = directory.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo
            ?? directory;
        if (IsFileSystemRoot(directory.FullName))
        {
            throw new ArgumentException($"Filesystem roots cannot be selected: {value}");
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

}
