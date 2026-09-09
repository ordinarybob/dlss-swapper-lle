using DLSS_Swapper;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests
{
    internal static class StreamlineBatchWorkflowTests
    {
        internal static async Task Run()
        {
            var root = Directory.CreateTempSubdirectory("lle-batch-");
            try
            {
                var package = Directory.CreateDirectory(Path.Combine(root.FullName, "sdk")).FullName;
                var names = StreamlineComponentSet.FileNames.Take(2).ToArray();
                foreach (var name in names) StreamlineSafetyTests.WriteDll(Path.Combine(package, name), "new");
                var games = Enumerable.Range(0, 3).Select(index => new Game
                { ID = index.ToString(), Title = $"Game {index}", InstallPath = Directory.CreateDirectory(Path.Combine(root.FullName, $"game{index}")).FullName }).ToArray();
                foreach (var game in games.Take(2))
                    foreach (var name in names) StreamlineSafetyTests.WriteDll(Path.Combine(game.InstallPath, name), "old");
                StreamlineReleaseManager.Package = new("v2.12.0", package);
                StreamlineReleaseManager.Calls = 0;
                var discovered = await StreamlineBatchUpdateWorkflow.DiscoverAsync(games);
                Check((await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, [], false)).Count == 0 &&
                    StreamlineReleaseManager.Calls == 0, "Empty selection acquired a package");
                await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered.Skip(2).ToArray(), names, false);
                Check(StreamlineReleaseManager.Calls == 0, "No eligible games acquired a package");
                Check(StreamlineBatchUpdateWorkflow.HasPartialSets(discovered, [names[0]]), "Partial set warning missing");
                Check(!StreamlineBatchUpdateWorkflow.HasPartialSets(discovered, names), "Full sets warned as partial");
                WinTrust.Calls = 0;
                var results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, names, false);
                Check(WinTrust.Calls == names.Length, "SDK signatures were not validated exactly once per distinct component");
                Check(StreamlineReleaseManager.Calls == 1, "Package acquired more than once");
                Check(results.Count(result => result.Status == BatchSwapStatus.Swapped) == 2 && results[2].Status == BatchSwapStatus.Skipped, "Bad results");
                Check(results[2].DisplayText.Contains("No selected"), "Skip detail lost");
                Check(StreamlineHistory.Changed == 4, "History lost changed files");
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, names, false);
                Check(results.Take(2).All(result => result.Status == BatchSwapStatus.AlreadyCurrent), "No-op not current");
                Check(StreamlineHistory.Changed == 4, "No-op added history");
                foreach (var game in games.Take(2))
                    foreach (var name in names) Check(StreamlineSafetyTests.ReadLabel(Path.Combine(game.InstallPath, name) + ".dlsss") == "old", "Original overwritten");

                var first = Path.Combine(games[0].InstallPath, names[0]);
                StreamlineSafetyTests.WriteDll(first, "old");
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered.Take(1).ToArray(), [names[0]], false);
                Check(results.Single().Status == BatchSwapStatus.Error && StreamlineSafetyTests.ReadLabel(first) == "old", "Unapproved partial applied");
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered.Take(1).ToArray(), [names[0]], true);
                Check(results.Single().Status == BatchSwapStatus.Swapped && StreamlineSafetyTests.ReadLabel(first) == "new", "Approved partial failed");

                Settings.Instance.OnlyShowDownloadedDlls = true;
                var calls = StreamlineReleaseManager.Calls;
                await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, names, false);
                Check(StreamlineReleaseManager.Calls == calls, "Downloaded-only performed acquisition");
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, names, false, new("v2.7.32", "https://fixture.invalid/old.zip"));
                Check(results.Take(2).All(result => result.Status == BatchSwapStatus.Error) && StreamlineReleaseManager.Calls == calls,
                    "Downloaded-only substituted another SDK for an uncached selected release");
                Settings.Instance.OnlyShowDownloadedDlls = false;
                await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, names, false, new("v2.12.0", "https://fixture.invalid/sdk.zip"));
                Check(StreamlineReleaseManager.RequestedTag == "v2.12.0", "Batch ignored selected SDK release");
                StreamlineReleaseManager.Fail = true;
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, names, false);
                Check(results.Take(2).All(result => result.Status == BatchSwapStatus.Error), "Download errors not isolated/reported");
                StreamlineReleaseManager.Fail = false;
                WinTrust.Valid = false;
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, names, false);
                Check(results.Take(2).All(result => result.Status == BatchSwapStatus.Error), "Trust gate bypassed");
                // A failed batch must release package handles and retry validation.
                StreamlineSafetyTests.WriteDll(Path.Combine(package, names[0]), "retry");
                WinTrust.Valid = true;
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered.Take(1).ToArray(), names, false);
                Check(results.Single().Status == BatchSwapStatus.Swapped, "Failed validation prevented retry");
                var extra = StreamlineComponentSet.FileNames.Skip(2).First();
                StreamlineSafetyTests.WriteDll(Path.Combine(package, extra), "new");
                WinTrust.OnVerify = () =>
                {
                    WinTrust.OnVerify = null;
                    StreamlineSafetyTests.WriteDll(Path.Combine(games[0].InstallPath, extra), "outside edit");
                };
                results = await StreamlineBatchUpdateWorkflow.ApplyAsync(discovered.Take(1).ToArray(), names.Append(extra).ToArray(), false);
                Check(results.Single().Status == BatchSwapStatus.Error &&
                    StreamlineSafetyTests.ReadLabel(Path.Combine(games[0].InstallPath, extra)) == "outside edit",
                    "Newly discovered component bypassed batch signature validation");
            }
            finally
            {
                WinTrust.Valid = true;
                WinTrust.OnVerify = null;
                Settings.Instance.OnlyShowDownloadedDlls = false;
                StreamlineReleaseManager.Fail = false;
                root.Delete(true);
            }
        }
        internal static async Task TestConcurrencyAsync()
        {
            foreach (var (limit, overlap, nested) in new[] { (1, false, false), (2, false, false), (2, true, false), (2, true, true) })
            {
                var root = Directory.CreateTempSubdirectory("lle-sdk-workers-");
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Task<List<BatchSwapResult>>? operation = null;
                try
                {
                    var package = Directory.CreateDirectory(Path.Combine(root.FullName, "sdk")).FullName;
                    const string name = "sl.common.dll";
                    StreamlineSafetyTests.WriteDll(Path.Combine(package, name), "new");
                    var games = Enumerable.Range(0, 3).Select(index => new Game
                    {
                        ID = index.ToString(), Title = $"Game {index}",
                        InstallPath = Directory.CreateDirectory(Path.Combine(root.FullName, overlap
                            ? Path.Combine(Enumerable.Repeat("same", nested ? index + 1 : 1).ToArray())
                            : "game" + new string('2', index))).FullName,
                    }).ToArray();
                    foreach (var game in games) StreamlineSafetyTests.WriteDll(Path.Combine(game.InstallPath, name), "old");
                    StreamlineReleaseManager.Package = new("v2.14.1", package);
                    StreamlineReleaseManager.Fail = false;
                    Settings.Instance.BatchSwapConcurrency = limit;
                    var active = 0;
                    var peak = 0;
                    var entered = 0;
                    var expected = overlap ? 1 : limit;
                    StreamlineHistory.BeforeRecordAsync = async () =>
                    {
                        var count = Interlocked.Increment(ref active);
                        lock (release) peak = Math.Max(peak, count);
                        if (Interlocked.Increment(ref entered) == expected) started.TrySetResult();
                        try { await release.Task; }
                        finally { Interlocked.Decrement(ref active); }
                    };
                    var discovered = await StreamlineBatchUpdateWorkflow.DiscoverAsync(games);
                    operation = StreamlineBatchUpdateWorkflow.ApplyAsync(discovered, [name], false);
                    await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Check(entered == expected && peak == expected, "Worker limit or overlap serialization failed");
                    release.SetResult();
                    var results = await operation.WaitAsync(TimeSpan.FromSeconds(10));
                    Check(results.Select(result => result.GameIdentity).SequenceEqual(new[] { "0", "1", "2" }), "Concurrent results lost input order");
                    Check(results.All(result => result.Status is BatchSwapStatus.Swapped or BatchSwapStatus.AlreadyCurrent), "Concurrent update reported a false conflict");
                    Check(results.Count(result => result.Status == BatchSwapStatus.Swapped) == (overlap ? 1 : 3), "Overlapping game was not rechecked after its predecessor");
                    foreach (var game in games)
                        Check(StreamlineSafetyTests.ReadLabel(Path.Combine(game.InstallPath, name) + ".dlsss") == "old", "Concurrent update lost original backup");
                    var invalid = new Game { ID = "invalid", InstallPath = "\0" };
                    var empty = new Game { ID = "empty", InstallPath = "" };
                    var rejected = await StreamlineBatchUpdateWorkflow.ApplyAsync(
                        [new(invalid, [], "Discovery failed"), new(empty, [])], [name], false);
                    Check(rejected[0].Status == BatchSwapStatus.Error && rejected[1].Status == BatchSwapStatus.Skipped,
                        "Invalid or empty discovery paths aborted the batch instead of retaining their outcomes");
                }
                finally
                {
                    release.TrySetResult();
                    if (operation is not null) await operation.WaitAsync(TimeSpan.FromSeconds(20));
                    StreamlineHistory.BeforeRecordAsync = null;
                    Settings.Instance.BatchSwapConcurrency = 2;
                    root.Delete(true);
                }
            }
        }

        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

