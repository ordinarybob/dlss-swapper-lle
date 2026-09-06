using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class ManualGameImportTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "manual-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "state")));
            var game = Path.Combine(root, "game"); Directory.CreateDirectory(game);
            var other = Path.Combine(root, "other"); Directory.CreateDirectory(other);
            library.UpdateState(state => { });
            var statePath = Path.Combine(library.StateDirectory, "state.json");
            var baseline = File.ReadAllText(statePath);
            async Task Reject(Func<Task> action)
            {
                var rejected = false;
                try { await action(); } catch (Exception ex) when (ex is IOException or OperationCanceledException) { rejected = true; }
                Check(rejected, "Invalid or failed import succeeded");
                Check(File.ReadAllText(statePath) == baseline && library.State.ManualGames.Count == 0, "Failed import changed registration");
                var covers = Path.Combine(library.StateDirectory, "custom-covers");
                Check(!Directory.Exists(covers) || Directory.GetFiles(covers).Length == 0, "Failed import retained draft cover");
            }
            await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, "Name", null, new Processor(), () => [game]));
            await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, " ", null, new Processor(), () => []));
            await Reject(() => ManualGameImportWorkflow.SaveAsync(library, Path.GetPathRoot(game)!, "Name", null, new Processor(), () => []));
            await Reject(() => ManualGameImportWorkflow.SaveAsync(library, Path.Combine(root, "missing"), "Name", null, new Processor(), () => []));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, "Name", [1], new Processor(), () => [], cancelled.Token));
            }
            await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, "Name", [1], new Processor(fail: true), () => []));
            using (var cancelled = new CancellationTokenSource())
            {
                // Cancellation after conversion must still prevent the state commit.
                await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, "Name", [1],
                    new Processor(afterWrite: cancelled.Cancel), () => [], cancelled.Token));
            }
            using (var cancelled = new CancellationTokenSource())
            {
                var processor = new PausedProcessor();
                var pending = ManualGameImportWorkflow.SaveAsync(library, game, "Name", [1], processor, () => [], cancelled.Token);
                try
                {
                    await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!pending.IsCompleted && File.ReadAllText(statePath) == baseline, "Conversion committed before completion");
                    cancelled.Cancel();
                    await Reject(() => pending);
                }
                finally
                {
                    cancelled.Cancel();
                    try { await pending; } catch (OperationCanceledException) { }
                }
            }
            using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, "Name", [1], new Processor(), () => []));
            var discovered = false;
            await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, "Name", [1],
                new Processor(afterWrite: () => discovered = true), () => discovered ? [game] : []));
            await Reject(() => ManualGameImportWorkflow.SaveAsync(library, game, "Name", [1],
                new Processor(afterWrite: () => Directory.Delete(game)), () => []));
            Directory.CreateDirectory(game);
            var saved = await ManualGameImportWorkflow.SaveAsync(library, game, "Custom title", [1,2,3], new Processor(), () => []);
            var reloaded = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
            Check(saved.Name == "Custom title" && reloaded.State.ManualGames.Single().Name == saved.Name, "Edited title did not persist");
            Check(File.ReadAllBytes(reloaded.FindGamePreference(game)!.CustomArtworkPath!).SequenceEqual(new byte[] {1,2,3}), "Owned cover did not persist");
            await ManualGameImportWorkflow.SaveAsync(library, other, "No cover", null, new Processor(), () => []);
            reloaded = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
            Check(reloaded.State.ManualGames.Count == 2 && reloaded.FindGamePreference(other)?.CustomArtworkPath is null, "Cover-free import failed");
            var duplicateRejected = false;
            try { await ManualGameImportWorkflow.SaveAsync(library, game, "Duplicate", null, new Processor(), () => []); }
            catch (IOException) { duplicateRejected = true; }
            Check(duplicateRejected && library.State.ManualGames.Count == 2, "Manual duplicate accepted");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class PausedProcessor : IArtworkImageProcessor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SavePortraitAsync(ReadOnlyMemory<byte> source, string destinationPath,
            int maximumWidth, int maximumHeight, CancellationToken cancellationToken)
        {
            await File.WriteAllBytesAsync(destinationPath, source.ToArray(), cancellationToken);
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
    private sealed class Processor(bool fail = false, Action? afterWrite = null) : IArtworkImageProcessor
    {
        public async Task SavePortraitAsync(ReadOnlyMemory<byte> source, string destinationPath,
            int maximumWidth, int maximumHeight, CancellationToken cancellationToken)
        {
            await File.WriteAllBytesAsync(destinationPath, source.ToArray(), cancellationToken);
            afterWrite?.Invoke();
            if (fail) throw new IOException("Invalid image fixture");
        }
    }
}
