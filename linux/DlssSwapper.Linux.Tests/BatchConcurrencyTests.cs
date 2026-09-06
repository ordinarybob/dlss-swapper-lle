using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class BatchConcurrencyTests
{
    internal static async Task RunAsync()
    {
        foreach (var limit in new[] { 1, 2 })
            await VerifyAsync(limit, false, false);
        await VerifyAsync(2, true, false);
        await VerifyAsync(2, false, true);
        await VerifyCompletedBeforeCancellationAsync();
    }

    private static async Task VerifyCompletedBeforeCancellationAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "lle-batch-completed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var original = StreamlineSafetyTests.DllBytes("original");
            var updated = StreamlineSafetyTests.DllBytes("updated");
            var source = Path.Combine(root, "payload.dll"); File.WriteAllBytes(source, updated);
            var family = DllTypes.Get(DllType.Dlss);
            var entry = new DllCatalogEntry(DllType.Dlss, "2.0", 1, DllScanner.ComputeMd5(source), "", null, updated.Length, 0, true, false);
            var targets = new List<string>();
            var games = Enumerable.Range(0, 3).Select(index =>
            {
                var directory = Path.Combine(root, index.ToString()); Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, family.FileName); File.WriteAllBytes(path, original); targets.Add(path);
                // Same installation root forces these plans to execute in order even with two workers.
                var game = new SelectedGame(index.ToString(), root, null);
                var target = new DetectedDll(DllType.Dlss, path, Path.GetRelativePath(root, path), DllScanner.ComputeMd5(path), "2.0");
                return new BatchGamePlan(game, [new(game, family, entry, [target], UpdatePlanStatus.Ready, "fixture")], null, null, false);
            }).ToArray();
            using var cancellation = new CancellationTokenSource();
            var calls = 0;
            Task<string> Acquire(DllCatalogEntry _, CancellationToken token)
            {
                if (Interlocked.Increment(ref calls) == 2) cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(source);
            }
            var results = await BatchUpdateWorkflow.ApplyAsync(new(games, null), Acquire, cancellation.Token, 2);
            if (results.Count != 3 || !results[0].Success || results.Skip(1).Any(result => result.EffectiveOutcome != OperationOutcome.Cancelled))
                throw new Exception("Cancellation lost a completed update or misreported unfinished work");
            if (!File.ReadAllBytes(targets[0]).SequenceEqual(updated) || !File.ReadAllBytes(targets[0] + ".dlsss").SequenceEqual(original))
                throw new Exception("Cancellation damaged completed update or original backup");
            if (targets.Skip(1).Any(path => !File.ReadAllBytes(path).SequenceEqual(original) || File.Exists(path + ".dlsss")))
                throw new Exception("Cancelled targets were changed");
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task VerifyAsync(int limit, bool overlap, bool cancel)
    {
        var root = Path.Combine(Path.GetTempPath(), "lle-concurrency-" + Guid.NewGuid().ToString("N"));
        var family = DllTypes.Get(DllType.Dlss);
        var entry = new DllCatalogEntry(DllType.Dlss, "2.0", 1, new string('a', 32), "", null, 1, 0, true, false);
        var games = Enumerable.Range(0, 3).Select(index =>
        {
            var game = new SelectedGame(index.ToString(), Path.Combine(root, overlap ? "same" : index.ToString()), null);
            var target = new DetectedDll(DllType.Dlss, Path.Combine(game.RootPath, family.FileName), family.FileName, "", "1.0");
            return new BatchGamePlan(game, [new(game, family, entry, [target], UpdatePlanStatus.Ready, "fixture")], null, null, false);
        }).ToArray();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0; var peak = 0; var entered = 0;
        var expected = overlap ? 1 : limit;
        using var cancellation = new CancellationTokenSource();
        async Task<string> Acquire(DllCatalogEntry _, CancellationToken token)
        {
            var count = Interlocked.Increment(ref active);
            lock (release) peak = Math.Max(peak, count);
            if (Interlocked.Increment(ref entered) == expected) started.TrySetResult();
            try
            {
                await release.Task.WaitAsync(token);
                throw new IOException("Intentional acquisition failure; no files written.");
            }
            finally { Interlocked.Decrement(ref active); }
        }
        var operation = BatchUpdateWorkflow.ApplyAsync(new(games, null), Acquire, cancellation.Token, limit);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (entered != expected) throw new Exception("Batch concurrency exceeded independent worker count");
        if (cancel) cancellation.Cancel(); else release.SetResult();
        var results = await operation.WaitAsync(TimeSpan.FromSeconds(10));
        if (peak != expected || results.Count != 3 || !results.Select(result => result.Game.Name).SequenceEqual(new[] { "0", "1", "2" }))
            throw new Exception("Batch concurrency limit or deterministic results failed");
        if (cancel && results.Any(result => result.EffectiveOutcome != OperationOutcome.Cancelled))
            throw new Exception("Cancelled queued games were lost or misreported");
        if (!cancel && results.Any(result => result.Success)) throw new Exception("Acquisition failure was reported as success");
    }
}
