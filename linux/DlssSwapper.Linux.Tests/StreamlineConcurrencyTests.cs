using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineConcurrencyTests
{
    internal static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("lle-streamline-concurrency-");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Task<StreamlineComponentOperationResult>? owner = null;
        try
        {
            var package = Directory.CreateDirectory(Path.Combine(root.FullName, "sdk")).FullName;
            var ownerDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "z-owner")).FullName;
            var otherDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "a-independent")).FullName;
            var failedDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "rollback")).FullName;
            var name = "sl.common.dll";
            foreach (var component in new[] { name, "sl.reflex.dll" })
                StreamlineSafetyTests.WriteDll(Path.Combine(package, component), "new");
            var ownerTarget = Path.Combine(ownerDirectory, name);
            var otherTarget = Path.Combine(otherDirectory, name);
            var failedTargets = new[] { name, "sl.reflex.dll" }.Select(file => Path.Combine(failedDirectory, file)).ToArray();
            foreach (var path in failedTargets.Append(ownerTarget).Append(otherTarget))
                StreamlineSafetyTests.WriteDll(path, "old");

            owner = Task.Run(() => StreamlineComponentSet.UpdateExisting(package, [ownerTarget], (_, _) =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(15))) throw new IOException("Timed out waiting for test release.");
            }));
            Check(entered.Wait(TimeSpan.FromSeconds(10)), "First game never reached replacement.");

            // A partially acquired multi-directory lock must not leak or mutate.
            var conflict = StreamlineComponentSet.UpdateExisting(package, [otherTarget, ownerTarget]);
            Check(!conflict.Success && StreamlineSafetyTests.ReadLabel(otherTarget) == "old", "Overlapping update changed an unlocked component.");
            Check(!StreamlineComponentSet.RestoreOriginals([ownerTarget]).Success, "Restore overlapped an active update.");
            Check(!StreamlineComponentSet.RecoverInterrupted(ownerDirectory).Success, "Recovery overlapped an active update.");

            var other = await Task.Run(() => StreamlineComponentSet.UpdateExisting(package, [otherTarget]))
                .WaitAsync(TimeSpan.FromSeconds(10));
            Check(other.Success && !owner.IsCompleted, "Independent game was blocked by the active transaction.");
            Check(StreamlineComponentSet.RestoreOriginals([otherTarget]).Success, "Independent restore was blocked.");

            // Retain a failed rollback in a third game, then recover it while
            // the first game's transaction is still held at its barrier.
            FileStream? held = null;
            try
            {
                var failed = StreamlineComponentSet.UpdateExisting(package, failedTargets, (index, _) =>
                {
                    if (index != 1) return;
                    held = new FileStream(failedTargets[0], FileMode.Open, FileAccess.Read, FileShare.None);
                    throw new IOException("Injected failure during independent transaction.");
                });
                Check(!failed.Success && StreamlineComponentSet.HasPendingRecovery(failedDirectory), "Failed rollback lost recovery.");
            }
            finally { held?.Dispose(); }
            Check(StreamlineComponentSet.RecoverInterrupted(failedDirectory).Success, "Independent recovery was blocked.");
            Check(failedTargets.All(path => StreamlineSafetyTests.ReadLabel(path) == "old"), "Recovery did not restore the failed game.");
            Check(StreamlineSafetyTests.ReadLabel(ownerTarget) == "old" && !owner.IsCompleted, "Another operation disturbed the held game.");

            release.Set();
            var completed = await owner.WaitAsync(TimeSpan.FromSeconds(10));
            Check(completed.Success && StreamlineSafetyTests.ReadLabel(ownerTarget) == "new", "Held update did not complete.");
            Check(StreamlineSafetyTests.ReadLabel(ownerTarget + StreamlineComponentSet.BackupSuffix) == "old", "Original backup changed.");
            Check(!StreamlineComponentSet.HasPendingRecovery(ownerDirectory), "Successful update left a journal.");
            Check(StreamlineComponentSet.RestoreOriginals([ownerTarget]).Success, "Locks were not released after success.");
        }
        finally
        {
            release.Set();
            if (owner is not null) await owner.WaitAsync(TimeSpan.FromSeconds(20));
            root.Delete(true);
        }
        Console.WriteLine("Streamline concurrency: independent update/restore/recovery overlap; conflicting sets excluded; rollback and locks preserved.");
    }

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