// Windows-only boundaries are substituted; the production batch orchestration and file engine are compiled unchanged.
namespace DLSS_Swapper
{
    internal sealed class Settings
    {
        internal static Settings Instance { get; } = new();
        internal bool OnlyShowDownloadedDlls { get; set; }
        internal bool AllowUntrusted { get; set; }
        internal int BatchSwapConcurrency { get; set; } = 2;
    }
    internal static class WinTrust
    {
        internal static bool Valid = true;
        internal static int Calls;
        internal static Action? OnVerify;
        internal static bool VerifyEmbeddedSignature(string _) { Calls++; OnVerify?.Invoke(); return Valid; }
    }
}
namespace DLSS_Swapper.Data
{
    internal sealed class Game
    {
        internal string ID { get; init; } = "";
        internal string Title { get; init; } = "";
        internal string InstallPath { get; init; } = "";
    }
}
namespace DLSS_Swapper.Helpers
{
    internal static class ResourceHelper { internal static string GetString(string key) => key; }
}
namespace DLSS_Swapper.Data.Streamline
{
    internal sealed record StreamlinePackage(string Tag, string DirectoryPath);
    internal sealed record StreamlineRelease(string Tag, string DownloadUrl);
    internal static class StreamlineReleaseManager
    {
        internal static StreamlinePackage? Package;
        internal static int Calls;
        internal static bool Fail;
        internal static StreamlinePackage? FindNewestCached() => Package;
        internal static StreamlinePackage? FindCached(string tag) => Package?.Tag == tag ? Package : null;
        internal static string? RequestedTag;
        internal static Task<StreamlinePackage> PrepareAsync(StreamlineRelease release)
        { RequestedTag = release.Tag; return PrepareLatestAsync(); }
        internal static Task<StreamlinePackage> PrepareLatestAsync()
        { Calls++; return Fail ? Task.FromException<StreamlinePackage>(new IOException("Download failed")) : Task.FromResult(Package!); }
    }
    internal static class StreamlineHistory
    {
        internal static int Changed;
        internal static Func<Task>? BeforeRecordAsync;
        internal static async Task<string?> TryRecordAsync(Game _, StreamlineComponentOperationResult result, bool restoring)
        {
            if (BeforeRecordAsync is not null) await BeforeRecordAsync();
            if (result.Success) Interlocked.Add(ref Changed, result.ChangedPaths?.Count ?? 0);
            return null;
        }
    }
}
