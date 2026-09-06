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

            // Experimental explicit-path operations need neither library state nor a download cache.
            if (options.Command == "streamline")
            {
                var result = StreamlineWorkflow.Execute(options.Paths[0], options.Operands[0],
                    options.PackageDirectory, options.DryRun);
                Console.WriteLine(result.Message);
                return result.Success ? 0 : 1;
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
                var providers = ProviderDiscovery.Discover(library.State, cancellation.Token, includeDefaultSteamRoots);
                WriteWarnings(providers.Warnings);
                foreach (var game in library.Merge(discovery, providers.Games).Where(game => game.SteamAppId is null))
                    Console.WriteLine($"{game.ProviderIdentity?.Provider.ToString() ?? "Manual"}\t{game.Name}\t{game.RootPath}");
                return 0;
            }


            if (options.Command == "filesystems")
            {
                return RunFilesystems(options, library, discovery, cancellation.Token, includeDefaultSteamRoots);
            }

            WriteWarnings(discovery.Warnings);
            var selectsLibrary = options.All || (options.Command == "scan" && options.AppIds.Count == 0
                && options.Paths.Count == 0 && options.Roots.Count == 0);
            IReadOnlyList<SelectedGame>? libraryGames = null;
            if (selectsLibrary)
            {
                var providers = ProviderDiscovery.Discover(library.State, cancellation.Token, includeDefaultSteamRoots);
                WriteWarnings(providers.Warnings);
                libraryGames = library.Merge(discovery, providers.Games);
            }
            var games = GameSelector.Resolve(options, discovery, libraryGames);
            return options.Command switch
            {
                "scan" => RunScan(options, games, library),
                "update" => await RunUpdateAsync(
                    options,
                    games,
                    library,
                    createDownloadCache,
                    cancellation.Token).ConfigureAwait(false),
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
            case "restore-providers":
                library.UpdateState(state => state.ExcludedProviderGames = []);
                Console.WriteLine("Cleared launcher-game exclusions. Game files were not changed.");
                return 0;
            case "add-provider-prefix":
            case "remove-provider-prefix":
            case "add-legendary-config":
            case "remove-legendary-config":
            case "add-heroic-config":
            case "remove-heroic-config":
                if (!Path.IsPathFullyQualified(value!)) throw new UsageException("Provider configuration paths must be absolute.");
                var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value!));
                if (action.EndsWith("heroic-config", StringComparison.Ordinal) && new DirectoryInfo(normalized).Name != "heroic")
                    throw new UsageException("Heroic configuration folders must end in heroic.");
                library.UpdateState(state =>
                {
                    var entries = action.EndsWith("provider-prefix", StringComparison.Ordinal)
                        ? state.ProviderWinePrefixes : action.EndsWith("heroic-config", StringComparison.Ordinal)
                            ? state.HeroicConfigDirectories : state.LegendaryConfigDirectories;
                    if (action.StartsWith("add-", StringComparison.Ordinal))
                    {
                        if (!entries.Contains(normalized, PathComparers.FileSystemPath)) entries.Add(normalized);
                    }
                    else entries.RemoveAll(path => PathComparers.FileSystemPath.Equals(path, normalized));
                });
                Console.WriteLine("Saved provider configuration. Launcher and game files were not changed.");
                return 0;
            case "set-heroic-executable":
                if (!Path.IsPathFullyQualified(value!)) throw new UsageException("Heroic executable path must be absolute.");
                library.UpdateState(state => state.HeroicExecutable = Path.GetFullPath(value!));
                Console.WriteLine("Saved Heroic executable path. Nothing was launched.");
                return 0;
            case "clear-heroic-executable":
                library.UpdateState(state => state.HeroicExecutable = null);
                Console.WriteLine("Heroic will use PATH for native launches.");
                return 0;
            case "add-steam-root":
                return ChangeStringState(
                    library,
                    state => state.AdditionalSteamRoots,
                    NormalizeExistingDirectory(value!),
                    add: true,
                    "Steam root");
            case "remove-steam-root":
                return ChangeStringState(
                    library,
                    state => state.AdditionalSteamRoots,
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(value!)),
                    add: false,
                    "Steam root");
            case "add-pattern":
                value = NormalizePattern(value!, out var addCovered);
                if (addCovered)
                {
                    Console.WriteLine("Fast Scan pattern: unchanged (covered by built-in patterns)");
                    return 0;
                }
                return ChangeStringState(
                    library,
                    state => state.CustomScanPatterns,
                    value!,
                    add: true,
                    "Fast Scan pattern");
            case "remove-pattern":
                value = NormalizePattern(value!, out var removeCovered);
                if (removeCovered)
                {
                    Console.WriteLine("Fast Scan pattern: unchanged (covered by built-in patterns)");
                    return 0;
                }
                return ChangeStringState(
                    library,
                    state => state.CustomScanPatterns,
                    value!,
                    add: false,
                    "Fast Scan pattern");
            default:
                throw new UsageException($"Unknown state action '{action}'.");
        }
    }

    internal static int ChangeStringState(
        PersistentLibrary library,
        Func<LinuxLibraryState, List<string>> selectValues,
        string value,
        bool add,
        string label)
    {
        var changed = library.UpdateState(state =>
        {
            var values = selectValues(state);
            if (!add) return values.RemoveAll(item => PathComparers.FileSystemPath.Equals(item, value)) > 0;
            if (values.Contains(value, PathComparers.FileSystemPath)) return false;
            values.Add(value);
            return true;
        });

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
        Console.WriteLine($"Excluded provider identities: {state.ExcludedProviderGames.Count}");
        foreach (var prefix in state.ProviderWinePrefixes) Console.WriteLine($"  provider-prefix\t{prefix}");
        foreach (var directory in state.LegendaryConfigDirectories) Console.WriteLine($"  legendary-config\t{directory}");
        foreach (var directory in state.HeroicConfigDirectories) Console.WriteLine($"  heroic-config\t{directory}");
        Console.WriteLine($"Heroic executable: {state.HeroicExecutable ?? "heroic (PATH)"}");
    }

    private static int RunFilesystems(
        CliOptions options,
        PersistentLibrary library,
        SteamDiscoveryResult discovery,
        CancellationToken token,
        bool includeDefaults)
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
            var providers = ProviderDiscovery.Discover(library.State, token, includeDefaults);
            WriteWarnings(providers.Warnings);
            paths.AddRange(library.Merge(discovery, providers.Games).Select(game => game.RootPath));
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

    private static string NormalizePattern(string input, out bool coveredByBuiltIn)
    {
        if (!FastScanPatternIndex.TryNormalizePattern(input, out var normalized))
        {
            throw new UsageException("Invalid Fast Scan pattern.");
        }

        var reduced = FastScanPatternIndex.NormalizeCustomPatterns([normalized]);
        coveredByBuiltIn = reduced.Count == 0;
        return coveredByBuiltIn ? normalized : reduced[0];
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
        IReadOnlyList<SelectedGame> games,
        PersistentLibrary library)
    {
        var catalog = LoadCatalog(options, library);
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
        PersistentLibrary library,
        Func<DownloadCache> createDownloadCache,
        CancellationToken cancellationToken)
    {
        var catalog = LoadCatalog(options, library);
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

    private static DllCatalog LoadCatalog(CliOptions options, PersistentLibrary library)
    {
        var savedCatalog = Path.Combine(library.StateDirectory, "manifest.json");
        var catalog = DllCatalog.Load(options.ManifestPath is null && File.Exists(savedCatalog)
            ? savedCatalog : ResolveManifestPath(options));
        foreach (var entry in library.State.ImportedDlls ?? []) catalog.AddImported(entry);
        // Keep CLI trust/debug defaults unchanged; GUI settings do not silently widen CLI eligibility.
        return catalog;
    }

    private static int RunRestore(
        CliOptions options,
        IReadOnlyList<SelectedGame> games)
    {
        var familyFilter = CandidateSelector.ResolveFamilyFilter(options.Families);
        var scanner = new DllScanner();
        var restoreWarnings = new List<string>();
        var plan = games
            .SelectMany(game =>
            {
                var items = scanner.PlanRestore(game, out var warnings);
                restoreWarnings.AddRange(warnings);
                return items;
            })
            .Where(item => familyFilter.Count == 0 || familyFilter.Contains(item.Family.Type))
            .ToArray();
        Console.WriteLine($"Restore targets: {plan.Length}");
        foreach (var warning in restoreWarnings) Console.Error.WriteLine($"WARNING: {warning}");
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
