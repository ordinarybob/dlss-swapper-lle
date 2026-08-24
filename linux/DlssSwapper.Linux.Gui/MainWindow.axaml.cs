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
    private readonly ComboBox _filterComboBox;
    private readonly ComboBox _sortComboBox;
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
    private bool _opened;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = _viewModel;

        _searchTextBox = FindRequiredTextBox("SearchTextBox");
        _filterComboBox = this.FindControl<ComboBox>("FilterComboBox")
            ?? throw new InvalidOperationException("Required filter control is missing.");
        _sortComboBox = this.FindControl<ComboBox>("SortComboBox")
            ?? throw new InvalidOperationException("Required sort control is missing.");
        _gridCardSizeInput = this.FindControl<ComboBox>("GridCardSizeInput")
            ?? throw new InvalidOperationException("Required grid-card-size control is missing.");
        _gameGridViewport = this.FindControl<ScrollViewer>("GameGridViewport")
            ?? throw new InvalidOperationException("Required game-grid viewport is missing.");
        _gridCardSizeInput.ItemsSource = Enumerable.Range(
            ResponsiveGridLayout.MinimumCardSize,
            ResponsiveGridLayout.MaximumCardSize
                - ResponsiveGridLayout.MinimumCardSize
                + 1).ToArray();

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
            _viewModel.IsGridView = _library.State.GridView;
            _viewModel.GridCardSize = _library.State.CardSize;
            _gridCardSizeInput.SelectedItem = _library.State.CardSize;
            _viewModel.StatusText = "Loading the persistent game library…";
        }
        catch (Exception exception)
        {
            _viewModel.StatusText =
                $"The bundled DLL catalog could not be loaded: {exception.Message}";
            AddAlert("Error", exception.Message);
        }

        Opened += MainWindow_Opened;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        _lifetime.Cancel();
        _artworkCancellation?.Cancel();
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            var backgroundTasks = new[] { _artworkTask, _deepScanTask }
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
        if (_library is { } library && !library.State.HasSelectedStorageProfile)
        {
            var hddMode = await new StorageProfileDialog().ShowDialog<bool>(this);
            library.UpdateState(state =>
            {
                state.HddMode = hddMode;
                state.HasSelectedStorageProfile = true;
            });
        }

        UpdateGridGeometry();
        await RefreshLibraryAsync(runInitialDeepScan: true);
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
                "Select a single games installation folder.",
                "Select Game Folder",
                "Select Game Folder"),
            ManualImportKind.Multiple => (
                library.State.SuppressMultipleFoldersNotice,
                "Select multiple separate game installation folders.",
                "Select Game Folders",
                "Select Game Folders"),
            ManualImportKind.Parent => (
                library.State.SuppressMultiGameDirectoryNotice,
                "Select the main folder where the games you want to add are installed. Each immediate child folder will be added as a separate manually added game. The parent folder itself is not added, and nested folders are not searched.",
                "Select Multi-Game Directory",
                "Select Multi-Game Directory"),
            _ => throw new InvalidOperationException("Unknown manual import kind."),
        };

        if (!suppressed)
        {
            var notice = await new ImportNoticeDialog(body, action)
                .ShowDialog<ImportNoticeResult>(this);
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
        var added = kind == ManualImportKind.Parent
            ? library.AddImmediateChildren(localPaths[0])
            : library.AddManualGames(localPaths);
        _viewModel.StatusText = $"Persisted {added} new game path{Plural(added)}.";
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
            + "Run it again only after your library changes and a game is missing. If you know which game is missing, add that game directly instead.")
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
            RemoveGameFromLibrary(row, library, manualPaths);
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

        var settings = new SettingsWindow(library);
        var saved = await settings.ShowDialog<bool>(this);
        if (saved)
        {
            _viewModel.GridCardSize = library.State.CardSize;
            _gridCardSizeInput.SelectedItem = library.State.CardSize;
            UpdateGridGeometry();
            _viewModel.StatusText = library.State.HddMode
                ? "Settings saved. HDD scan and artwork limits are active."
                : "Settings saved. Standard scan and artwork limits are active.";
            await RefreshLibraryAsync(runInitialDeepScan: settings.WasReset);
        }
    }

    private async void DllLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetCatalog(out var catalog))
        {
            return;
        }

        var selectedEntry = await new DllLibraryWindow(catalog)
            .ShowDialog<DllCatalogEntry?>(this);
        if (selectedEntry is null)
        {
            return;
        }

        if (!TryGetSelectedRows(out var rows))
        {
            _viewModel.StatusText = $"{DllTypes.Get(selectedEntry.Type).DisplayName} {selectedEntry.Version} is downloaded. Select games before applying it.";
            return;
        }

        var unscanned = rows.Where(row => row.ScanResult is null).ToArray();
        if (unscanned.Length > 0)
        {
            foreach (var (row, scan) in await ScanRowsAsync(unscanned, catalog))
            {
                row.SetScanResult(scan);
            }
        }

        var identityResolved = false;
        await RunBusyAsync(
            $"Resolving the exact {DllTypes.Get(selectedEntry.Type).DisplayName} build identity…",
            async () =>
            {
                var resolved = await Task.Run(() => rows
                    .Where(row => row.ScanResult is not null)
                    .Select(row =>
                    {
                        var scan = row.ScanResult!;
                        var dlls = scan.Dlls.Select(dll =>
                            dll.Type == selectedEntry.Type
                            && !dll.HasHash
                            && dll.Version.Equals(
                                selectedEntry.Version,
                                StringComparison.OrdinalIgnoreCase)
                                ? _scanner.ResolveIdentity(dll, catalog)
                                : dll).ToArray();
                        return (Row: row, Scan: scan with { Dlls = dlls });
                    })
                    .ToArray());
                foreach (var (row, scan) in resolved)
                {
                    row.SetScanResult(scan);
                }

                identityResolved = true;
            });
        if (!identityResolved)
        {
            return;
        }

        var plan = _planner.Plan(
            rows.Where(row => row.ScanResult is not null)
                .Select(row => row.ScanResult!)
                .ToArray(),
            new Dictionary<DllType, DllCatalogEntry>
            {
                [selectedEntry.Type] = selectedEntry,
            });
        var ready = plan.Where(item => item.Status == UpdatePlanStatus.Ready).ToArray();
        var targetCount = ready.Sum(item => item.Targets.Count);
        if (targetCount == 0)
        {
            var currentCount = plan.Count(item => item.Status == UpdatePlanStatus.AlreadyCurrent);
            var skippedCount = plan.Count(item => item.Status == UpdatePlanStatus.Skipped);
            _viewModel.StatusText =
                $"Exact-version plan is a no-op: {currentCount} current, {skippedCount} incompatible or indeterminate.";
            return;
        }

        var family = DllTypes.Get(selectedEntry.Type).DisplayName;
        var gameCount = ready.Select(item => item.Game.RootPath)
            .Distinct(PathComparer)
            .Count();
        var confirmed = await new ConfirmationDialog(
            "Confirm exact DLL version",
            $"Apply {family} {selectedEntry.Version} ({selectedEntry.Md5[..8]}) to {targetCount} detected DLL file{Plural(targetCount)} in {gameCount} selected game{Plural(gameCount)}?",
            ConfirmationDialog.GameFileWriteWarning)
            .ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        await RunBusyAsync($"Applying exact {family} {selectedEntry.Version}…", async () =>
        {
            using var cache = new DownloadCache();
            var results = await Task.Run(() => DllOperations.ApplyUpdatesAsync(
                plan,
                cache,
                _lifetime.Token));
            ShowOperationFailures(results);
            foreach (var result in results.Where(result => result.Success))
            {
                _library?.RecordHistory(
                    result.Game.RootPath,
                    "DLL updated",
                    family,
                    selectedEntry.Version,
                    result.Target);
            }

            var affectedRows = rows.Where(row => ready.Any(item =>
                PathComparer.Equals(item.Game.RootPath, row.RootPath))).ToArray();
            foreach (var (row, scan) in await ScanRowsAsync(affectedRows, catalog))
            {
                row.SetScanResult(scan);
            }

            var succeeded = results.Count(result => result.Success);
            var failed = results.Count - succeeded;
            _viewModel.StatusText =
                $"Exact-version update finished: {succeeded} succeeded, {failed} failed.";
        });
    }

    private void GameLaunch_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row))
        {
            return;
        }

        try
        {
            var installedSteamGame = _lastSteamGames.FirstOrDefault(game =>
                PathComparer.Equals(game.InstallDirectory, row.RootPath));
            var target = installedSteamGame is null
                ? row.RootPath
                : $"steam://rungameid/{installedSteamGame.AppId}";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
            _viewModel.StatusText = installedSteamGame is null
                ? $"Opened {row.Name}'s installation folder."
                : $"Sent {row.Name} to Steam.";
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = $"Could not launch {row.Name}: {exception.Message}";
        }
    }

    private async void GameNotes_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        var notes = await new GameNotesDialog(
            row.Name,
            library.FindGamePreference(row.RootPath)?.Notes)
            .ShowDialog<string?>(this);
        if (notes is null)
        {
            return;
        }

        library.UpdateGamePreference(
            row.RootPath,
            preference => preference.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes);
        _viewModel.StatusText = $"Saved notes for {row.Name}.";
    }

    private async void GameHistory_Click(object? sender, RoutedEventArgs e)
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
        await historyWindow.ShowDialog(this);
    }

    private void GameFavorite_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        var preference = library.UpdateGamePreference(
            row.RootPath,
            value => value.IsFavorite = !value.IsFavorite);
        row.ApplyPreference(preference);
        ApplyGameView();
        _viewModel.StatusText = preference.IsFavorite
            ? $"Added {row.Name} to favorites."
            : $"Removed {row.Name} from favorites.";
    }

    private async void GameReload_Click(object? sender, RoutedEventArgs e)
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

        var preference = library.UpdateGamePreference(
            row.RootPath,
            value => value.IsHidden = !value.IsHidden);
        row.ApplyPreference(preference);
        ApplyGameView();
        _viewModel.StatusText = preference.IsHidden
            ? $"Hid {row.Name}. Use the Hidden filter to show it again."
            : $"Restored {row.Name} to the library view.";
    }

    private async void GameCustomCover_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Select cover art for {row.Name}",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
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

        var path = files[0].Path.LocalPath;
        if (!File.Exists(path))
        {
            _viewModel.StatusText = "The selected cover-art file is unavailable.";
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var artworkError = TrySetArtwork(row, fullPath);
        if (artworkError is not null)
        {
            _viewModel.StatusText = $"The selected cover art could not be opened: {artworkError}";
            return;
        }

        library.UpdateGamePreference(
            row.RootPath,
            preference => preference.CustomArtworkPath = fullPath);
        library.RecordHistory(row.RootPath, "Cover changed", "Artwork", detail: path);
        _viewModel.StatusText = $"Applied custom cover art to {row.Name}.";
    }

    private async void GameUpdateLatest_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetCatalog(out var catalog))
        {
            return;
        }

        if (row.ScanResult is null)
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
        var plan = _planner.Plan([row.ScanResult], candidates);
        var ready = plan.Where(item => item.Status == UpdatePlanStatus.Ready).ToArray();
        var targetCount = ready.Sum(item => item.Targets.Count);
        if (targetCount == 0)
        {
            _viewModel.StatusText = $"{row.Name} already has the latest detected DLL versions.";
            return;
        }

        var confirmed = await new ConfirmationDialog(
            "Confirm DLL update",
            $"Update {targetCount} detected DLL file{Plural(targetCount)} in {row.Name}? An adjacent .dlsss backup is created when needed.",
            ConfirmationDialog.GameFileWriteWarning)
            .ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        await RunBusyAsync($"Updating detected DLLs in {row.Name}…", async () =>
        {
            using var cache = new DownloadCache();
            var results = await Task.Run(() => DllOperations.ApplyUpdatesAsync(
                plan,
                cache,
                _lifetime.Token));
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
            _viewModel.StatusText = $"Updated {succeeded} DLL file{Plural(succeeded)} in {row.Name}.";
        });
    }

    private async void GameRemove_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library))
        {
            return;
        }

        var confirmed = await new ConfirmationDialog(
            "Remove game",
            $"Remove {row.Name} from this library?")
            .ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        RemoveGameFromLibrary(row, library);
        await RefreshLibraryAsync(runInitialDeepScan: false);
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_opened)
        {
            ApplyGameView();
        }
    }

    private void LibraryView_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_opened)
        {
            ApplyGameView();
        }
    }

    private void FilterMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value }
            && int.TryParse(value, out var index))
        {
            _filterComboBox.SelectedIndex = index;
        }
    }

    private void SortMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value }
            && int.TryParse(value, out var index))
        {
            _sortComboBox.SelectedIndex = index;
        }
    }

    private void Batch_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsBatchMode = !_viewModel.IsBatchMode;
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
        foreach (var game in _viewModel.Games)
        {
            game.IsSelected = true;
        }
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
            AddAlert("Warning", warning);
        }

        var games = library.Merge(discovery);
        foreach (var filesystem in FilesystemInspector.InspectPaths(
            games.Select(game => game.RootPath))
            .GroupBy(item => (item.MountPoint, item.Type))
            .Select(group => group.First()))
        {
            var detail = filesystem.Warning
                ?? $"{filesystem.Type} mounted at {filesystem.MountPoint}";
            AddAlert(filesystem.Warning is null ? "Detected" : "Warning", detail);
        }

        return games;
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
            AddAlert("Warning", exception.Message);
        }
    }

    private Progress<LibraryScanProgress> CreateScanProgress(bool isDeepScan) =>
        new Progress<LibraryScanProgress>(progress =>
        {
            var label = isDeepScan ? "Deep Scan" : "Fast Scan";
            _viewModel.StatusText =
                $"{label}: {progress.ProcessedGames:N0} / {progress.TotalGames:N0} game roots processed…";
        });

    private void ReplaceScans(IReadOnlyList<ScanResult> scans)
    {
        foreach (var row in _allRows)
        {
            row.PropertyChanged -= GameRow_PropertyChanged;
            row.ClearArtwork();
        }

        _viewModel.Games.Clear();
        _allRows.Clear();
        _knownPaths.Clear();
        foreach (var scan in scans.Where(scan => scan.Dlls.Count > 0))
        {
            if (!AddGame(scan.Game))
            {
                continue;
            }

            _allRows[^1].SetScanResult(scan);
        }

        _viewModel.GameCount = _allRows.Count;
        _viewModel.SelectedCount = _allRows.Count(row => row.IsSelected);
        ApplyGameView();
    }

    private void ApplyGameView()
    {
        IEnumerable<GameRowViewModel> rows = _allRows;
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
        rows = _filterComboBox.SelectedIndex == 8
            ? rows.Where(row => row.IsHidden)
            : rows.Where(row => !row.IsHidden);
        rows = _filterComboBox.SelectedIndex switch
        {
            1 => rows.Where(row => !manualPaths.Contains(row.RootPath)),
            2 => rows.Where(row => manualPaths.Contains(row.RootPath)),
            3 => rows.Where(row => HasFamily(row, DllType.Dlss)),
            4 => rows.Where(row => HasFamily(
                row,
                DllType.DlssFrameGeneration,
                DllType.XeSsFrameGeneration)),
            5 => rows.Where(row => HasFamily(row, DllType.DlssRayReconstruction)),
            6 => rows.Where(row => HasFamily(
                row,
                DllType.XeSs,
                DllType.XeLl,
                DllType.XeSsFrameGeneration,
                DllType.XeSsDx11)),
            7 => rows.Where(row => HasFamily(
                row,
                DllType.Fsr31Dx12,
                DllType.Fsr31Vulkan)),
            _ => rows,
        };

        rows = _sortComboBox.SelectedIndex switch
        {
            1 => rows.OrderBy(row => row.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            2 => rows.OrderByDescending(GetHighestVersion, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
        };

        _viewModel.Games.Clear();
        foreach (var row in rows)
        {
            _viewModel.Games.Add(row);
        }
    }

    private static bool HasFamily(GameRowViewModel row, params DllType[] families) =>
        row.ScanResult?.Dlls.Any(dll => families.Contains(dll.Type)) == true;

    private static string GetHighestVersion(GameRowViewModel row) =>
        row.ScanResult?.Dlls
            .Select(dll => dll.Version)
            .OrderByDescending(version => version, StringComparer.OrdinalIgnoreCase)
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
                            () => TrySetArtwork(row, customArtworkPath));
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
                            () => TrySetArtwork(row, result.Path));
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
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The window is closing.
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = $"Operation failed: {exception.Message}";
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
            _viewModel.SelectedCount = _allRows.Count(row => row.IsSelected);
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
            return $"Could not open artwork for {row.Name}: {exception.Message}";
        }
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

    private static bool TryGetMenuGame(object? sender, out GameRowViewModel row)
    {
        row = null!;
        if (sender is MenuItem { Tag: GameRowViewModel taggedRow })
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
        await RunBusyAsync($"Reloading {row.Name}…", async () =>
        {
            var scans = await ScanRowsAsync([row], catalog);
            if (scans.Count == 0)
            {
                _viewModel.StatusText = $"{row.Name} is no longer available for Fast Scan.";
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

            _viewModel.StatusText = $"Reloaded {row.Name}; found {scans[0].Scan.Dlls.Count} supported DLL file{Plural(scans[0].Scan.Dlls.Count)}.";
        });
    }

    private static void RemoveGameFromLibrary(
        GameRowViewModel row,
        PersistentLibrary library,
        HashSet<string>? manualPaths = null)
    {
        manualPaths ??= library.State.ManualGames
            .Select(game => game.RootPath)
            .ToHashSet(PathComparer);
        if (manualPaths.Contains(row.RootPath))
        {
            library.RemoveManualGame(row.RootPath);
        }
        else if (row.Game.SteamAppId is not null)
        {
            library.ExcludeSteamGame(row.Game.SteamAppId);
        }

        library.RemoveGameState(row.RootPath);
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

    private static string[] ParseLines(string? value) =>
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

    private enum ManualImportKind
    {
        Single,
        Multiple,
        Parent,
    }

}
