using DlssSwapper.Linux.Cli.Core;
using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests;

internal static class LinuxBatchUpdateTests
{
    public static async Task RunAsync()
    {
        var temporary = Directory.CreateTempSubdirectory("lle-linux-batch-");
        try
        {
            var package = Directory.CreateDirectory(Path.Combine(temporary.FullName, "package")).FullName;
            foreach (var name in StreamlineComponentSet.FileNames)
                StreamlineSafetyTests.WriteDll(Path.Combine(package, name), "new");
            var scans = Enumerable.Range(1, 2).Select(index =>
            {
                var root = Directory.CreateDirectory(Path.Combine(temporary.FullName, $"game{index}")).FullName;
                StreamlineSafetyTests.WriteDll(Path.Combine(root, "sl.common.dll"), "old");
                StreamlineSafetyTests.WriteDll(Path.Combine(root, "sl.reflex.dll"), "keep");
                return new ScanResult(new SelectedGame($"Game {index}", root, null), [], []);
            }).ToArray();
            var acquisitions = 0;
            Task<string> Acquire(CancellationToken token) { acquisitions++; return Task.FromResult(package); }
            var plan = await BatchUpdateWorkflow.PrepareAsync(scans, new Dictionary<DllType, DllCatalogEntry>(), ["sl.common.dll"], Acquire, default);
            Check(acquisitions == 1, "SDK should be acquired once per batch");
            Check(plan.Games.All(game => game.PartialStreamline), "partial-set warning not requested");
            Check(scans.All(scan => StreamlineSafetyTests.ReadLabel(Path.Combine(scan.Game.RootPath, "sl.common.dll")) == "old"), "planning mutated game files");
            // One changed game must fail without blocking the other game.
            StreamlineSafetyTests.WriteDll(Path.Combine(scans[0].Game.RootPath, "sl.common.dll"), "external");
            var results = await BatchUpdateWorkflow.ApplyAsync(plan, (_, _) => throw new Exception("unexpected DLL download"), default);
            Check(results.Count == 2 && !results[0].Success && results[1].Success, "stale game was not isolated");
            Check(StreamlineSafetyTests.ReadLabel(Path.Combine(scans[0].Game.RootPath, "sl.common.dll")) == "external", "stale game overwritten");
            Check(StreamlineSafetyTests.ReadLabel(Path.Combine(scans[1].Game.RootPath, "sl.common.dll")) == "new", "selected component not updated");
            Check(scans.All(scan => StreamlineSafetyTests.ReadLabel(Path.Combine(scan.Game.RootPath, "sl.reflex.dll")) == "keep"), "unselected component changed");
            var failed = await BatchUpdateWorkflow.PrepareAsync(scans, new Dictionary<DllType, DllCatalogEntry>(), ["sl.common.dll"], _ => throw new IOException("offline"), default);
            var failedResults = await BatchUpdateWorkflow.ApplyAsync(failed, (_, _) => throw new Exception("unexpected DLL download"), default);
            Check(failedResults.Count == 2 && failedResults.All(result => !result.Success && result.Message.Contains("offline")), "download failure reported as installation success");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var cancelledResults = await BatchUpdateWorkflow.ApplyAsync(plan, (_, _) => throw new Exception("unexpected download"), cancelled.Token);
            Check(cancelledResults.Count == 2 && cancelledResults.All(result => !result.Success), "cancelled batch lost outcomes");
            acquisitions = 0;
            var noSelection = await BatchUpdateWorkflow.PrepareAsync(scans, new Dictionary<DllType, DllCatalogEntry>(), [], Acquire, default);
            Check(acquisitions == 0 && noSelection.Games.All(game => game.Streamline is null), "opt-out acquired SDK");

            // Exercise the actual ordinary-DLL mutation adapter with a local fixture payload.
            var dll = Path.Combine(scans[0].Game.RootPath, "libxess.dll");
            File.WriteAllText(dll, "old ordinary DLL");
            var payload = Path.Combine(temporary.FullName, "payload.dll"); File.WriteAllText(payload, "new ordinary DLL");
            var type = DllTypes.All.Single(family => family.FileName == "libxess.dll").Type;
            var entry = new DllCatalogEntry(type, "2.0", 2, DllScanner.ComputeMd5(payload), new string('0', 32), new Uri("https://example.invalid/payload.zip"), 1, 1, true, false);
            var ordinaryScan = scans[0] with { Dlls = [new DetectedDll(type, dll, "libxess.dll", DllScanner.ComputeMd5(dll), "1.0")] };
            var combined = await BatchUpdateWorkflow.PrepareAsync([ordinaryScan], new Dictionary<DllType, DllCatalogEntry> { [type] = entry }, ["sl.common.dll"], Acquire, default);
            var dllAcquired = 0;
            var combinedResults = await BatchUpdateWorkflow.ApplyAsync(combined, (_, _) => { dllAcquired++; return Task.FromResult(payload); }, default);
            Check(dllAcquired == 1 && File.ReadAllText(dll) == "new ordinary DLL", "uncached apply did not continue to replacement");
            Check(combinedResults.Count == 2 && combinedResults.All(result => result.Success), "combined batch did not report both families");
            File.WriteAllText(dll, "old ordinary DLL");
            var secondDll = Path.Combine(scans[1].Game.RootPath, "libxess.dll");
            File.WriteAllText(secondDll, "old ordinary DLL");
            var secondScan = scans[1] with { Dlls = [new DetectedDll(type, secondDll, "libxess.dll", DllScanner.ComputeMd5(secondDll), "1.0")] };
            var cancellationPlan = await BatchUpdateWorkflow.PrepareAsync([ordinaryScan, secondScan], new Dictionary<DllType, DllCatalogEntry> { [type] = entry }, [], Acquire, default);
            using var midway = new CancellationTokenSource();
            var calls = 0;
            var partialResults = await BatchUpdateWorkflow.ApplyAsync(cancellationPlan, (_, token) =>
            {
                if (++calls == 2) { midway.Cancel(); token.ThrowIfCancellationRequested(); }
                return Task.FromResult(payload);
            }, midway.Token);
            Check(partialResults.Count == 2 && partialResults[0].Success && !partialResults[1].Success, "mid-batch cancellation lost completed outcomes");
            Check(File.ReadAllText(secondDll) == "old ordinary DLL", "cancelled target changed");
            File.WriteAllText(dll, "old ordinary DLL");
            var abortedCalls = 0;
            var aborted = await BatchUpdateWorkflow.ApplyAsync(cancellationPlan, (_, _) =>
            {
                abortedCalls++;
                throw new OperationCanceledException("download cancelled independently");
            }, default);
            Check(abortedCalls == 1 && aborted.Count == 2 && aborted.All(result => !result.Success), "acquisition cancellation did not stop subsequent updates");
            Check(File.ReadAllText(dll) == "old ordinary DLL" && File.ReadAllText(secondDll) == "old ordinary DLL", "aborted batch changed files");
            var invalidPackage = await BatchUpdateWorkflow.PrepareAsync(scans, new Dictionary<DllType, DllCatalogEntry>(), ["sl.common.dll"], _ => Task.FromResult(temporary.FullName), default);
            Check(invalidPackage.Games.All(game => game.StreamlineError is not null), "missing package files mistaken for current versions");

            var language = new Translations("en-US");
            var text = (Dictionary<string, string>)language.Values;
            text["Linux_PlanUpdate"] = "PLAN {0}";
            text["Linux_OperationUpdated"] = "UPDATED {0}";
            text["Linux_OperationCancelled"] = "CANCELLED";
            text["Linux_BatchSdkFailed"] = "SDK FAILURE {0}";
            text["Linux_StreamlineUpdated"] = "SDK UPDATED {0}";
            var localized = await BatchUpdateWorkflow.PrepareAsync([ordinaryScan], new Dictionary<DllType, DllCatalogEntry> { [type] = entry }, [], Acquire, default, language);
            Check(localized.Games.Single().Dlls.Single().Message == "PLAN 2.0", "localized plan lost selected version");
            var localizedResults = await BatchUpdateWorkflow.ApplyAsync(localized, (_, _) => Task.FromResult(payload), default, 2, language);
            Check(localizedResults.Single().Message == "UPDATED 2.0" && File.ReadAllText(dll) == "new ordinary DLL", "localized concurrent update changed behavior or lost outcome");
            var localizedCancel = await BatchUpdateWorkflow.ApplyAsync(localized, (_, _) => throw new Exception("unexpected acquisition"), cancelled.Token, 2, language);
            Check(localizedCancel.Single().Message == "CANCELLED" && localizedCancel.Single().EffectiveOutcome == OperationOutcome.Cancelled, "localized cancellation changed outcome");
            var localizedFailure = await BatchUpdateWorkflow.PrepareAsync(scans, new Dictionary<DllType, DllCatalogEntry>(), ["sl.common.dll"], _ => throw new IOException("offline"), default, language);
            Check(localizedFailure.Games.All(game => game.StreamlineError == "SDK FAILURE offline"), "localized SDK failure lost diagnostic detail");
            StreamlineSafetyTests.WriteDll(Path.Combine(scans[0].Game.RootPath, "sl.common.dll"), "old");
            var localizedSdk = await BatchUpdateWorkflow.PrepareAsync([scans[0]], new Dictionary<DllType, DllCatalogEntry>(), ["sl.common.dll"], Acquire, default, language);
            var localizedSdkResult = await BatchUpdateWorkflow.ApplyAsync(localizedSdk, (_, _) => throw new Exception("unexpected acquisition"), default, translations: language);
            Check(localizedSdkResult.Single().Message == "SDK UPDATED 1" && localizedSdkResult.Single().Success, "localized SDK result lost successful count");
            Check(new UpdatePlanner().Plan([ordinaryScan], new Dictionary<DllType, DllCatalogEntry> { [type] = entry }).Single().Message == "Update to 2.0.", "default CLI plan changed");

            // An SDK-only game must finish while an independent game's DLL
            // acquisition is waiting, not queue behind a global SDK phase.
            File.WriteAllText(dll, "old ordinary DLL");
            foreach (var scan in scans) StreamlineSafetyTests.WriteDll(Path.Combine(scan.Game.RootPath, "sl.common.dll"), "old");
            var concurrent = await BatchUpdateWorkflow.PrepareAsync([ordinaryScan, scans[1]],
                new Dictionary<DllType, DllCatalogEntry> { [type] = entry }, ["sl.common.dll"], Acquire, default);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = BatchUpdateWorkflow.ApplyAsync(concurrent, async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return payload;
            }, default, 2);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var target = Path.Combine(scans[1].Game.RootPath, "sl.common.dll");
                string ReadUpdatingLabel()
                {
                    using var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    stream.Position = 512;
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd().TrimEnd('\0');
                }
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (ReadUpdatingLabel() != "new" && DateTime.UtcNow < deadline)
                    await Task.Delay(20);
                Check(ReadUpdatingLabel() == "new" && !operation.IsCompleted,
                    "Independent Streamline game waited for the ordinary DLL batch");
            }
            finally
            {
                release.TrySetResult();
                await operation.WaitAsync(TimeSpan.FromSeconds(20));
            }
            var concurrentResults = await operation;
            Check(concurrentResults.Count == 3 && concurrentResults.All(result => result.Success)
                && concurrentResults[0].Game == scans[0].Game && concurrentResults[2].Game == scans[1].Game,
                "Combined concurrent batch lost successful outcomes or input order");
        }
        finally { temporary.Delete(true); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
