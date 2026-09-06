using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class CustomCoverTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "custom-cover-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "state")));
            var game = Path.Combine(root, "game");
            var source = Path.Combine(root, "source.png");
            await File.WriteAllBytesAsync(source, [1, 2, 3]);
            Check(CustomCoverWorkflow.ValidateSelection([source]) == source, "Single image rejected");
            foreach (var selection in new string?[][] { [], [source, source], [null], [Path.Combine(root, "bad.exe")], [Path.Combine(root, "missing.png")] })
            {
                var rejected = false;
                try { CustomCoverWorkflow.ValidateSelection(selection); } catch (IOException) { rejected = true; }
                Check(rejected, "Invalid cover selection accepted");
            }
            var target = await CustomCoverWorkflow.SaveAsync(library, game, source, new Processor());
            File.Delete(source);
            Check(File.ReadAllBytes(target).SequenceEqual(new byte[] { 1, 2, 3 }), "Owned cover depended on source");
            Check(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).FindGamePreference(game)!.CustomArtworkPath == target, "Cover not saved");
            await File.WriteAllBytesAsync(source, [4, 5, 6]);
            var committed = File.ReadAllText(Path.Combine(library.StateDirectory, "state.json"));
            var delayed = new DelayedProcessor();
            var pending = CustomCoverWorkflow.SaveAsync(library, game, source, delayed);
            await delayed.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            library.UpdateGamePreference(game, value => value.CustomArtworkPath = source);
            delayed.Continue.SetResult();
            var staleRejected = false;
            try { await pending; } catch (IOException) { staleRejected = true; }
            Check(staleRejected && library.FindGamePreference(game)!.CustomArtworkPath == source, "Stale conversion replaced newer choice");
            library.UpdateGamePreference(game, value => value.CustomArtworkPath = target);
            committed = File.ReadAllText(Path.Combine(library.StateDirectory, "state.json"));
            foreach (var mode in new[] { "decode", "cancel", "empty" })
            {
                using var cancellation = new CancellationTokenSource();
                var rejected = false;
                try { await CustomCoverWorkflow.SaveAsync(library, game, source, new FailingProcessor(mode, cancellation), cancellation.Token); }
                catch (IOException) { rejected = mode != "cancel"; }
                catch (OperationCanceledException) { rejected = mode == "cancel"; }
                Check(rejected, "Failed conversion/cancellation was accepted: " + mode);
                Check(File.ReadAllText(Path.Combine(library.StateDirectory, "state.json")) == committed, "Failed conversion changed saved state");
                Check(library.FindGamePreference(game)!.CustomArtworkPath == target && File.Exists(target), "Failed conversion lost the cover");
                Check(Directory.GetFiles(Path.Combine(library.StateDirectory, "custom-covers")).Length == 1, "Failed conversion leaked a candidate");
                Check(File.ReadAllBytes(source).SequenceEqual(new byte[] { 4, 5, 6 }), "Failed conversion changed the source");
            }
            using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var failed = false;
                try { await CustomCoverWorkflow.SaveAsync(library, game, source, new Processor()); } catch (IOException) { failed = true; }
                Check(failed && library.FindGamePreference(game)!.CustomArtworkPath == target, "Failed save replaced cover");
                failed = false;
                try { CustomCoverWorkflow.Remove(library, game); } catch (IOException) { failed = true; }
                Check(failed && library.FindGamePreference(game)!.CustomArtworkPath == target, "Failed removal changed cover");
            }
            Check(Directory.GetFiles(Path.Combine(library.StateDirectory, "custom-covers")).Length == 1, "Failed save leaked candidate");
            CustomCoverWorkflow.Remove(library, game);
            Check(library.FindGamePreference(game)?.CustomArtworkPath is null && File.Exists(target), "Removal did not retain recoverable copy");
            library.UpdateGamePreference(game, value => value.CustomArtworkPath = source);
            var removalRejected = false;
            try { CustomCoverWorkflow.Remove(library, game, target); } catch (IOException) { removalRejected = true; }
            Check(removalRejected && library.FindGamePreference(game)!.CustomArtworkPath == source, "Stale removal discarded a newer cover");
            Check(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).FindGamePreference(game)!.CustomArtworkPath == source, "Stale removal changed saved cover");
            CustomCoverWorkflow.Remove(library, game);
            Check(File.Exists(source), "Legacy source was deleted");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class DelayedProcessor : IArtworkImageProcessor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SavePortraitAsync(ReadOnlyMemory<byte> source, string destinationPath, int maximumWidth,
            int maximumHeight, CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Continue.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            await File.WriteAllBytesAsync(destinationPath, source.ToArray(), cancellationToken);
        }
    }
    private sealed class FailingProcessor(string mode, CancellationTokenSource cancellation) : IArtworkImageProcessor
    {
        public async Task SavePortraitAsync(ReadOnlyMemory<byte> source, string destinationPath, int maximumWidth,
            int maximumHeight, CancellationToken cancellationToken)
        {
            await File.WriteAllBytesAsync(destinationPath, mode == "empty" ? [] : new byte[] { 9 }, cancellationToken);
            if (mode == "decode") throw new IOException("Fixture decode failure after partial output.");
            if (mode == "cancel") cancellation.Cancel();
        }
    }
    private sealed class Processor : IArtworkImageProcessor
    {
        public Task SavePortraitAsync(ReadOnlyMemory<byte> source, string destinationPath, int maximumWidth,
            int maximumHeight, CancellationToken cancellationToken)
        {
            Check(maximumWidth == 400 && maximumHeight == 600, "Wrong portrait bounds");
            return File.WriteAllBytesAsync(destinationPath, source.ToArray(), cancellationToken);
        }
    }
}
