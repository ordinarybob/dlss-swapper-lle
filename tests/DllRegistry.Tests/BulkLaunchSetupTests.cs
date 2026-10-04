using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.UserControls;

internal static class BulkLaunchSetupTests
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("lle-launch-defaults-").FullName;
        try
        {
            string Exe(string relative)
            {
                var path = Path.GetFullPath(Path.Combine(root, relative));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, []);
                return path;
            }
            var alan = Exe("Alan Wake 2/AlanWake2.exe");
            Exe("Alan Wake 2/launcher.exe");
            Exe("Alan Wake 2/GameHelper.exe");
            Exe("Alan Wake 2/setup.exe");
            Exe("Alan Wake 2/updater.exe");
            Exe("Alan Wake 2/Artbook/AlanWake2.exe");
            var citron = Exe("citron/citron.exe");
            Exe("citron/citron-cmd.exe");
            var savedPath = Exe("kept/custom.exe");
            var empty = Directory.CreateDirectory(Path.Combine(root, "empty")).FullName;
            var game = new ManuallyAddedGame { Title = "Alan Wake 2", InstallPath = Path.GetDirectoryName(alan)! };
            var kept = new ManuallyAddedGame { Title = "kept", InstallPath = root,
                LaunchExecutable = savedPath, LaunchArguments = "--custom", LaunchWorkingDirectory = root };
            var failed = new ManuallyAddedGame { Title = "failed", InstallPath = game.InstallPath, SaveResult = false };
            var missing = new ManuallyAddedGame { Title = "empty", InstallPath = empty };
            var next = new ManuallyAddedGame { Title = "citron", InstallPath = Path.GetDirectoryName(citron)! };
            Task<List<ManualLaunchManifest.Candidate>> Scan(ManuallyAddedGame item) =>
                Task.FromResult(ManualLaunchManifest.FindCandidates(item.InstallPath, item.Title));
            var drafts = new[] { game, kept, failed, missing, next }
                .Select(item => new ManualLaunchSetup.LaunchDraft(new(item, Scan(item).GetAwaiter().GetResult()))).ToArray();
            Check(drafts[0].Executable == alan && drafts[1].Executable == savedPath,
                "Review must retain saved choices and rank new suggestions.");
            Check(game.LaunchExecutable is null, "Preparing the list must not save.");
            var failures = await ManualLaunchSetup.SaveDraftsAsync(drafts);
            Check(failures == 2 && game.LaunchExecutable == alan && next.LaunchExecutable == citron,
                "Valid rows must save despite independent row failures.");
            Check(kept.SaveCalls == 0 && kept.LaunchArguments == "--custom" && kept.LaunchWorkingDirectory == root,
                "Unchanged saved settings must be preserved without redundant writes.");
            Check(failed.LaunchExecutable is null && failed.LaunchArguments is null && failed.LaunchWorkingDirectory is null,
                "Failed saves must restore in-memory state.");
            Check(missing.SaveCalls == 0 && drafts[2].Error.Length > 0 && drafts[3].Error.Length > 0,
                "Invalid rows need individual errors and no writes.");
            failures = await ManualLaunchSetup.SaveDraftsAsync(drafts, skipUnselected: true);
            Check(failures == 1 && drafts[2].Error.Length > 0 && drafts[3].Error.Length == 0,
                "Save and close must skip unselected rows but retain actual save failures.");
            failed.SaveResult = true;
            failures = await ManualLaunchSetup.SaveDraftsAsync(drafts, skipUnselected: true);
            Check(failures == 0 && missing.SaveCalls == 0 && missing.LaunchExecutable is null,
                "Unselected games must remain unconfigured without preventing close.");
            drafts[3].Executable = savedPath;
            failures = await ManualLaunchSetup.SaveDraftsAsync(drafts);
            Check(failures == 0 && drafts.All(item => item.Error.Length == 0) && game.SaveCalls == 1,
                "Retry must save corrected rows, clear errors and not rewrite successful rows.");
            drafts[0].Executable = savedPath;
            Check(game.LaunchExecutable == alan, "Editing after Apply must remain a draft until saving again.");
            drafts[0].Arguments = "--changed";
            failures = await ManualLaunchSetup.SaveDraftsAsync(drafts);
            Check(failures == 0 && game.LaunchExecutable == savedPath && game.LaunchArguments == "--changed",
                "A second Apply must save changed draft settings.");
            var choices = await Scan(game);
            Check(choices.All(c => !ManualLaunchManifest.IsExcluded(c.Path)), "Excluded executables must never be offered.");
            var batch = Enumerable.Range(0, 8).Select(i => new ManuallyAddedGame { Title = $"Batch {i}", InstallPath = root }).ToArray();
            var scanCalls = new System.Collections.Concurrent.ConcurrentDictionary<ManuallyAddedGame, int>();
            var firstWave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseScans = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var active = 0;
            var peak = 0;
            var preparation = ManualLaunchSetup.ScanAllDefaultsAsync(batch, async item =>
            {
                scanCalls.AddOrUpdate(item, 1, (_, count) => count + 1);
                lock (scanCalls)
                {
                    active++;
                    peak = Math.Max(peak, active);
                    if (active == 4) firstWave.TrySetResult();
                }
                try
                {
                    await releaseScans.Task;
                    if (item == batch[2]) throw new IOException("Unreadable folder");
                    return [new ManualLaunchManifest.Candidate(alan, "AlanWake2.exe")];
                }
                finally { lock (scanCalls) active--; }
            });
            try
            {
                await firstWave.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check(!preparation.IsCompleted && batch.All(item => item.SaveCalls == 0), "Review/save must wait for the complete scan.");
            }
            finally { releaseScans.TrySetResult(); }
            var prepared = await preparation;
            Check(peak == 4 && prepared.Count == batch.Length && prepared.Select(item => item.Game).SequenceEqual(batch),
                "Bulk scans must be bounded and retain review order.");
            Check(scanCalls.Count == batch.Length && scanCalls.Values.All(count => count == 1), "Every game must be scanned once.");
            Check(prepared[2].Error == "Unreadable folder" && scanCalls.Values.All(count => count == 1),
                "Review must retain scan failures and reuse candidates without rescanning.");
            using var stopScan = new CancellationTokenSource();
            stopScan.Cancel();
            try
            {
                await ManualLaunchSetup.ScanAllDefaultsAsync(batch, Scan, token: stopScan.Token);
                throw new Exception("Cancelled preparation was allowed to continue.");
            }
            catch (OperationCanceledException) { }
            Console.WriteLine("Bulk pre-scan: full-set barrier, four-worker bound, ordered results, isolated errors, cached acceptance and cancellation passed.");
            Console.WriteLine("Launch list: staged edits, saved-choice preservation, unselected-row skip on close, per-row errors, partial success, retry and repeated Apply passed.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

namespace DLSS_Swapper.Data.ManuallyAdded
{
    internal sealed class ManuallyAddedGame
    {
        public string Title { get; set; } = "";
        public string InstallPath { get; set; } = "";
        public string? LaunchExecutable { get; set; }
        public string? LaunchArguments { get; set; }
        public string? LaunchWorkingDirectory { get; set; }
        public bool SaveResult { get; set; } = true;
        public int SaveCalls { get; private set; }
        public Task<bool> SaveToDatabaseAsync(bool bypassBatch)
        {
            if (!bypassBatch) throw new Exception("Bulk launch must persist each accepted choice immediately.");
            SaveCalls++;
            return Task.FromResult(SaveResult);
        }
    }
}
