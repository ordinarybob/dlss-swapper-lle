using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record BatchGamePlan(SelectedGame Game, IReadOnlyList<UpdatePlanItem> Dlls,
    StreamlinePreviewSnapshot? Streamline, string? StreamlineError, bool PartialStreamline);

public sealed record BatchUpdatePlan(IReadOnlyList<BatchGamePlan> Games, string? PackageDirectory);

/// <summary>One confirmed selection across DLL families and optional Streamline components.</summary>
public static class BatchUpdateWorkflow
{
    public static async Task<BatchUpdatePlan> PrepareAsync(IReadOnlyList<ScanResult> scans,
        IReadOnlyDictionary<DllType, DllCatalogEntry> candidates, IReadOnlyList<string> componentNames,
        Func<CancellationToken, Task<string>> acquirePackage, CancellationToken token, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);

        // Validate the selection even when no game contains these components.
        StreamlineBatchSelection.SelectPaths([], componentNames);
        var installed = new List<(ScanResult Scan, IReadOnlyList<string> Paths, int Total, string? Error)>();
        foreach (var scan in scans)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var all = componentNames.Count == 0 ? [] : StreamlineComponentSet.FindInstalled(StreamlineWorkflow.ValidateGameRoot(scan.Game.RootPath));
                installed.Add((scan, StreamlineBatchSelection.SelectPaths(all, componentNames), all.Count, null));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { installed.Add((scan, [], 0, ex.Message)); }
        }
        string? package = null, acquisitionError = null;
        if (installed.Any(item => item.Paths.Count > 0))
        {
            try { package = await acquirePackage(token).ConfigureAwait(false); StreamlineComponentSet.ValidatePackage(package); }
            catch (Exception ex) when (ex is not OperationCanceledException) { acquisitionError = T("Linux_BatchSdkFailed", "SDK download failed: {0}", ex.Message); }
        }
        var games = new List<BatchGamePlan>();
        foreach (var item in installed)
        {
            token.ThrowIfCancellationRequested();
            StreamlinePreviewSnapshot? preview = null;
            var error = item.Error ?? (item.Paths.Count > 0 ? acquisitionError : null);
            if (item.Paths.Count > 0 && error is null)
            {
                try
                {
                    if (StreamlineComponentSet.HasPendingRecovery(item.Scan.Game.RootPath))
                        throw new IOException(T("Linux_BatchRecoveryRequired", "Recover the interrupted Streamline operation before updating."));
                    preview = StreamlineWorkflow.Preview(item.Scan.Game.RootPath, package).SelectTargets(item.Paths);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { error = ex.Message; }
            }
            games.Add(new(item.Scan.Game, new UpdatePlanner().Plan([item.Scan], candidates, translations), preview, error,
                item.Paths.Count > 0 && item.Paths.Count < item.Total));
        }
        return new(games, package);
    }

    public static async Task<IReadOnlyList<OperationResult>> ApplyAsync(BatchUpdatePlan plan,
        Func<DllCatalogEntry, CancellationToken, Task<string>> acquireDll, CancellationToken token, int concurrency = 1, Translations? translations = null)
    {
        if (concurrency is < 1 or > 26) throw new ArgumentOutOfRangeException(nameof(concurrency));
        if (concurrency == 1) return await ApplySequentialAsync(plan, acquireDll, token, translations).ConfigureAwait(false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var workers = new SemaphoreSlim(concurrency);
        var tasks = new List<Task<IReadOnlyList<OperationResult>>>();
        var roots = plan.Games.Select(game => Path.TrimEndingDirectorySeparator(Path.GetFullPath(game.Game.RootPath))).ToArray();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        bool Overlaps(string left, string right) => left.Equals(right, comparison)
            || left.StartsWith(right + Path.DirectorySeparatorChar, comparison) || right.StartsWith(left + Path.DirectorySeparatorChar, comparison);
        async Task<string> Acquire(DllCatalogEntry entry, CancellationToken ct)
        {
            try { return await acquireDll(entry, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { cancellation.Cancel(); throw; }
        }
        async Task<IReadOnlyList<OperationResult>> ApplyGame(BatchGamePlan game, Task[] predecessors)
        {
            await Task.WhenAll(predecessors).ConfigureAwait(false);
            // Do not cancel this wait: queued targets still need explicit cancelled results.
            await workers.WaitAsync().ConfigureAwait(false);
            try
            {
                return await ApplySequentialAsync(new([game], plan.PackageDirectory), Acquire, cancellation.Token, translations).ConfigureAwait(false);
            }
            finally { workers.Release(); }
        }
        for (var index = 0; index < plan.Games.Count; index++)
        {
            var predecessors = Enumerable.Range(0, index).Where(previous => Overlaps(roots[index], roots[previous])).Select(previous => (Task)tasks[previous]).ToArray();
            var game = plan.Games[index];
            tasks.Add(Task.Run(() => ApplyGame(game, predecessors)));
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
        var results = new List<OperationResult>();
        for (var index = 0; index < plan.Games.Count; index++)
        {
            results.AddRange(await tasks[index].ConfigureAwait(false));
        }
        return results;
    }

    private static async Task<IReadOnlyList<OperationResult>> ApplySequentialAsync(BatchUpdatePlan plan,
        Func<DllCatalogEntry, CancellationToken, Task<string>> acquireDll, CancellationToken token, Translations? translations)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);

        var results = new List<OperationResult>();
        var cancelled = false;
        foreach (var game in plan.Games)
        {
            foreach (var item in game.Dlls)
            {
                if (item.Status != UpdatePlanStatus.Ready)
                {
                    results.Add(new(game.Game, item.Family.DisplayName, game.Game.RootPath,
                        item.Status == UpdatePlanStatus.AlreadyCurrent, item.Message,
                        item.Status == UpdatePlanStatus.AlreadyCurrent ? OperationOutcome.AlreadyCurrent : OperationOutcome.Skipped));
                    continue;
                }
                foreach (var target in item.Targets)
                {
                    if (cancelled || token.IsCancellationRequested)
                    { results.Add(new(game.Game, item.Family.DisplayName, target.RelativePath, false, T("Linux_OperationCancelled", "Not applied: cancelled."), OperationOutcome.Cancelled)); continue; }
                    try
                    {
                        // One target per call retains completed results if a later acquisition is cancelled.
                        results.AddRange(await DllOperations.ApplyUpdatesAsync([item with { Targets = [target] }], async (entry, ct) =>
                        {
                            var payload = await acquireDll(entry, ct).ConfigureAwait(false);
                            if (target.HasHash && !DllScanner.ComputeMd5(target.Path).Equals(target.Md5, StringComparison.OrdinalIgnoreCase))
                                throw new IOException(T("Linux_OperationChanged", "The installed file changed since confirmation. Review it and try again."));
                            return payload;
                        }, token, translations).ConfigureAwait(false));
                    }
                    catch (OperationCanceledException)
                    { cancelled = true; results.Add(new(game.Game, item.Family.DisplayName, target.RelativePath, false, T("Linux_OperationCancelled", "Not applied: cancelled."), OperationOutcome.Cancelled)); }
                    catch (Exception ex)
                    { results.Add(new(game.Game, item.Family.DisplayName, target.RelativePath, false, ex.Message)); }
                }
            }
            if (game.StreamlineError is not null)
                results.Add(new(game.Game, "Streamline", game.Game.RootPath, false, game.StreamlineError));
            else if (game.Streamline is { } preview)
            {
                if (cancelled || token.IsCancellationRequested)
                    results.Add(new(game.Game, "Streamline", game.Game.RootPath, false, T("Linux_OperationCancelled", "Not applied: cancelled."), OperationOutcome.Cancelled));
                else if (preview.Components.All(item => item.Update.Kind == StreamlineChangeKind.Identical))
                    results.Add(new(game.Game, "Streamline", game.Game.RootPath, true, T("Linux_OperationCurrent", "Already current."), OperationOutcome.AlreadyCurrent));
                else
                {
                    try
                    {
                        var result = StreamlineWorkflow.ApplySelection(game.Game.RootPath, plan.PackageDirectory, preview, false, translations);
                        results.Add(new(game.Game, "Streamline", game.Game.RootPath, result.Success, result.Message));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    { results.Add(new(game.Game, "Streamline", game.Game.RootPath, false, ex.Message)); }
                }
            }
            if (game.Dlls.Count == 0 && game.Streamline is null && game.StreamlineError is null)
                results.Add(new(game.Game, "Selection", game.Game.RootPath, true, T("Linux_OperationNoMatch", "No matching installed components; nothing changed."), OperationOutcome.Skipped));
        }
        return results;
    }
}
