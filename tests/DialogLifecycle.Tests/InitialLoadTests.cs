using DLSS_Swapper;
using DLSS_Swapper.Data;
using DLSS_Swapper.Pages;

internal static class InitialLoadTests
{
    internal static async Task RunAsync()
    {
        foreach (var failureAt in new[] { "cache", "prepare", "cover", "scan", "none" })
        foreach (var alreadyScanned in new[] { false, true })
        {
            var failure = new IOException("Injected " + failureAt + " failure");
            Settings.Instance.HasCompletedInitialDeepScan = alreadyScanned;
            var model = new GameGridPageModel();
            GameManager.Instance.LoadCache = async () =>
            {
                await Task.Yield();
                if (!model.IsGameListLoading || !model.IsDLSSLoading)
                    throw new Exception("Initial loading indicators were not set.");
                if (failureAt == "cache") throw failure;
            };
            model.gameGridPage.Prepare = () =>
            {
                if (model.IsGameListLoading) throw new Exception("Cached games were not made visible before scanning.");
                if (failureAt == "prepare") throw failure;
            };
            model.gameGridPage.Wait = async () =>
            {
                await Task.Yield();
                if (!model.IsBackgroundScanRunning || model.IsDLSSLoading)
                    throw new Exception("Background scanning blocked the usable cached library.");
                if (failureAt == "cover") throw failure;
            };
            model.Scan = async (force, exhaustive, ready) =>
            {
                if (force == alreadyScanned || exhaustive == alreadyScanned)
                    throw new Exception("Initial deep-scan policy changed.");
                await ready();
                if (failureAt == "scan") throw failure;
            };
            Exception? actual = null;
            try { await model.InitialLoadAsync(); }
            catch (Exception error) { actual = error; }
            if (failureAt == "none" ? actual is not null : !ReferenceEquals(actual, failure))
                throw new Exception("Startup swallowed or replaced its failure.", actual);
            if (model.IsGameListLoading || model.IsDLSSLoading || model.IsBackgroundScanRunning
                || model.ScanProgressText != "" || model.PublishedCounts != 1)
                throw new Exception("Startup left loading state active after " + failureAt);
            if (Settings.Instance.HasCompletedInitialDeepScan != (alreadyScanned || failureAt == "none"))
                throw new Exception("Failed startup incorrectly completed the initial deep scan.");
        }
        Console.WriteLine("PASS: initial loading cleanup on cache, cover preparation, cover wait and scan failures; cached visibility and deep-scan retry preserved (I/O doubled).");
    }
}

namespace DLSS_Swapper.Data
{
    internal sealed class GameManager
    {
        internal static GameManager Instance { get; } = new();
        internal Func<Task> LoadCache { get; set; } = () => Task.CompletedTask;
        internal Task LoadGamesFromCacheAsync() => LoadCache();
    }
}

namespace DLSS_Swapper.Helpers
{
    internal static class ResourceHelper
    {
        internal static string GetString(string key) => key;
    }
}

namespace DLSS_Swapper.Pages
{
    public partial class GameGridPageModel
    {
        internal bool IsGameListLoading { get; set; }
        internal bool IsDLSSLoading { get; set; }
        internal bool IsBackgroundScanRunning { get; set; }
        internal string ScanProgressText { get; set; } = "";
        internal int PublishedCounts { get; private set; }
        internal readonly FakeGamePage gameGridPage = new();
        internal Func<bool, bool, Func<Task>, Task> Scan { get; set; } = (_, _, _) => Task.CompletedTask;
        private Task LoadGamesWithProgressAsync(bool forceNeedsProcessing, bool exhaustiveScan, Func<Task> candidateLibraryReady)
            => Scan(forceNeedsProcessing, exhaustiveScan, candidateLibraryReady);
        private void PublishVisibleGameCount()
        {
            if (IsGameListLoading || IsDLSSLoading || IsBackgroundScanRunning)
                throw new Exception("Cleanup must precede publishing the visible count.");
            PublishedCounts++;
        }
    }

    internal sealed class FakeGamePage
    {
        internal Action Prepare { get; set; } = () => { };
        internal Func<Task> Wait { get; set; } = () => Task.CompletedTask;
        internal void PrepareVisibleCoverWait() => Prepare();
        internal Task WaitForVisibleCoverAsync() => Wait();
    }
}
