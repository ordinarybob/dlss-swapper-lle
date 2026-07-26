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
        Func<DownloadCache> createDownloadCache)
    {
        try
        {
            var options = CliParser.Parse(args);
            if (options.Help)
            {
                CliHelp.Write();
                return 0;
            }

            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            var discovery = new SteamDiscovery().Discover(new SteamDiscoveryOptions
            {
                AdditionalRoots = options.SteamRoots,
                IncludeDefaultRoots = includeDefaultSteamRoots,
            });
            if (options.Command == "discover")
            {
                WriteDiscovery(discovery);
                return 0;
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
