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
            var result = await ManualLaunchSetup.SaveDefaultsAsync([game, kept, failed, missing, next], Scan);
            Check(result.Saved == 2 && result.Kept == 1 && result.Skipped.Count == 2 && !result.Cancelled,
                "Bulk outcome counts or continue-after-failure failed.");
            Check(game.LaunchExecutable == alan && next.LaunchExecutable == citron, "Defaults must use existing ranking.");
            Check(kept.SaveCalls == 0 && kept.LaunchExecutable == savedPath && kept.LaunchArguments == "--custom"
                && kept.LaunchWorkingDirectory == root, "Saved choices must remain unchanged.");
            Check(failed.LaunchExecutable is null && failed.LaunchArguments is null
                && failed.LaunchWorkingDirectory is null, "Failed saves must restore in-memory state.");
            Check(missing.SaveCalls == 0, "No candidate must not save.");
            var choices = await Scan(game);
            Check(choices.All(c => !ManualLaunchManifest.IsExcluded(c.Path)), "Excluded executables must never be offered.");
            using var cancel = new CancellationTokenSource();
            var cancelledGame = new ManuallyAddedGame { Title = "cancelled", InstallPath = game.InstallPath };
            result = await ManualLaunchSetup.SaveDefaultsAsync([cancelledGame], async item =>
            {
                var found = await Scan(item);
                cancel.Cancel();
                return found;
            }, token: cancel.Token);
            Check(result.Cancelled && cancelledGame.SaveCalls == 0 && cancelledGame.LaunchExecutable is null,
                "Cancellation during scan must prevent saving.");
            Console.WriteLine("Bulk launch defaults: ranking/exclusions, saved-choice preservation, missing candidates, failed-save rollback, continuation and cancellation passed.");
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
