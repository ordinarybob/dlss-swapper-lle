using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Cli;

internal static class Program
{
    public static Task<int> Main(string[] args) =>
        RunAsync(
            args,
            includeDefaultSteamRoots: true,
            static () => new DownloadCache());

    internal static async Task<int> RunAsync(
        string[] args,
        bool includeDefaultSteamRoots,
        Func<DownloadCache> createDownloadCache,
        Func<LibraryStateStore>? createStateStore = null)
    {
        try
        {
            var options = CliParser.Parse(args);
            if (options.Help)
            {
                CliHelp.Write();
                return 0;
            }

            var stateStore = createStateStore?.Invoke() ?? new LibraryStateStore();
            if (options.Command == "reset")
            {
                new LocalDataResetService(stateStore).Reset();
                Console.WriteLine("Removed LLE-owned Linux configuration and application cache.");
                Console.WriteLine("SteamLibrary-adjacent artwork cache was preserved.");
                return 0;
            }

            var library = new PersistentLibrary(stateStore);
            if (options.Command == "state")
            {
                return RunState(options, library);
            }

            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            var discovery = new SteamDiscovery().Discover(new SteamDiscoveryOptions
            {
                AdditionalRoots = options.SteamRoots
                    .Concat(library.State.AdditionalSteamRoots)
                    .Distinct(PathComparers.FileSystemPath)
                    .ToArray(),
                IncludeDefaultRoots = includeDefaultSteamRoots,
            });
            if (options.Command == "discover")
            {
                WriteDiscovery(discovery);
                return 0;
            }


            if (options.Command == "filesystems")
            {
                return RunFilesystems(options, library, discovery);
            }

            WriteWarnings(discovery.Warnings);
            var games = GameSelector.Resolve(options, discovery);
            return options.Command switch
            {
                "scan" => RunScan(options, games),
                "update" => await RunUpdateAsync(
                    options,
                    games,
                    cancellation.Token,
                    createDownloadCache).ConfigureAwait(false),
                "restore" => RunRestore(options, games),
                _ => throw new UsageException($"Unknown command '{options.Command}'."),
            };
        }
        catch (UsageException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            Console.Error.WriteLine("Run with --help for usage.");
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static int RunState(CliOptions options, PersistentLibrary library)
    {
        var action = options.Operands[0].ToLowerInvariant();
        var value = options.Operands.Count > 1 ? options.Operands[1] : null;
        switch (action)
        {
            case "show":
                WriteState(library.State);
                return 0;
            case "add-game":
                Console.WriteLine($"Added manual games: {library.AddManualGames([value!])}");
                return 0;
            case "remove-game":
                Console.WriteLine(library.RemoveManualGame(value!)
                    ? "Removed manual game."
                    : "Manual game was not present.");
                return 0;
            case "restore-steam":
                Console.WriteLine($"Restored Steam games: {library.RestoreSteamGames()}");
                return 0;
            case "add-steam-root":
                return ChangeStringState(
                    library,
                    library.State.AdditionalSteamRoots,
                    NormalizeExistingDirectory(value!),
                    add: true,
                    "Steam root");
            case "remove-steam-root":
                return ChangeStringState(
                    library,
                    library.State.AdditionalSteamRoots,
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(value!)),
                    add: false,
                    "Steam root");
            case "add-pattern":
                value = NormalizePattern(value!);
                return ChangeStringState(
                    library,
                    library.State.CustomScanPatterns,
                    value!,
                    add: true,
                    "Fast Scan pattern");
            case "remove-pattern":
                value = NormalizePattern(value!);
                return ChangeStringState(
                    library,
                    library.State.CustomScanPatterns,
                    value!,
                    add: false,
                    "Fast Scan pattern");
            default:
                throw new UsageException($"Unknown state action '{action}'.");
        }
    }

    private static int ChangeStringState(
        PersistentLibrary library,
        List<string> values,
        string value,
        bool add,
        string label)
    {
        var changed = add
            ? !values.Contains(value, PathComparers.FileSystemPath)
            : values.RemoveAll(item => PathComparers.FileSystemPath.Equals(item, value)) > 0;
        if (add && changed)
        {
            values.Add(value);
        }

        if (changed)
        {
            library.Save();
        }

        Console.WriteLine($"{label}: {(changed ? (add ? "added" : "removed") : "unchanged")}");
        return 0;
    }

    private static void WriteState(LinuxLibraryState state)
    {
        Console.WriteLine($"Initial Deep Scan complete: {state.HasCompletedInitialDeepScan}");
        Console.WriteLine($"HDD mode: {state.HddMode}");
        Console.WriteLine($"Manual games: {state.ManualGames.Count}");
        foreach (var game in state.ManualGames)
        {
            Console.WriteLine($"  game\t{game.Name}\t{game.RootPath}");
        }

        Console.WriteLine($"Additional Steam roots: {state.AdditionalSteamRoots.Count}");
        foreach (var root in state.AdditionalSteamRoots)
        {
            Console.WriteLine($"  steam-root\t{root}");
        }

        Console.WriteLine($"Custom Fast Scan patterns: {state.CustomScanPatterns.Count}");
        foreach (var pattern in state.CustomScanPatterns)
        {
            Console.WriteLine($"  pattern\t{pattern}");
        }

        Console.WriteLine($"Excluded Steam games: {state.ExcludedSteamAppIds.Count}");
    }

    private static int RunFilesystems(
        CliOptions options,
        PersistentLibrary library,
        SteamDiscoveryResult discovery)
    {
        var paths = new List<string>();
        if (options.Paths.Count > 0 || options.Roots.Count > 0)
        {
            paths.AddRange(options.Paths.Select(NormalizeExistingDirectory));
            foreach (var root in options.Roots.Select(NormalizeExistingDirectory))
            {
                paths.AddRange(Directory.EnumerateDirectories(root));
            }
        }
        else
        {
            paths.AddRange(library.Merge(discovery).Select(game => game.RootPath));
            paths.AddRange(library.State.AdditionalSteamRoots);
            paths.AddRange(options.SteamRoots.Select(NormalizeExistingDirectory));
        }

        var filesystems = FilesystemInspector.InspectPaths(paths)
            .GroupBy(item => (item.MountPoint, item.Type))
            .Select(group => group.First())
            .OrderBy(item => item.MountPoint, StringComparer.Ordinal)
            .ToArray();
        Console.WriteLine($"Library filesystems: {filesystems.Length}");
        foreach (var filesystem in filesystems)
        {
            Console.WriteLine($"{filesystem.Type}\t{filesystem.MountPoint}\t{filesystem.Path}");
            if (filesystem.Warning is not null)
            {
                Console.Error.WriteLine($"warning: {filesystem.Warning}");
            }
        }

        return 0;
    }

    private static string NormalizeExistingDirectory(string input)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
        if (!Directory.Exists(path))
        {
            throw new UsageException($"Directory does not exist: {path}");
        }

        if (PathComparers.FileSystemPath.Equals(path, Path.GetPathRoot(path)))
        {
            throw new UsageException($"Filesystem roots cannot be selected: {path}");
        }

        return path;
    }

