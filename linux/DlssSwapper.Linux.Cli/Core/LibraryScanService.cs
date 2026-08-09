using System.Collections.Concurrent;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record LibraryScanProgress(
    int ProcessedGames,
    int TotalGames,
    bool IsDeepScan);

public sealed record LibraryScanResult(
    IReadOnlyList<ScanResult> Games,
    int LearnedPatternCount,
    TimeSpan Elapsed);

public sealed class LibraryScanService
{
    private readonly DllCatalog _catalog;
    private readonly DllScanner _scanner;

    public LibraryScanService(DllCatalog catalog, DllScanner? scanner = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _scanner = scanner ?? new DllScanner();
    }

    public Task<LibraryScanResult> ScanFastAsync(
        IReadOnlyList<SelectedGame> games,
        LinuxLibraryState state,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        ScanAsync(
            games,
            state,
            deepScan: false,
            library: null,
            progress,
            cancellationToken);

    public async Task<LibraryScanResult> ScanDeepAsync(
        IReadOnlyList<SelectedGame> games,
        PersistentLibrary library,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(library);
        var result = await ScanAsync(
            games,
            library.State,
            deepScan: true,
            library,
            progress,
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<LibraryScanResult> ScanAsync(
        IReadOnlyList<SelectedGame> games,
        LinuxLibraryState state,
        bool deepScan,
        PersistentLibrary? library,
        IProgress<LibraryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(state);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var results = new ConcurrentDictionary<string, ScanResult>(
            PathComparers.FileSystemPath);
        var learnedPatterns = new ConcurrentBag<string>();
        var processed = 0;
        var customPatterns = state.CustomScanPatterns.ToArray();
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = state.Performance.ScanConcurrency,
        };

        await Parallel.ForEachAsync(games, options, (game, token) =>
        {
            try
            {
                var fast = FastScanPatternIndex.EnumerateFastCandidates(
                    game.RootPath,
                    customPatterns,
                    token);
                var candidates = fast;
                if (deepScan)
                {
                    var deep = FastScanPatternIndex.EnumerateDeepCandidates(game.RootPath, token);
                    var fastPaths = fast.Files.ToHashSet(PathComparers.FileSystemPath);
                    foreach (var straggler in deep.Files.Where(path => !fastPaths.Contains(path)))
                    {
                        if (FastScanPatternIndex.TryCreateAdaptivePattern(
                            game.RootPath,
                            straggler,
                            out var pattern))
                        {
                            learnedPatterns.Add(pattern);
                        }
                    }

                    candidates = new CandidateFileResult(
                        deep.Files,
                        fast.Warnings.Concat(deep.Warnings)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray());
                }

                results[game.RootPath] = _scanner.ScanCandidates(game, _catalog, candidates);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                results[game.RootPath] = new ScanResult(
                    game,
                    [],
                    [$"Could not scan '{game.RootPath}': {exception.Message}"]);
            }

            var completed = Interlocked.Increment(ref processed);
            if (completed == games.Count || completed % 100 == 0)
            {
                progress?.Report(new LibraryScanProgress(completed, games.Count, deepScan));
            }

            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);

        var learnedCount = 0;
        if (deepScan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            learnedCount = (library
                ?? throw new InvalidOperationException("Deep Scan requires a persistent library."))
                .UpdateState(currentState =>
                {
                    var normalized = FastScanPatternIndex.NormalizeCustomPatterns(
                        currentState.CustomScanPatterns.Concat(learnedPatterns));
                    var count = normalized
                        .Except(
                            currentState.CustomScanPatterns,
                            StringComparer.OrdinalIgnoreCase)
                        .Count();
                    currentState.CustomScanPatterns = normalized.ToList();
                    currentState.HasCompletedInitialDeepScan = true;
                    return count;
                });
        }

        started.Stop();
        return new LibraryScanResult(
            games
                .Where(game => results.ContainsKey(game.RootPath))
                .Select(game => results[game.RootPath])
                .ToArray(),
            learnedCount,
            started.Elapsed);
    }
}
