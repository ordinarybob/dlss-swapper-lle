using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Tests;

internal static class DiscoveryCacheTests
{
    internal static Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "discovery-policy-fixture");
        var source = new DiscoverySourceKey("GOG Wine", Path.Combine(root, "system.reg"));
        var old = new ProviderGame(new(GameProvider.Gog, "42"), "Old title", Path.Combine(root, "game"), source.Path) { Source = source };
        foreach (var state in new[] { DiscoverySourceState.Partial, DiscoverySourceState.Unavailable })
        {
            var result = DiscoveryCacheReconciliation.Providers([old], new([], []) { Sources = [new(source.Kind, source.Path, state)] });
            Check(result.Games.Single() == old && result.UnverifiedGames.Single() == old, "Failed source dropped cached identity");
        }
        var completed = new DiscoverySourceOutcome(source.Kind, source.Path, DiscoverySourceState.Complete);
        Check(DiscoveryCacheReconciliation.Providers([old], new([], []) { Sources = [completed] }).Games.Count == 0, "Successful empty source retained absent game");
        var updated = old with { Name = "New title" };
        var refreshed = DiscoveryCacheReconciliation.Providers([old], new([updated], []));
        Check(refreshed.Games.Single().Name == "New title" && refreshed.UnverifiedGames.Count == 0, "Fresh record did not replace cached metadata");
        Check(DiscoveryCacheReconciliation.Providers([old], new([], []) { Sources = [completed, completed with { State = DiscoverySourceState.Partial }] }).UnverifiedGames.Count == 1,
            "Conflicting source outcomes authorized removal");
        Check(DiscoveryCacheReconciliation.Providers([old with { Source = null }], new([], []) { Sources = [completed] }).UnverifiedGames.Count == 1,
            "Unknown source ownership authorized removal");
        var steam = new SteamGame("42", "Steam fixture", old.InstallDirectory, root, Path.Combine(root, "appmanifest_42.acf"));
        Check(DiscoveryCacheReconciliation.Steam([steam], new([], [])).UnverifiedGames.Count == 1, "Unobserved Steam source dropped cached game");
        Check(DiscoveryCacheReconciliation.Steam([steam], new([], []) { Sources = [new("SteamLibrary", root, DiscoverySourceState.Complete)] }).Games.Count == 0,
            "Successful Steam source did not remove absent cached game");
        VerifySnapshotPersistence();
        return Task.CompletedTask;
    }
    private static void VerifySnapshotPersistence()
    {
        var root = Path.Combine(Path.GetTempPath(), "lle-snapshot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(root));
            library.UpdateState(state => state.CardSize = 7);
            var game = new SteamGame("42", "Saved game", Path.Combine(root, "game"), root, Path.Combine(root, "appmanifest_42.acf"));
            DiscoverySnapshot.Save(library, new([game], []), new([], []));
            var saved = new PersistentLibrary(new LibraryStateStore(root));
            Check(saved.State.DiscoverySnapshot!.SteamGames.Single() == game && saved.State.CardSize == 7, "Snapshot round trip lost game or unrelated state");
            var scan = new ScanResult(new(game.Name, game.InstallDirectory, game.AppId),
                [new(DllType.Dlss, Path.Combine(game.InstallDirectory, "nvngx_dlss.dll"), "nvngx_dlss.dll", new string('a', 32), "2.0")], []);
            DiscoverySnapshot.SaveScans(library, [scan]);
            var cached = DiscoverySnapshot.ReadScans(new PersistentLibrary(new LibraryStateStore(root))).Single();
            Check(cached.CachedAtUtc is not null && cached.Dlls.Single().Version == "2.0", "Cached DLL display data or timestamp lost");
            var failed = scan with { Dlls = [], Warnings = ["Folder unavailable"] };
            var retained = DiscoverySnapshot.ReconcileScans([cached], [failed]).Single();
            Check(retained.CachedAtUtc == cached.CachedAtUtc && retained.Dlls.Count == 1
                && retained.Warnings.Single() == "Folder unavailable", "Failed refresh lost cache age, DLLs or current warning");
            Check(DiscoverySnapshot.ReconcileScans([cached], [scan]).Single().CachedAtUtc is null,
                "Successful refresh retained stale status");
            Check(DiscoverySnapshot.ReconcileScans([cached], []).Count == 0,
                "Scan reconciliation resurrected a game removed by discovery");
            var candidate = new DllCatalogEntry(DllType.Dlss, "3.0", 1, new string('b', 32), "", null, 1, 0, true, false);
            Check(new UpdatePlanner().Plan([cached], new Dictionary<DllType, DllCatalogEntry> { [DllType.Dlss] = candidate }).Single().Status == UpdatePlanStatus.Skipped,
                "Cached scan authorized mutation without fresh inspection");
            DiscoverySnapshot.SaveScans(library, [scan with { Dlls = [], Warnings = ["Unavailable"] }]);
            Check(DiscoverySnapshot.ReadScans(library).Single().Dlls.Count == 1, "Failed scan erased last-known DLLs");
            var updatedScan = scan with { Dlls = [scan.Dlls.Single() with { Version = "3.0" }] };
            DiscoverySnapshot.SaveScans(library, [updatedScan]);
            Check(DiscoverySnapshot.ReadScans(new PersistentLibrary(new LibraryStateStore(root))).Single().Dlls.Single().Version == "3.0",
                "Post-update scan did not replace the saved version");
            DiscoverySnapshot.SaveScans(library, [cached]);
            Check(DiscoverySnapshot.ReadScans(library).Single().Dlls.Single().Version == "3.0",
                "Old display data overwrote a newer verified scan");
            DiscoverySnapshot.SaveScans(library, [scan]);
            Check(DiscoverySnapshot.ReadScans(new PersistentLibrary(new LibraryStateStore(root))).Single().Dlls.Single().Version == "2.0",
                "Post-restore scan did not replace the saved version");
            library.UpdateState(state => state.LibrarySelection = [new() { Id = "Steam", IsEnabled = false }]);
            Check(DiscoverySnapshot.ReadScans(library).Count == 0, "Disabled library appeared from cached scans");
            library.UpdateState(state => state.LibrarySelection = []);
            library.ExcludeSteamGame(game.AppId);
            Check(DiscoverySnapshot.ReadScans(library).Count == 0, "Excluded game appeared from cached scans");
            library.RestoreSteamGames();
            Check(DiscoverySnapshot.ReadScans(library).Count == 1, "Restored game lost its cached scan");
            var manualRoot = Path.Combine(root, "manual");
            Directory.CreateDirectory(manualRoot);
            library.AddManualGames([manualRoot]);
            DiscoverySnapshot.SaveScans(library, [scan with { Game = new("Manual", manualRoot, null) }]);
            Check(DiscoverySnapshot.ReadScans(library).Count == 2, "Manual game's cached scan was missing");
            library.RemoveManualGame(manualRoot);
            Check(DiscoverySnapshot.ReadScans(library).Count == 1, "Removed manual game was resurrected by cache");
            var empty = new SteamDiscoveryResult([], []) { Sources = [new("SteamLibrary", root, DiscoverySourceState.Complete)] };
            using (var held = new FileStream(Path.Combine(root, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                try { DiscoverySnapshot.Save(library, empty, new([], [])); throw new Exception("Locked snapshot save succeeded"); }
                catch (IOException) { }
                Check(library.State.DiscoverySnapshot!.SteamGames.Count == 1, "Failed save discarded prior snapshot in memory");
            }
            Check(new PersistentLibrary(new LibraryStateStore(root)).State.DiscoverySnapshot!.SteamGames.Count == 1, "Failed save changed persisted snapshot");
            DiscoverySnapshot.Save(library, new([], []), new([], []));
            Check(library.State.DiscoverySnapshot!.SteamGames.Count == 1, "Unobserved source discarded saved game");
            DiscoverySnapshot.Save(library, empty, new([], []));
            Check(new PersistentLibrary(new LibraryStateStore(root)).State.DiscoverySnapshot!.SteamGames.Count == 0, "Complete source did not commit removal");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