    private static string NormalizePattern(string input)
    {
        try
        {
            return FastScanPatternIndex.NormalizeCustomPatterns([input]).Single();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            throw new UsageException($"Invalid Fast Scan pattern: {exception.Message}");
        }
    }

    private static void WriteDiscovery(SteamDiscoveryResult discovery)
    {
        Console.WriteLine($"Steam games: {discovery.Games.Count}");
        foreach (var game in discovery.Games)
        {
            Console.WriteLine($"{game.AppId}\t{game.Name}\t{game.InstallDirectory}");
        }

        WriteWarnings(discovery.Warnings);
    }

    private static int RunScan(
        CliOptions options,
        IReadOnlyList<SelectedGame> games)
    {
        var catalog = DllCatalog.Load(ResolveManifestPath(options));
        var scanner = new DllScanner();
        var detectedCount = 0;
        foreach (var game in games)
        {
            var scan = scanner.Scan(game, catalog);
            Console.WriteLine($"{game.Name} [{game.RootPath}]");
            foreach (var dll in scan.Dlls)
            {
                var family = DllTypes.Get(dll.Type);
                Console.WriteLine(
                    $"  {family.ManifestKey}\t{dll.Version}\t{dll.Md5}\t{dll.RelativePath}");
                detectedCount++;
            }

            if (scan.Dlls.Count == 0)
            {
                Console.WriteLine("  No supported DLLs detected.");
            }

            WriteWarnings(scan.Warnings);
        }

        Console.WriteLine($"Detected DLLs: {detectedCount}");
        return 0;
    }

