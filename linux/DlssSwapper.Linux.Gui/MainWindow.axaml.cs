using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
    private readonly TextBox _steamRootsTextBox;
    private readonly TextBox _explicitPathsTextBox;
    private readonly TextBox _gameRootsTextBox;

    private DllCatalog? _catalog;
    private IReadOnlyList<UpdatePlanItem> _currentUpdatePlan = [];

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
            _viewModel.StatusText = "Ready. Discover Steam games or add explicit paths.";
        }
        catch (Exception exception)
        {
            _viewModel.StatusText =
                $"The bundled DLL catalog could not be loaded: {exception.Message}";
            AddResult("Application", "Catalog", "Assets/static_manifest.json", "Error", exception.Message);
        }
    }

    private async void DiscoverSteam_Click(object? sender, RoutedEventArgs e)
    {
        var additionalRoots = ParseLines(_steamRootsTextBox.Text);
        InvalidatePreview();
        await RunBusyAsync("Discovering Steam libraries…", async () =>
        {
            var discovery = await Task.Run(() => _steamDiscovery.Discover(additionalRoots));
            var added = 0;
            foreach (var game in discovery.Games)
            {
                if (AddGame(new SelectedGame(game.Name, game.InstallDirectory, game.AppId)))
                {
                    added++;
                }
            }

            _viewModel.Results.Clear();
            foreach (var warning in discovery.Warnings)
            {
                AddResult("Steam discovery", "Steam", "—", "Warning", warning);
            }

            _viewModel.StatusText =
                $"Steam discovery found {discovery.Games.Count} game{Plural(discovery.Games.Count)}; added {added} new path{Plural(added)}.";
        });
    }

    private async void AddPaths_Click(object? sender, RoutedEventArgs e)
    {
        var explicitPaths = ParseLines(_explicitPathsTextBox.Text);
        var roots = ParseLines(_gameRootsTextBox.Text);
        InvalidatePreview();
        await RunBusyAsync("Adding explicit game paths…", async () =>
        {
            var expansion = await Task.Run(() => ExpandManualPaths(explicitPaths, roots));
            var added = 0;
            foreach (var game in expansion.Games)
            {
                if (AddGame(game))
                {
                    added++;
                }
            }

            _viewModel.Results.Clear();
            foreach (var warning in expansion.Warnings)
            {
                AddResult("Path import", "Filesystem", "—", "Warning", warning);
            }

            _viewModel.StatusText =
                $"Added {added} new game path{Plural(added)}. Multi-game roots include immediate children only.";
        });
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
        return await Task.Run(() => rows
            .Select(row => (row, _scanner.Scan(row.Game, catalog)))
            .ToArray());
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

    private TextBox FindRequiredTextBox(string name) =>
        this.FindControl<TextBox>(name)
        ?? throw new InvalidOperationException($"Required text box '{name}' is missing.");

    private static IReadOnlyList<string> ParseLines(string? value) =>
        (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static ManualExpansion ExpandManualPaths(
        IReadOnlyList<string> explicitPaths,
        IReadOnlyList<string> roots)
    {
        var games = new List<SelectedGame>();
        var warnings = new List<string>();
        foreach (var path in explicitPaths)
        {
            TryAddManualGame(path, games, warnings);
        }

        foreach (var rootInput in roots)
        {
            try
            {
                var root = NormalizeExistingDirectory(rootInput);
                foreach (var child in Directory
                             .EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    TryAddManualGame(child, games, warnings);
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                warnings.Add($"Could not expand game root '{rootInput}': {exception.Message}");
            }
        }

        return new ManualExpansion(games, warnings);
    }

    private static void TryAddManualGame(
        string input,
        List<SelectedGame> games,
        List<string> warnings)
    {
        try
        {
            var path = NormalizeExistingDirectory(input);
            var name = new DirectoryInfo(path).Name;
            games.Add(new SelectedGame(
                string.IsNullOrWhiteSpace(name) ? path : name,
                path,
                null));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            warnings.Add($"Could not add game path '{input}': {exception.Message}");
        }
    }

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

    private sealed record ManualExpansion(
        IReadOnlyList<SelectedGame> Games,
        IReadOnlyList<string> Warnings);
}