    private static async Task<int> RunUpdateAsync(
        CliOptions options,
        IReadOnlyList<SelectedGame> games,
        CancellationToken cancellationToken,
        Func<DownloadCache> createDownloadCache)
    {
        var catalog = DllCatalog.Load(ResolveManifestPath(options));
        var candidates = CandidateSelector.Resolve(options, catalog);
        var scanner = new DllScanner();
        var scans = games.Select(game => scanner.Scan(game, catalog)).ToArray();
        foreach (var scan in scans)
        {
            WriteWarnings(scan.Warnings);
        }

        var plan = new UpdatePlanner().Plan(scans, candidates);
        WriteUpdatePlan(plan);
        if (options.DryRun)
        {
            Console.WriteLine("DRY RUN: no files were downloaded or written.");
            return 0;
        }

        using var cache = createDownloadCache();
        var results = await DllOperations.ApplyUpdatesAsync(
            plan,
            cache,
            cancellationToken).ConfigureAwait(false);
        WriteResults(results);
        return results.Any(result => !result.Success) ? 1 : 0;
    }

    private static int RunRestore(
        CliOptions options,
        IReadOnlyList<SelectedGame> games)
    {
        var familyFilter = CandidateSelector.ResolveFamilyFilter(options.Families);
        var scanner = new DllScanner();
        var plan = games
            .SelectMany(scanner.PlanRestore)
            .Where(item => familyFilter.Count == 0 || familyFilter.Contains(item.Family.Type))
            .ToArray();
        Console.WriteLine($"Restore targets: {plan.Length}");
        foreach (var item in plan)
        {
            Console.WriteLine(
                $"  {item.Game.Name}\t{item.Family.ManifestKey}\t{item.RelativeTargetPath}");
        }

        if (options.DryRun)
        {
            Console.WriteLine("DRY RUN: no files were written.");
            return 0;
        }

        var results = DllOperations.ApplyRestores(plan);
        WriteResults(results);
        return results.Any(result => !result.Success) ? 1 : 0;
    }

    private static string ResolveManifestPath(CliOptions options)
    {
        var path = options.ManifestPath
            ?? Path.Combine(AppContext.BaseDirectory, "Assets", "static_manifest.json");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new UsageException($"Manifest does not exist: {path}");
        }

        return path;
    }

    private static void WriteUpdatePlan(IReadOnlyList<UpdatePlanItem> plan)
    {
        var ready = plan.Count(item => item.Status == UpdatePlanStatus.Ready);
        var current = plan.Count(item => item.Status == UpdatePlanStatus.AlreadyCurrent);
        var skipped = plan.Count(item => item.Status == UpdatePlanStatus.Skipped);
        Console.WriteLine(
            $"Plan: {ready} update(s), {current} already current, {skipped} skipped.");
        foreach (var item in plan)
        {
            var status = item.Status switch
            {
                UpdatePlanStatus.Ready => "UPDATE",
                UpdatePlanStatus.AlreadyCurrent => "CURRENT",
                UpdatePlanStatus.Skipped => "SKIP",
                _ => throw new InvalidOperationException("Unknown plan status."),
            };
            Console.WriteLine(
                $"  {status}\t{item.Game.Name}\t{item.Family.ManifestKey}\t{item.Candidate.Version}\t{item.Message}");
        }
    }

    private static void WriteResults(IReadOnlyList<OperationResult> results)
    {
        foreach (var result in results)
        {
            Console.WriteLine(
                $"  {(result.Success ? "OK" : "ERROR")}\t{result.Game.Name}\t{result.Family}\t{result.Target}\t{result.Message}");
        }

        Console.WriteLine(
            $"Results: {results.Count(result => result.Success)} succeeded, "
            + $"{results.Count(result => !result.Success)} failed.");
    }

    private static void WriteWarnings(IEnumerable<string> warnings)
    {
        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"warning: {warning}");
        }
    }
}
