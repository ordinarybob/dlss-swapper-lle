using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
using DLSS_Swapper.Data.Streamline;
using DlssSwapper.Linux.Cli;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--state-worker")
            return StateProcessTests.Worker(args[1], args[2]);
        if (args.Length == 1 && args[0] == "--streamline-acquisition-tests")
        {
            await StreamlineAcquisitionTests.RunAsync();
            Console.WriteLine("PASS: Streamline release selection and acquisition");
            return 0;
        }
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("All translation bundles, fallback, RTL and saved language", TranslationsTests.RunAsync),
            ("Ignored roots, descendants, scan exclusion and restored membership", IgnoredPathsTests.RunAsync),
            ("Version transition refresh, validation, retry and same-version persistence", VersionTransitionTests.RunAsync),
            ("Translation JSON/CSV round-trip, ZIP export and failed-load preservation", TranslationDocumentTests.RunAsync),
            ("Network test mapping, transfer outcomes, isolated User-Agent and cancellation", NetworkTestsTests.RunAsync),
            ("Proxy address validation, credential routing, persistence and reset", ProxySettingsTests.RunAsync),
            ("NVIDIA server listing filters, versions, pagination, cancellation and failures", NgxModelServerTests.RunAsync),
            ("Discovery cache reconciliation requires complete source evidence", DiscoveryCacheTests.RunAsync),
            ("Batch history saves together or preserves prior state", BatchHistoryTests.RunAsync),
            ("DLL staging preserves destinations on failure", DllFileReplacementTests.RunAsync),
            ("Batch workers preserve bounds, overlap serialization and cancellation results", BatchConcurrencyTests.RunAsync),
            ("Local report saves preserve destination on failure", OperationReportSaveTests.RunAsync),
            ("Steam VDF/ACF fixture discovery", RunSync(TestSteamDiscovery)),
            ("Immediate-root selection deduplicates", RunSync(TestImmediateRootSelection)),
            ("Scanner skips directory symlinks", RunSync(TestScannerSkipsSymlinks)),
            ("Detected-family and compatibility planning", RunSync(TestPlanning)),
            ("Dry-run planner does not mutate", RunSync(TestPlannerDoesNotMutate)),
            ("CLI dry-run never reaches cache or writes", TestCliDryRunBoundaryAsync),
            ("Mutation boundary rejects tampering", TestMutationBoundaryAsync),
            ("Adjacent backup, update, and restore", TestUpdateAndRestoreAsync),
            ("Restore preview rejects stale files and preserves backup semantics", RunSync(DllRestoreWorkflowTests.Run)),
            ("Persistent library state and exclusions", RunSync(TestPersistentLibraryState)),
            ("Provider merging preserves Steam/manual ownership and exclusions", RunSync(TestProviderMerge)),
            ("Epic prefix manifest discovery and Windows path mapping", RunSync(TestEpicDiscovery)),
            ("Legendary installed-game metadata discovery", RunSync(TestLegendaryDiscovery)),
            ("Heroic GOG discovery preserves installations without title metadata", RunSync(TestHeroicGogDiscovery)),
            ("GOG Wine registry discovery is read-only", RunSync(TestGogPrefixDiscovery)),
            ("Ubisoft configuration framing bounds and recovery", RunSync(TestUbisoftConfigurationReader)),
            ("Ubisoft prefix registry and YAML discovery", RunSync(TestUbisoftPrefixDiscovery)),
            ("EA prefix registry and installer discovery", RunSync(TestEaPrefixDiscovery)),
            ("Battle.net prefix product database discovery", RunSync(TestBattleNetPrefixDiscovery)),
            ("Shared Battle.net catalog preserves Windows definitions", RunSync(TestBattleNetCatalog)),
            ("Provider Wine requests and runner persistence", RunSync(TestProviderWineLaunch)),
            ("EA and Epic Wine URI requests", RunSync(TestProviderWineUris)),
            ("State migration preserves disk input and unavailable roots", RunSync(TestStatePreservationBoundary)),
            ("Saved notes preserve exact whitespace across restart", RunSync(TestExactNotePreservation)),
            ("History retains all games and exact details across restart", RunSync(TestFullHistoryPreservation)),
            ("Unknown saved fields survive edits, rollback and restart", RunSync(TestUnknownStateFields)),
            ("Conflicting saved records are rejected without data loss", RunSync(TestConflictingStateRecords)),
            ("Launcher exclusions preserve preferences and history", RunSync(TestExclusionPreservation)),
            ("Independent state stores reject stale and overlapping writes", RunSync(TestStaleStateWriter)),
            ("Failed state updates roll back in-memory changes", RunSync(TestStateRollback)),
            ("Failed game preference saves preserve values and allow retry", RunSync(TestGamePreferenceSaveFailure)),
            ("Manual titles persist and reject provider edits and failed saves", RunSync(TestManualTitle)),
            ("Custom covers own their copy and preserve sources on removal", CustomCoverTests.RunAsync),
            ("Displayed DLL versions sort numerically", VersionTextTests.RunAsync),
            ("Steam ownership and visible select-all policy", GameViewPolicyTests.RunAsync),
            ("Scan presentation distinguishes empty and unavailable results", ScanPresentationTests.RunAsync),
            ("Swappable filter migrates, persists and rolls back failed saves", FilterStateTests.RunAsync),
            ("Grouping shares favourites and preserves visible membership", GameGroupingTests.RunAsync),
            ("Library selection preserves data, ordering and explicit CLI discovery", LibrarySelectionTests.RunAsync),
            ("Latest acquisition keeps regular/debug tracks and truthful summaries", LatestAcquisitionTests.RunAsync),
            ("Concurrent acquisitions isolate cancellation and permit retry", ConcurrentAcquisitionTests.RunAsync),
            ("Manual import rejects partial input without changing state", RunSync(TestManualImportInputAtomicity)),
            ("Single-game editor saves atomically and rejects failed drafts", ManualGameImportTests.RunAsync),
            ("Separate processes reject stale writes and lock contention", StateProcessTests.RunAsync),
            ("Startup distinguishes catalog and state failures without writes", RunSync(TestStartupRecovery)),
            ("Concurrent state updates remain atomic", TestConcurrentStateUpdatesAsync),
            ("CLI help reflects validated Linux release", RunSync(TestValidatedCliHelp)),
            ("Windows-parity responsive grid geometry", RunSync(TestResponsiveGridLayout)),
            ("Fast scan learns deep-scan stragglers", RunSync(TestFastScanLearning)),
            ("Fast library scan records metadata without hashing", TestMetadataOnlyFastScanAsync),
            ("Matching version is a metadata-only no-op", RunSync(TestMetadataOnlyNoOp)),
            ("Initial deep scan completion is retry safe", TestRetrySafeDeepScanAsync),
            ("Library scan isolates missing game roots", TestMissingRootIsolationAsync),
            ("Artwork cache, CDN, and strict fallback", TestArtworkResolutionAsync),
            ("Battle.net artwork source order and caching", TestBattleNetArtworkAsync),
            ("Ubisoft thumbnail resolution and artwork caching", TestUbisoftArtworkAsync),
            ("EA artwork catalog matching and cache", TestEaArtworkAsync),
            ("EA local artwork fallback and content cache", TestEaLocalArtworkAsync),
            ("Windows icon reference parsing", RunSync(TestIconReference)),
            ("PE icon resource extraction and bounds", RunSync(TestPeIcons)),
            ("PE32/PE32+ named PNG/DIB icon decoding", RunSync(TestIconDecoding)),
            ("DLL catalog browsing and cache management", RunSync(TestDllLibraryCache)),
            ("DLL information retains optional metadata and full identities", RunSync(TestDllRecordDetails)),
            ("Catalog debug/trust policy defaults, combinations and persistence", RunSync(TestCatalogPolicy)),
            ("DLL/ZIP import source casing, isolation and cancellation", DllImportSourcesTests.RunAsync),
            ("DLL import cache commit, duplicates, persistence and trust boundary", TestDllImportCommitAsync),
            ("DLL ZIP export round trip and destination preservation", TestDllExportAsync),
            ("Catalog refresh validates before publishing", TestCatalogRefreshAsync),
            ("CLI saved catalog and explicit manifest precedence", TestSavedCatalogAsync),
            ("NGX configuration discovery is read-only and rejects unrelated payloads", RunSync(TestNgxDiscovery)),
            ("Acquisition reports new downloads and revalidates cache", TestAcquisitionReporting),
            ("Download summaries retain failures and cancellation", TestDownloadResults),
            ("Streamline shared metadata and cached acquisition", StreamlineAcquisitionTests.RunAsync),
            ("Linux selected Streamline apply/restore rejects stale snapshots", RunSync(TestSelectedStreamlineWorkflow)),
            ("Filesystem mount reporting and NTFS/FUSE warnings", RunSync(TestFilesystemInspection)),
            ("CLI persistent-state controls", TestCliPersistentStateAsync),
            ("Local-data reset is scoped to owned directories", RunSync(TestLocalDataReset)),
            ("Reset rejects active writers and validates all targets first", RunSync(TestResetCoordination)),
            ("Streamline package selects production binaries only", RunSync(TestStreamlineProductionExtraction)),
            ("Streamline set update, rollback, and original restore", RunSync(TestStreamlineTransactionalUpdate)),
            ("Streamline no-op, failure, recovery, and exclusion", RunSync(StreamlineSafetyTests.Run)),
            ("Shared scan rules and grid equivalence", RunSync(SharedRulesTests.Run)),
            ("Linux Streamline explicit-path workflow and dry-run", StreamlineWorkflowTests.RunAsync),
            ("Streamline cache selects finalized numeric releases", RunSync(StreamlinePackageCacheTests.Run)),
            ("Streamline decision preview and stale confirmation", RunSync(StreamlineDecisionPreviewTests.Run)),
            ("Streamline component descriptions", RunSync(StreamlineComponentDescriptionTests.Run)),
            ("Streamline selected targets and apply all", RunSync(StreamlineSelectionTests.Run)),
            ("Windows Streamline batch orchestration", StreamlineBatchWorkflowTests.Run),
            ("Linux combined batch acquisition, selection and failure isolation", LinuxBatchUpdateTests.RunAsync),
            ("Manual launch manifest validation and suggestions", RunSync(ManualLaunchTests.Run)),
            ("Linux launch requests preserve arguments and validate runners", RunSync(LinuxManualLaunchTests.Run)),
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} focused tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void TestSelectedStreamlineWorkflow()
    {
        using var temporary = new TemporaryDirectory();
        var game = Directory.CreateDirectory(Path.Combine(temporary.Path, "game")).FullName;
        var package = Directory.CreateDirectory(Path.Combine(temporary.Path, "package")).FullName;
        foreach (var name in StreamlineComponentSet.FileNames)
            File.WriteAllBytes(Path.Combine(package, name), StreamlineSafetyTests.DllBytes("new:" + name));
        var selected = Path.Combine(game, "sl.common.dll");
        var other = Path.Combine(game, "sl.reflex.dll");
        File.WriteAllBytes(selected, StreamlineSafetyTests.DllBytes("original"));
        File.WriteAllBytes(other, StreamlineSafetyTests.DllBytes("untouched"));
        var preview = StreamlineWorkflow.Preview(game, package).SelectTargets([selected]);
        var result = StreamlineWorkflow.ApplySelection(game, package, preview, false);
        Assert(result.Success && result.ComponentCount == 1, "selected update failed");
        AssertEqual("untouched", StreamlineSafetyTests.ReadLabel(other), "unselected file updated");
        var restore = StreamlineWorkflow.Preview(game, package).SelectTargets([selected]);
        Assert(StreamlineWorkflow.ApplySelection(game, package, restore, true).Success, "selected restore failed");
        AssertEqual("original", StreamlineSafetyTests.ReadLabel(selected), "original not restored");
        Assert(File.Exists(selected + StreamlineComponentSet.BackupSuffix), "first original backup lost");
        var stale = StreamlineWorkflow.Preview(game, package).SelectTargets([selected]);
        File.WriteAllBytes(selected, StreamlineSafetyTests.DllBytes("external change"));
        Assert(!StreamlineWorkflow.ApplySelection(game, package, stale, false).Success, "stale preview applied");
        AssertEqual("external change", StreamlineSafetyTests.ReadLabel(selected), "stale update overwrote external file");
    }

    private static async Task TestAcquisitionReporting()
    {
        using var temporary = new TemporaryDirectory();
        var payload = "fixture-dll"u8.ToArray();
        using var zipBuffer = new MemoryStream();
        using (var zip = new ZipArchive(zipBuffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var output = zip.CreateEntry("nvngx_dlss.dll").Open();
            output.Write(payload);
        }
        var archive = zipBuffer.ToArray();
        var entry = Candidate(DllType.Dlss, "2.0", payload) with
        {
            ZipMd5 = Convert.ToHexString(MD5.HashData(archive)),
            ZipFileSize = archive.Length,
        };
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(archive),
        });
        using var client = new HttpClient(handler);
        using var cache = new DownloadCache(client, temporary.Path);
        var progress = new List<(long Received, long Total)>();
        var first = await cache.AcquireAsync(entry, CancellationToken.None,
            (received, total) => progress.Add((received, total)));
        Assert(first.WasDownloaded, "cold acquisition not marked downloaded");
        Assert(progress.Count >= 2 && progress[0].Received == 0
            && progress[^1].Received == archive.Length
            && progress.All(item => item.Total == archive.Length && item.Received <= item.Total)
            && progress.Select(item => item.Received).SequenceEqual(progress.Select(item => item.Received).Order()),
            "transfer progress does not match received archive bytes");
        progress.Clear();
        Assert(!(await cache.AcquireAsync(entry, CancellationToken.None,
            (received, total) => progress.Add((received, total)))).WasDownloaded, "warm acquisition marked downloaded");
        Assert(progress.Count == 0, "cached acquisition reported a transfer");
        AssertEqual(1, handler.RequestCount, "warm acquisition used network");
        File.WriteAllText(first.Path, "corrupt fixture");
        Assert((await cache.AcquireAsync(entry, CancellationToken.None)).WasDownloaded, "corrupt cache was not reacquired");
        AssertEqual(2, handler.RequestCount, "completed task prevented cache revalidation");
        Assert(File.ReadAllBytes(first.Path).AsSpan().SequenceEqual(payload), "reacquired payload differs");
        using var cancelledWrite = new CancellationTokenSource(); cancelledWrite.Cancel();
        try { await CacheFileWriter.WriteAsync(first.Path, "replacement"u8.ToArray(), cancelledWrite.Token); throw new Exception("Cancelled cache write succeeded."); }
        catch (OperationCanceledException) { }
        Assert(File.ReadAllBytes(first.Path).AsSpan().SequenceEqual(payload), "cancelled cache write changed existing payload");
        var directoryTarget = Path.Combine(temporary.Path, "directory-target");
        Directory.CreateDirectory(directoryTarget);
        try { await CacheFileWriter.WriteAsync(directoryTarget, payload, CancellationToken.None); throw new Exception("Directory replaced by cache file."); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Assert(Directory.Exists(directoryTarget) && !Directory.EnumerateFiles(temporary.Path, "*.incoming-*", SearchOption.AllDirectories).Any(),
            "failed cache publication left temporary data or changed target");
    }

    private static async Task TestDownloadResults()
    {
        var entries = Enumerable.Range(1, 4).Select(i => Candidate(DllType.Dlss, $"2.{i}", $"fixture{i}")).ToArray();
        var results = await LibraryDownloadWorkflow.RunAsync(entries, (entry, _) =>
        {
            if (entry == entries[1]) throw new IOException("fixture failure");
            return Task.FromResult(new CacheAcquisition("unused", entry != entries[0]));
        }, CancellationToken.None);
        AssertEqual(4, results.Count, "failure stopped later acquisitions");
        var text = LibraryDownloadWorkflow.Describe(results);
        Assert(!text.Contains(results[0].Label), "cached item claimed as downloaded");
        Assert(text.Contains(results[2].Label) && text.Contains("fixture failure"), "summary lost results");
        using var cancel = new CancellationTokenSource();
        var calls = 0;
        var cancelled = await LibraryDownloadWorkflow.RunAsync(entries, (_, token) =>
        {
            calls++;
            cancel.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new CacheAcquisition("unused", true));
        }, cancel.Token);
        AssertEqual(1, calls, "cancel continued downloading");
        Assert(cancelled[0].Status == LibraryDownloadStatus.Cancelled
            && cancelled.Skip(1).All(item => item.Status == LibraryDownloadStatus.NotAttempted), "cancel outcomes incorrect");
        using var partialCancel = new CancellationTokenSource();
        var completed = await LibraryDownloadWorkflow.RunAsync(entries, (entry, token) =>
        {
            if (entry == entries[1]) { partialCancel.Cancel(); token.ThrowIfCancellationRequested(); }
            return Task.FromResult(new CacheAcquisition("unused", true));
        }, partialCancel.Token);
        Assert(completed[0].Status == LibraryDownloadStatus.Downloaded
            && completed[1].Status == LibraryDownloadStatus.Cancelled
            && completed.Skip(2).All(item => item.Status == LibraryDownloadStatus.NotAttempted),
            "cancellation lost completed results or continued the queue");
        Assert(LibraryDownloadWorkflow.Describe(completed).Contains(completed[0].Label),
            "cancel summary lost the acquired item");
        var retried = await LibraryDownloadWorkflow.RunAsync(entries, (entry, _) =>
            Task.FromResult(new CacheAcquisition("unused", entry != entries[0])), CancellationToken.None);
        Assert(retried[0].Status == LibraryDownloadStatus.Cached
            && retried.Skip(1).All(item => item.Status == LibraryDownloadStatus.Downloaded),
            "retry failed to retain cached completion and acquire remaining items");
    }

    private static void TestStartupRecovery()
    {
        using var temporary = new TemporaryDirectory();
        var manifest = WriteManifest(temporary.Path, "fixture"u8.ToArray());
        var store = new LibraryStateStore(Path.Combine(temporary.Path, "state"));
        var missing = LibraryStartup.Load(manifest + ".missing", store);
        Assert(!missing.Succeeded && missing.Error!.Contains("DLL catalog"), "missing catalog misclassified");
        Assert(!Directory.Exists(store.StateDirectory), "catalog failure created state directory");
        Assert(LibraryStartup.Load(manifest + ".missing", store, manifest).Succeeded, "bundled catalog fallback failed");
        Assert(!File.Exists(store.StatePath), "fallback created saved state");
        var corruptCatalog = Path.Combine(temporary.Path, "corrupt-manifest.json");
        File.WriteAllText(corruptCatalog, "{broken");
        Assert(LibraryStartup.Load(corruptCatalog, store, manifest).Succeeded, "corrupt saved catalog prevented bundled fallback");
        AssertEqual("{broken", File.ReadAllText(corruptCatalog), "fallback overwrote corrupt catalog");
        var bothFailed = LibraryStartup.Load(corruptCatalog, store, manifest + ".missing");
        Assert(!bothFailed.Succeeded && bothFailed.Error!.Contains(corruptCatalog) && bothFailed.Error.Contains(manifest + ".missing"), "dual catalog failure omitted recovery details");
        Assert(LibraryStartup.Load(manifest, store).Succeeded, "clean startup failed");
        Assert(!File.Exists(store.StatePath), "loading created default state on disk");
        Directory.CreateDirectory(store.StateDirectory);
        foreach (var invalid in new[] { "{broken", "{\"schemaVersion\":999}" })
        {
            File.WriteAllText(store.StatePath, invalid);
            var failed = LibraryStartup.Load(manifest, store);
            Assert(!failed.Succeeded && failed.Library is null, "invalid state produced a usable library");
            Assert(failed.Error!.Contains("saved library") && failed.Error.Contains(store.StatePath),
                "state error omitted classification/path");
            AssertEqual(invalid, File.ReadAllText(store.StatePath), "startup overwrote unreadable state");
            var fallbackFailed = LibraryStartup.Load(corruptCatalog, store, manifest);
            Assert(!fallbackFailed.Succeeded && fallbackFailed.Library is null && fallbackFailed.Error!.Contains("saved library"), "catalog fallback hid invalid saved library");
            AssertEqual(invalid, File.ReadAllText(store.StatePath), "catalog fallback replaced unreadable saved library");
        }
        File.WriteAllText(store.StatePath, "{\"schemaVersion\":2,\"hddMode\":true}");
        var recovered = LibraryStartup.Load(manifest, store);
        Assert(recovered.Succeeded && recovered.Library!.State.HddMode, "retry did not load repaired state");
    }

    private static void TestConflictingStateRecords()
    {
        using var temporary = new TemporaryDirectory();
        var root = Path.Combine(temporary.Path, "unavailable");
        var cases = new[]
        {
            new LinuxLibraryState { ManualGames = [new() { RootPath = root, Name = "First" }, new() { RootPath = root, Name = "Second" }] },
            new LinuxLibraryState { GamePreferences = [new() { RootPath = root, Notes = "First" }, new() { RootPath = root, Notes = "Second" }] },
            new LinuxLibraryState { GameHistory = [new() { RootPath = root, EventType = "" }] },
            new LinuxLibraryState { ManualGames = [null!] },
            new LinuxLibraryState { GamePreferences = [new() { RootPath = "", Notes = "Do not discard" }] },
        };
        foreach (var state in cases)
        {
            var store = new LibraryStateStore(temporary.Path);
            var original = JsonSerializer.Serialize(state, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            File.WriteAllText(store.StatePath, original);
            var rejected = false;
            try { store.Load(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "conflicting or malformed records were silently discarded");
            AssertEqual(original, File.ReadAllText(store.StatePath), "rejected migration changed disk input");
            rejected = false;
            try { store.Save(new LinuxLibraryState()); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "failed load allowed replacement with defaults");
            AssertEqual(original, File.ReadAllText(store.StatePath), "failed load lost recovery data");
        }
        var sameGame = new ManualGameState { RootPath = root, Name = "Same" };
        var samePreference = new GamePreferenceState { RootPath = root, Notes = "Keep" };
        var duplicates = new LinuxLibraryState
        {
            ManualGames = [sameGame, sameGame], GamePreferences = [samePreference, samePreference],
            GameHistory = [new() { RootPath = root, EventType = "event" }, new() { RootPath = root, EventType = "event" }],
        };
        var validStore = new LibraryStateStore(temporary.Path);
        File.WriteAllText(validStore.StatePath, JsonSerializer.Serialize(duplicates,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var library = new PersistentLibrary(validStore);
        AssertEqual(1, library.State.ManualGames.Count, "identical manual duplicates should normalize");
        AssertEqual(1, library.State.GamePreferences.Count, "identical preference duplicates should normalize");
        AssertEqual(2, library.State.GameHistory.Count, "same-game history must not deduplicate");
        library.Save();
        var before = File.ReadAllText(validStore.StatePath);
        var conflictRejected = false;
        try { library.UpdateState(value => value.GamePreferences.Add(new() { RootPath = root, Notes = "Conflict" })); }
        catch (InvalidDataException) { conflictRejected = true; }
        Assert(conflictRejected, "save allowed conflicting preference data");
        AssertEqual(before, File.ReadAllText(validStore.StatePath), "conflicting save changed disk");
        AssertEqual("Keep", library.State.GamePreferences.Single().Notes!, "conflicting save changed committed memory");
    }

    private static void TestExclusionPreservation()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LibraryStateStore(temporary.Path);
        var library = new PersistentLibrary(store);
        var steam = new SelectedGame("Steam", Path.Combine(temporary.Path, "steam"), "42");
        var provider = new SelectedGame("Provider", Path.Combine(temporary.Path, "provider"), null)
        {
            ProviderIdentity = new(GameProvider.Gog, "123"),
            ProviderIdentityAliases = [new(GameProvider.Epic, "alias")],
        };
        var manualRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "manual")).FullName;
        var manual = new SelectedGame("Manual", manualRoot, "manual-steam-hint");
        library.AddManualGames([manualRoot]);
        foreach (var game in new[] { steam, provider, manual })
        {
            library.UpdateGamePreference(game.RootPath, preference => { preference.Notes = "keep"; preference.IsFavorite = true; });
            library.RecordHistory(game.RootPath, "fixture", detail: "keep");
        }
        var before = File.ReadAllText(store.StatePath);
        using (var heldLock = new FileStream(Path.Combine(temporary.Path, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var rejected = false;
            try { LibraryRemovalWorkflow.Remove(library, provider); }
            catch (IOException) { rejected = true; }
            Assert(rejected, "exclusion must reject held writer lock");
            AssertEqual(before, File.ReadAllText(store.StatePath), "failed exclusion changed disk");
            AssertEqual(0, library.State.ExcludedProviderGames.Count, "failed exclusion changed memory");
        }
        foreach (var game in new[] { steam, provider })
        {
            LibraryRemovalWorkflow.Remove(library, game);
            AssertEqual("keep", library.FindGamePreference(game.RootPath)?.Notes ?? "missing", "exclusion removed preferences");
            AssertEqual(1, library.GetGameHistory(game.RootPath).Count, "exclusion removed history");
        }
        var reopened = new PersistentLibrary(new LibraryStateStore(temporary.Path));
        Assert(reopened.State.ExcludedSteamAppIds.Contains("42"), "Steam exclusion not saved");
        Assert(reopened.State.ExcludedProviderGames.Contains(provider.ProviderIdentity!), "provider exclusion not saved");
        Assert(reopened.State.ExcludedProviderGames.Contains(provider.ProviderIdentityAliases[0]), "provider alias not excluded");
        reopened.RestoreSteamGames();
        reopened.UpdateState(state => state.ExcludedProviderGames.Clear());
        foreach (var game in new[] { steam, provider })
            AssertEqual("keep", reopened.FindGamePreference(game.RootPath)?.Notes ?? "missing", "restoring exclusions lost preferences");
        var beforeManualRemoval = File.ReadAllText(store.StatePath);
        using (var heldLock = new FileStream(Path.Combine(temporary.Path, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var rejected = false;
            try { LibraryRemovalWorkflow.Remove(reopened, manual); }
            catch (IOException) { rejected = true; }
            Assert(rejected, "manual removal must reject held writer lock");
            AssertEqual(beforeManualRemoval, File.ReadAllText(store.StatePath), "failed manual removal changed disk");
            AssertEqual(1, reopened.State.ManualGames.Count, "failed manual removal lost registration");
            AssertEqual("keep", reopened.FindGamePreference(manualRoot)?.Notes ?? "missing", "failed manual removal lost notes");
            AssertEqual(1, reopened.GetGameHistory(manualRoot).Count, "failed manual removal lost history");
        }
        LibraryRemovalWorkflow.Remove(reopened, manual);
        AssertEqual(0, reopened.State.ManualGames.Count, "manual removal retained manual registration");
        Assert(reopened.FindGamePreference(manualRoot) is null && reopened.GetGameHistory(manualRoot).Count == 0, "manual removal retained owned metadata");
        Assert(!reopened.State.ExcludedSteamAppIds.Contains("manual-steam-hint"), "manual hint became a Steam exclusion");
        Assert(Directory.Exists(manualRoot), "removal deleted the game directory");
    }

    private static void TestUnknownStateFields()
    {
        using var temporary = new TemporaryDirectory();
        var root = Path.Combine(temporary.Path, "absent-game");
        var state = new LinuxLibraryState
        {
            ManualGames = [new() { Name = "Game", RootPath = root,
                Launch = new(root + ".exe", temporary.Path, [""], ManualLaunchKind.Wine, root + "-wine") }],
            GamePreferences = [new() { RootPath = root }],
            GameHistory = [new() { RootPath = root, EventType = "fixture" }],
            ExcludedProviderGames = [new(GameProvider.Gog, "123")],
            ImportedDlls = [new(DllType.Dlss, "1", 1, new string('a', 32), "", null, 1, 0, true, false, IsImported: true)],
        };
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(state,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }))!;
        var locations = new[] { "", "manualGames/0", "manualGames/0/launch", "gamePreferences/0", "gameHistory/0", "excludedProviderGames/0", "importedDlls/0" };
        System.Text.Json.Nodes.JsonNode At(System.Text.Json.Nodes.JsonNode node, string path)
        {
            if (path.Length == 0) return node;
            foreach (var part in path.Split('/'))
                node = int.TryParse(part, out var index) ? node[index]! : node[part]!;
            return node;
        }
        foreach (var location in locations)
            At(json, location)["unrecognizedFixture"] = System.Text.Json.Nodes.JsonNode.Parse("{\"text\":\"  exact  \",\"values\":[null,true,123,{\"key\":\"value\"}]}");
        var store = new LibraryStateStore(temporary.Path);
        File.WriteAllText(store.StatePath, json.ToJsonString());
        var library = new PersistentLibrary(store);
        // Extra metadata must not change launcher identity equality or exclusion lookup.
        Assert(library.State.ExcludedProviderGames.ToHashSet().Contains(new(GameProvider.Gog, "123")), "unknown metadata changed provider identity");
        Assert(state.ImportedDlls[0] == library.State.ImportedDlls[0], "unknown metadata changed DLL equality");
        AssertEqual(state.ImportedDlls[0].GetHashCode(), library.State.ImportedDlls[0].GetHashCode(), "unknown metadata changed DLL hash");
        var launch = library.State.ManualGames[0].Launch!;
        var withoutMetadata = launch with { AdditionalData = null };
        Assert(launch == withoutMetadata && launch.GetHashCode() == withoutMetadata.GetHashCode(), "unknown metadata changed launch equality");
        library.UpdateState(value => value.HddMode = true);
        var saved = File.ReadAllText(store.StatePath);
        foreach (var location in locations)
            AssertEqual(At(json, location)["unrecognizedFixture"]!.ToJsonString(),
                At(System.Text.Json.Nodes.JsonNode.Parse(saved)!, location)["unrecognizedFixture"]?.ToJsonString() ?? "missing", "unknown field lost at " + location);
        try { library.UpdateState(value => { value.HddMode = false; throw new IOException("fixture rollback"); }); }
        catch (IOException) { }
        library.Save();
        AssertEqual(saved, File.ReadAllText(store.StatePath), "rollback lost unknown data");
        var reopened = new PersistentLibrary(new LibraryStateStore(temporary.Path));
        reopened.Save();
        AssertEqual(saved, File.ReadAllText(store.StatePath), "restart lost unknown data");
    }

    private static void TestExactNotePreservation()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LibraryStateStore(temporary.Path);
        var notes = new[] { "  indented\r\n\tsecond line  \n", " \t\r\n", string.Empty };
        var source = new LinuxLibraryState
        {
            GamePreferences = notes.Select((note, index) => new GamePreferenceState
            {
                RootPath = Path.Combine(temporary.Path, $"absent-game-{index}"), Notes = note,
            }).ToList(),
        };
        var original = JsonSerializer.Serialize(source, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(store.StatePath, original);
        var loaded = store.Load();
        AssertEqual(original, File.ReadAllText(store.StatePath), "loading notes must not rewrite saved input");
        for (var index = 0; index < notes.Length; index++)
            AssertEqual(notes[index], loaded.GamePreferences.Single(p => p.RootPath == source.GamePreferences[index].RootPath).Notes!, "loaded note text changed");
        store.Save(loaded);
        var reopened = new LibraryStateStore(temporary.Path).Load();
        for (var index = 0; index < notes.Length; index++)
            AssertEqual(notes[index], reopened.GamePreferences.Single(p => p.RootPath == source.GamePreferences[index].RootPath).Notes!, "saved note text changed");
    }

    private static void TestFullHistoryPreservation()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LibraryStateStore(temporary.Path);
        var root = Path.Combine(temporary.Path, "absent-game");
        var otherRoot = Path.Combine(temporary.Path, "other-game");
        var source = new LinuxLibraryState
        {
            GameHistory = Enumerable.Range(0, 6002).Select(index => new GameHistoryState
            {
                RootPath = index % 2 == 0 ? root : otherRoot,
                EventTimeUtc = DateTimeOffset.UnixEpoch.AddSeconds(index),
                EventType = "fixture", Detail = $"  detail {index}\r\n",
            }).ToList(),
        };
        File.WriteAllText(store.StatePath, JsonSerializer.Serialize(source,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var library = new PersistentLibrary(store);
        AssertEqual(6002, library.State.GameHistory.Count, "loading truncated history");
        AssertEqual(3001, library.GetGameHistory(root).Count, "loading lost one game's history");
        AssertEqual("  detail 0\r\n", library.State.GameHistory.Single(h => h.EventTimeUtc == DateTimeOffset.UnixEpoch).Detail, "loading trimmed history detail");
        library.RecordHistory(root, "fixture", detail: "  new detail\n");
        var reopened = new PersistentLibrary(new LibraryStateStore(temporary.Path));
        AssertEqual(6003, reopened.State.GameHistory.Count, "saving truncated history");
        AssertEqual(3001, reopened.GetGameHistory(otherRoot).Count, "saving one game lost another game's history");
        AssertEqual("  new detail\n", reopened.GetGameHistory(root)[0].Detail, "new history detail changed");
        AssertEqual("  detail 0\r\n", reopened.State.GameHistory.Single(h => h.EventTimeUtc == DateTimeOffset.UnixEpoch).Detail, "oldest history entry changed");
    }

    private static void TestManualImportInputAtomicity()
    {
        using var temporary = new TemporaryDirectory();
        var valid = Directory.CreateDirectory(Path.Combine(temporary.Path, "new-game")).FullName;
        var existing = Directory.CreateDirectory(Path.Combine(temporary.Path, "existing-game")).FullName;
        var store = new LibraryStateStore(Path.Combine(temporary.Path, "state"));
        var library = new PersistentLibrary(store);
        library.AddManualGames([existing]);
        var beforeMemory = JsonSerializer.Serialize(library.State);
        var beforeDisk = File.ReadAllText(store.StatePath);

        IEnumerable<string> FailingPaths()
        {
            yield return valid;
            throw new IOException("Fixture enumeration failed.");
        }

        foreach (var paths in new IEnumerable<string>[]
                 { new[] { valid, Path.Combine(temporary.Path, "missing") }, FailingPaths() })
        {
            var rejected = false;
            try { library.AddManualGames(paths); }
            catch (IOException) { rejected = true; }
            Assert(rejected, "invalid or interrupted import must fail");
            AssertEqual(beforeMemory, JsonSerializer.Serialize(library.State), "failed input changed in-memory library");
            AssertEqual(beforeDisk, File.ReadAllText(store.StatePath), "failed input changed saved library");
        }

        using (var heldLock = new FileStream(Path.Combine(store.StateDirectory, ".state.write.lock"),
                   FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var rejected = false;
            try { library.AddManualGames([valid]); }
            catch (IOException) { rejected = true; }
            Assert(rejected, "manual import must reject a held writer lock");
            AssertEqual(beforeMemory, JsonSerializer.Serialize(library.State), "failed save changed in-memory library");
            AssertEqual(beforeDisk, File.ReadAllText(store.StatePath), "failed save changed saved library");
        }
        AssertEqual(1, library.AddManualGames([valid, existing, valid]), "valid retry adds only new roots");
        AssertEqual(2, new LibraryStateStore(store.StateDirectory).Load().ManualGames.Count, "valid retry persists both games");
    }

    private static void TestManualTitle()
    {
        using var temporary = new TemporaryDirectory();
        var root = Path.Combine(temporary.Path, "game");
        Directory.CreateDirectory(root);
        var store = new LibraryStateStore(Path.Combine(temporary.Path, "state"));
        var library = new PersistentLibrary(store);
        library.AddManualGames([root]);
        var before = File.ReadAllText(store.StatePath);
        using (var held = new FileStream(Path.Combine(Path.GetDirectoryName(store.StatePath)!, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var rejected = false;
            try { ManualGameTitle.Save(library, root, "new title"); } catch (IOException) { rejected = true; }
            Assert(rejected && library.State.ManualGames.Single().Name == "game", "Failed title save changed memory");
            AssertEqual(before, File.ReadAllText(store.StatePath), "Failed title changed saved state");
        }
        var saved = ManualGameTitle.Save(library, root, " A new title ");
        Assert(saved == "A new title", "Title did not follow existing manual-name normalization");
        Assert(new LibraryStateStore(Path.GetDirectoryName(store.StatePath)!).Load().ManualGames.Single().Name == saved, "Title did not survive restart");
        var committed = File.ReadAllText(store.StatePath);
        var denied = false;
        try { ManualGameTitle.Save(library, Path.Combine(temporary.Path, "provider"), "No"); } catch (InvalidOperationException) { denied = true; }
        Assert(denied, "Nonmanual title was accepted");
        denied = false;
        try { ManualGameTitle.Save(library, root, "  "); } catch (ArgumentException) { denied = true; }
        Assert(denied, "Empty title accepted");
        AssertEqual(committed, File.ReadAllText(store.StatePath), "Rejected title modified state");
    }

    private static void TestGamePreferenceSaveFailure()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LibraryStateStore(temporary.Path);
        var library = new PersistentLibrary(store);
        var root = Path.Combine(temporary.Path, "game");
        library.UpdateGamePreference(root, value => value.Notes = "original");
        var before = File.ReadAllText(store.StatePath);
        var artwork = Path.Combine(temporary.Path, "cover.png");
        Action<GamePreferenceState>[] edits = [value => value.Notes = "  draft\n", value => value.IsFavorite = true, value => value.IsHidden = true, value => value.CustomArtworkPath = artwork];
        using (var held = new FileStream(Path.Combine(temporary.Path, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            foreach (var edit in edits)
            {
                var rejected = false;
                try { library.UpdateGamePreference(root, edit); } catch (IOException) { rejected = true; }
                Assert(rejected, "Locked preference save succeeded");
                var current = library.FindGamePreference(root)!;
                Assert(current.Notes == "original" && !current.IsFavorite && !current.IsHidden && current.CustomArtworkPath is null, "Failed edit remained in memory");
                AssertEqual(before, File.ReadAllText(store.StatePath), "Failed edit changed saved bytes");
            }
        }
        foreach (var edit in edits) library.UpdateGamePreference(root, edit);
        var reopened = new PersistentLibrary(new LibraryStateStore(temporary.Path)).FindGamePreference(root)!;
        Assert(reopened.Notes == "  draft\n" && reopened.IsFavorite && reopened.IsHidden && reopened.CustomArtworkPath == artwork, "Retry did not preserve all edits");
        var saved = File.ReadAllText(store.StatePath);
        using (var held = new FileStream(Path.Combine(temporary.Path, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { library.RecordHistory(root, "Cover changed", "Artwork", detail: artwork); }
            catch (IOException) { }
            Assert(library.GetGameHistory(root).Count == 0, "Failed history remained in memory");
            AssertEqual(saved, File.ReadAllText(store.StatePath), "History failure changed the saved cover");
            Assert(library.FindGamePreference(root)!.CustomArtworkPath == artwork, "History failure rolled back the committed cover");
        }
    }

    private static void TestStateRollback()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LibraryStateStore(temporary.Path);
        var library = new PersistentLibrary(store);
        library.Save();
        var before = File.ReadAllText(store.StatePath);
        using (var heldLock = new FileStream(Path.Combine(temporary.Path, ".state.write.lock"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var rejected = false;
            try { library.UpdateState(state => state.HddMode = true); }
            catch (IOException) { rejected = true; }
            Assert(rejected, "save under lock should fail");
            Assert(!library.State.HddMode, "failed save left unsaved setting in memory");
            var cliRejected = false;
            try { DlssSwapper.Linux.Cli.Program.ChangeStringState(library, state => state.AdditionalSteamRoots, temporary.Path, true, "Steam root"); }
            catch (IOException) { cliRejected = true; }
            Assert(cliRejected && library.State.AdditionalSteamRoots.Count == 0, "Failed CLI list edit left pending state");
            library.State.GridView = false;
            try { library.Save(); } catch (IOException) { }
            Assert(library.State.GridView, "direct failed save left unsaved setting in memory");
        }
        AssertEqual(before, File.ReadAllText(store.StatePath), "failed save changed disk");
        DlssSwapper.Linux.Cli.Program.ChangeStringState(library, state => state.AdditionalSteamRoots, temporary.Path, true, "Steam root");
        Assert(new LibraryStateStore(temporary.Path).Load().AdditionalSteamRoots.SequenceEqual([temporary.Path]), "CLI retry used a detached list after rollback");
        DlssSwapper.Linux.Cli.Program.ChangeStringState(library, state => state.AdditionalSteamRoots, temporary.Path, false, "Steam root");
        try
        {
            library.UpdateState(state =>
            {
                state.HddMode = true;
                throw new InvalidOperationException("fixture callback failure");
            });
        }
        catch (InvalidOperationException) { }
        Assert(!library.State.HddMode, "throwing callback left changed state");
        library.UpdateState(state => state.HddMode = true);
        Assert(new LibraryStateStore(temporary.Path).Load().HddMode, "retry after failure did not persist");
    }

    private static void TestStaleStateWriter()
    {
        using var temporary = new TemporaryDirectory();
        var first = new LibraryStateStore(temporary.Path);
        var second = new LibraryStateStore(temporary.Path);
        var firstState = first.Load();
        var secondState = second.Load();
        firstState.HddMode = true;
        first.Save(firstState);
        var committed = File.ReadAllText(first.StatePath);
        secondState.GridView = false;
        var rejected = false;
        try { second.Save(secondState); }
        catch (IOException) { rejected = true; }
        Assert(rejected, "stale writer must reject a concurrent initial creation");
        AssertEqual(committed, File.ReadAllText(first.StatePath), "stale writer overwrote state");

        secondState = second.Load();
        secondState.GridView = false;
        second.Save(secondState);
        Assert(second.Load().HddMode, "reloaded writer lost existing preference");
        rejected = false;
        try { first.Save(firstState); }
        catch (IOException) { rejected = true; }
        Assert(rejected, "stale writer must reject changed existing state");

        var current = first.Load();
        using (var heldLock = new FileStream(Path.Combine(temporary.Path, ".state.write.lock"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            rejected = false;
            try { first.Save(current); }
            catch (IOException) { rejected = true; }
            Assert(rejected, "overlapping writer must not enter replacement");
        }
        first.Save(current);
        File.WriteAllText(first.StatePath, "{broken");
        try { first.Load(); } catch (InvalidDataException) { }
        rejected = false;
        try { first.Save(new LinuxLibraryState()); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected, "failed load must prevent saving defaults");
        AssertEqual("{broken", File.ReadAllText(first.StatePath), "failed load data was overwritten");
    }

    private static void TestStatePreservationBoundary()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LibraryStateStore(temporary.Path);
        var absentRoot = Path.Combine(temporary.Path, "unmounted-game");
        var original = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            gridColumns = 8,
            manualGames = new[] { new { name = "Preserved game", rootPath = absentRoot } },
            gamePreferences = new[] { new { rootPath = absentRoot, isFavorite = true, notes = "Keep these notes" } },
            gameHistory = new[] { new { rootPath = absentRoot, eventType = "Update", detail = "Keep history" } },
        });
        File.WriteAllText(store.StatePath, original);
        var interruptedPath = Path.Combine(temporary.Path, ".state.json.interrupted.tmp");
        File.WriteAllText(interruptedPath, "{incomplete");
        var migrated = store.Load();
        AssertEqual(original, File.ReadAllText(store.StatePath), "Load must not rewrite migration input");
        AssertEqual(LinuxLibraryState.CurrentSchemaVersion, migrated.SchemaVersion, "in-memory migration");
        AssertEqual(absentRoot, migrated.ManualGames.Single().RootPath, "unavailable root retained");
        AssertEqual("Keep these notes", migrated.GamePreferences.Single().Notes!, "notes retained");
        AssertEqual("Keep history", migrated.GameHistory.Single().Detail, "history retained");
        store.Save(migrated);
        AssertEqual(absentRoot, store.Load().ManualGames.Single().RootPath, "unavailable root survives save/restart");
        AssertEqual("{incomplete", File.ReadAllText(interruptedPath), "uncommitted temporary input is not adopted or deleted");

        foreach (var invalid in new[] { "{", "null", "{\"schemaVersion\":0}", "{\"schemaVersion\":999}" })
        {
            File.WriteAllText(store.StatePath, invalid);
            var rejected = false;
            try { store.Load(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, $"invalid/future state accepted: {invalid}");
            AssertEqual(invalid, File.ReadAllText(store.StatePath), "rejected state must remain untouched");
        }
    }

    private static Func<Task> RunSync(Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

    private static void TestStreamlineProductionExtraction()
    {
        using var temporary = new TemporaryDirectory();
        var archivePath = Path.Combine(temporary.Path, "streamline.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var fileName in StreamlineComponentSet.FileNames)
            {
                using (var output = archive.CreateEntry($"bin/x64/{fileName}").Open()) output.Write(StreamlineSafetyTests.DllBytes($"production:{fileName}"));
                WriteZipEntry(archive, $"bin/x64/development/{fileName}", $"development:{fileName}");
            }
        }

        var destination = Path.Combine(temporary.Path, "staged");
        var extracted = StreamlineComponentSet.ExtractProductionFiles(archivePath, destination);
        AssertEqual(StreamlineComponentSet.FileNames.Count, extracted.Count, "extracted component count");
        foreach (var path in extracted)
        {
            Assert(
                StreamlineSafetyTests.ReadLabel(path).StartsWith("production:", StringComparison.Ordinal),
                $"development binary replaced production binary: {Path.GetFileName(path)}");
        }
    }

    private static void TestStreamlineTransactionalUpdate()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "game")).FullName;
        var sourceOne = Directory.CreateDirectory(Path.Combine(temporary.Path, "sdk-one")).FullName;
        var sourceTwo = Directory.CreateDirectory(Path.Combine(temporary.Path, "sdk-two")).FullName;
        var names = StreamlineComponentSet.FileNames.Take(3).ToArray();
        var targets = new List<string>();
        foreach (var fileName in names)
        {
            var target = Path.Combine(gameRoot, fileName);
            StreamlineSafetyTests.WriteDll(target, $"original:{fileName}");
            StreamlineSafetyTests.WriteDll(Path.Combine(sourceOne, fileName), $"release-one:{fileName}");
            StreamlineSafetyTests.WriteDll(Path.Combine(sourceTwo, fileName), $"release-two:{fileName}");
            targets.Add(target);
        }

        var first = StreamlineComponentSet.UpdateExisting(sourceOne, targets);
        Assert(first.Success, first.Message);
        foreach (var target in targets)
        {
            Assert(StreamlineSafetyTests.ReadLabel(target).StartsWith("release-one:", StringComparison.Ordinal),
                "first release was not installed");
            Assert(StreamlineSafetyTests.ReadLabel(target + StreamlineComponentSet.BackupSuffix)
                    .StartsWith("original:", StringComparison.Ordinal),
                "original backup was not created");
        }

        var second = StreamlineComponentSet.UpdateExisting(sourceTwo, targets);
        Assert(second.Success, second.Message);
        foreach (var target in targets)
        {
            Assert(StreamlineSafetyTests.ReadLabel(target).StartsWith("release-two:", StringComparison.Ordinal),
                "second release was not installed");
            Assert(StreamlineSafetyTests.ReadLabel(target + StreamlineComponentSet.BackupSuffix)
                    .StartsWith("original:", StringComparison.Ordinal),
                "repeated update overwrote the original backup");
        }

        var failed = StreamlineComponentSet.UpdateExisting(
            sourceOne,
            targets,
            (index, _) =>
            {
                if (index == 1)
                {
                    throw new IOException("Injected replacement failure.");
                }
            });
        Assert(!failed.Success, "injected update unexpectedly succeeded");
        Assert(targets.All(target => StreamlineSafetyTests.ReadLabel(target).StartsWith("release-two:", StringComparison.Ordinal)),
            "failed set update was not rolled back");

        var restored = StreamlineComponentSet.RestoreOriginals(targets);
        Assert(restored.Success, restored.Message);
        foreach (var target in targets)
        {
            Assert(StreamlineSafetyTests.ReadLabel(target).StartsWith("original:", StringComparison.Ordinal),
                "original component was not restored");
            Assert(File.Exists(target + StreamlineComponentSet.BackupSuffix),
                "restore consumed the immutable original backup");
        }
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }

    private static async Task TestConcurrentStateUpdatesAsync()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LibraryStateStore(Path.Combine(temporary.Path, "state"));
        var library = new PersistentLibrary(store);
        var gameRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "game")).FullName;
        var tasks = Enumerable.Range(0, 20)
            .Select(index => Task.Run(() =>
            {
                library.UpdateState(state =>
                    state.CustomScanPatterns.Add($"fixtures/path{index}/unique/leaf"));
                library.RecordHistory(
                    gameRoot,
                    "Concurrent fixture",
                    detail: index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        var reloaded = store.Load();
        AssertEqual(20, reloaded.CustomScanPatterns.Count,
            "concurrent custom-pattern count");
        AssertEqual(20, reloaded.GameHistory.Count,
            "concurrent history count");
        Assert(
            !Directory.EnumerateFiles(store.StateDirectory, "*.tmp").Any(),
            "concurrent state update left a temporary file");
    }

    private static void TestSteamDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        var steamRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Steam")).FullName;
        var steamApps = Directory.CreateDirectory(
            Path.Combine(steamRoot, "steamapps")).FullName;
        var libraryRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Library")).FullName;
        var librarySteamApps = Directory.CreateDirectory(
            Path.Combine(libraryRoot, "steamapps")).FullName;
        var gamePath = Directory.CreateDirectory(
            Path.Combine(librarySteamApps, "common", "Fixture Game")).FullName;
        var escapedLinkCreated = false;
        var externalGames = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "ExternalGames")).FullName;
        Directory.CreateDirectory(Path.Combine(externalGames, "Escaped Game"));
        try
        {
            Directory.CreateSymbolicLink(
                Path.Combine(librarySteamApps, "common", "Escape"),
                externalGames);
            escapedLinkCreated = true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException)
        {
            // Linux runs exercise the escape fixture; restricted hosts may skip it.
        }

        File.WriteAllText(
            Path.Combine(steamApps, "libraryfolders.vdf"),
            $$"""
              "libraryfolders"
              {
                "0"
                {
                  "path" "{{EscapeVdf(libraryRoot)}}"
                }
              }
              """);
        File.WriteAllText(
            Path.Combine(librarySteamApps, "appmanifest_4242.acf"),
            """
            "AppState"
            {
              "appid" "4242"
              "name" "Fixture Game"
              "installdir" "Fixture Game"
              "StateFlags" "4"
            }
            """);
        File.WriteAllText(
            Path.Combine(librarySteamApps, "appmanifest_4444.acf"),
            """
            "AppState"
            {
              "appid" "4444"
              "name" "Common Root"
              "installdir" "."
              "StateFlags" "4"
            }
            """);
        if (escapedLinkCreated)
        {
            File.WriteAllText(
                Path.Combine(librarySteamApps, "appmanifest_4343.acf"),
                """
                "AppState"
                {
                  "appid" "4343"
                  "name" "Escaped Game"
                  "installdir" "Escape/Escaped Game"
                  "StateFlags" "4"
                }
                """);
        }

        var result = new SteamDiscovery().Discover(new SteamDiscoveryOptions
        {
            AdditionalRoots = [steamRoot],
            IncludeDefaultRoots = false,
            HomeDirectory = temporary.Path,
            XdgDataHome = Path.Combine(temporary.Path, "xdg"),
        });
        AssertEqual(
            1,
            result.Games.Count,
            $"Steam fixture game count; warnings: {string.Join(" | ", result.Warnings)}");
        var game = result.Games[0];
        Assert(result.Sources.Any(source => source.Path == Path.GetFullPath(libraryRoot) && source.State == DiscoverySourceState.Partial),
            "Steam invalid manifest did not mark its library partial");
        var emptyLibrary = Directory.CreateDirectory(Path.Combine(temporary.Path, "empty-library")).FullName;
        Directory.CreateDirectory(Path.Combine(emptyLibrary, "steamapps"));
        var absentLibrary = Path.Combine(temporary.Path, "absent-library");
        var sourceStates = new SteamDiscovery().Discover(new SteamDiscoveryOptions
            { AdditionalRoots = [emptyLibrary, absentLibrary], IncludeDefaultRoots = false }).Sources;
        Assert(sourceStates.Any(source => source.Path == emptyLibrary && source.State == DiscoverySourceState.Complete)
            && sourceStates.Any(source => source.Path == absentLibrary && source.State == DiscoverySourceState.Unavailable),
            "Steam empty and unavailable libraries were conflated");
        AssertEqual("4242", game.AppId, "Steam app ID");
        AssertEqual(
            Path.GetFullPath(gamePath),
            Path.GetFullPath(game.InstallDirectory),
            "Steam install directory");
        Assert(
            result.Games.All(candidate => candidate.AppId != "4444"),
            "Steam discovery accepted the library common directory as a game");
        if (escapedLinkCreated)
        {
            Assert(
                result.Games.All(candidate => candidate.AppId != "4343"),
                "Steam discovery accepted an install path escaping through a symlink");
            Assert(
                result.Warnings.Any(warning => warning.Contains(
                    "escapes its library",
                    StringComparison.OrdinalIgnoreCase)),
                "Steam discovery did not report the escaping install path");
        }
    }

    private static void TestImmediateRootSelection()
    {
        using var temporary = new TemporaryDirectory();
        var root = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "games")).FullName;
        var game = Directory.CreateDirectory(
            Path.Combine(root, "Example")).FullName;
        var options = new CliOptions
        {
            Command = "scan",
        };
        options.Paths.Add(game);
        options.Roots.Add(root);

        var selected = GameSelector.Resolve(
            options,
            new SteamDiscoveryResult([], []));
        AssertEqual(1, selected.Count, "deduplicated selected game count");
        AssertEqual(
            Path.GetFullPath(game),
            Path.GetFullPath(selected[0].RootPath),
            "selected game path");
    }

    private static void TestScannerSkipsSymlinks()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "game")).FullName;
        var payload = "fixture-dll"u8.ToArray();
        File.WriteAllBytes(Path.Combine(gameRoot, "nvngx_dlss.dll"), payload);
        var outside = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "outside")).FullName;
        File.WriteAllBytes(Path.Combine(outside, "nvngx_dlss.dll"), payload);

        var linkCreated = false;
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(gameRoot, "linked"), outside);
            linkCreated = true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException)
        {
            // The fixture remains useful on hosts that cannot create symlinks.
        }

        var manifest = WriteManifest(temporary.Path, payload);
        var catalog = DllCatalog.Load(manifest);
        var scan = new DllScanner().Scan(
            new SelectedGame("Fixture", gameRoot, null),
            catalog);
        AssertEqual(1, scan.Dlls.Count, "detected DLL count");
        if (linkCreated)
        {
            Assert(
                scan.Warnings.Any(warning => warning.Contains(
                    "symbolic link",
                    StringComparison.OrdinalIgnoreCase)),
                "scanner should report the skipped symlink");
        }
    }

    private static void TestPlanning()
    {
        var game = new SelectedGame("Fixture", "/fixture", null);
        var dlssCandidate = Candidate(DllType.Dlss, "3.0.0.0", "A");
        var frameCandidate = Candidate(DllType.DlssFrameGeneration, "3.0.0.0", "B");
        var frameTarget = new DetectedDll(
            DllType.DlssFrameGeneration,
            "/fixture/nvngx_dlssg.dll",
            "nvngx_dlssg.dll",
            frameCandidate.Md5,
            "2.0.0.0");
        var detectedOnlyPlan = new UpdatePlanner().Plan(
            [new ScanResult(game, [frameTarget], [])],
            new Dictionary<DllType, DllCatalogEntry>
            {
                [DllType.Dlss] = dlssCandidate,
                [DllType.DlssFrameGeneration] = frameCandidate,
            });
        var current = AssertSingle(detectedOnlyPlan);
        AssertEqual(
            DllType.DlssFrameGeneration,
            current.Family.Type,
            "detected family");
        AssertEqual(
            UpdatePlanStatus.AlreadyCurrent,
            current.Status,
            "true no-op status");

        var mixedPlan = new UpdatePlanner().Plan(
            [
                new ScanResult(
                    game,
                    [
                        new DetectedDll(
                            DllType.Dlss,
                            "/fixture/a/nvngx_dlss.dll",
                            "a/nvngx_dlss.dll",
                            HashOf("one"),
                            "1.5.0.0"),
                        new DetectedDll(
                            DllType.Dlss,
                            "/fixture/b/nvngx_dlss.dll",
                            "b/nvngx_dlss.dll",
                            HashOf("two"),
                            "3.0.0.0"),
                    ],
                    []),
            ],
            new Dictionary<DllType, DllCatalogEntry>
            {
                [DllType.Dlss] = dlssCandidate,
            });
        AssertEqual(
            UpdatePlanStatus.Skipped,
            AssertSingle(mixedPlan).Status,
            "mixed DLSS status");

        var unknownPlan = new UpdatePlanner().Plan(
            [
                new ScanResult(
                    game,
                    [
                        new DetectedDll(
                            DllType.Dlss,
                            "/fixture/nvngx_dlss.dll",
                            "nvngx_dlss.dll",
                            HashOf("unknown"),
                            "unknown"),
                    ],
                    []),
            ],
            new Dictionary<DllType, DllCatalogEntry>
            {
                [DllType.Dlss] = dlssCandidate,
            });
        AssertEqual(
            UpdatePlanStatus.Skipped,
            AssertSingle(unknownPlan).Status,
            "unknown DLSS generation status");
    }

    private static void TestPlannerDoesNotMutate()
    {
        using var temporary = new TemporaryDirectory();
        var targetPath = Path.Combine(temporary.Path, "nvngx_dlssg.dll");
        var original = "old"u8.ToArray();
        File.WriteAllBytes(targetPath, original);
        var candidate = Candidate(DllType.DlssFrameGeneration, "3.0.0.0", "new");
        var target = new DetectedDll(
            DllType.DlssFrameGeneration,
            targetPath,
            Path.GetFileName(targetPath),
            Convert.ToHexString(MD5.HashData(original)),
            "2.0.0.0");
        var plan = new UpdatePlanner().Plan(
            [
                new ScanResult(
                    new SelectedGame("Fixture", temporary.Path, null),
                    [target],
                    []),
            ],
            new Dictionary<DllType, DllCatalogEntry>
            {
                [candidate.Type] = candidate,
            });
        AssertEqual(UpdatePlanStatus.Ready, AssertSingle(plan).Status, "planned status");
        Assert(File.ReadAllBytes(targetPath).SequenceEqual(original), "planner changed target");
        Assert(!File.Exists(targetPath + ".dlsss"), "planner created a backup");
    }

    private static async Task TestCliDryRunBoundaryAsync()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "game")).FullName;
        var targetPath = Path.Combine(gameRoot, "nvngx_dlssg.dll");
        var original = "dry-run-original"u8.ToArray();
        var replacement = "dry-run-replacement"u8.ToArray();
        File.WriteAllBytes(targetPath, original);
        var manifest = WriteManifest(temporary.Path, replacement);
        var cacheFactoryCalled = false;

        var exitCode = await DlssSwapper.Linux.Cli.Program.RunAsync(
            [
                "update",
                "--path",
                gameRoot,
                "--manifest",
                manifest,
                "--dry-run",
            ],
            includeDefaultSteamRoots: false,
            () =>
            {
                cacheFactoryCalled = true;
                throw new InvalidOperationException("Dry-run reached the download/cache boundary.");
            }).ConfigureAwait(false);

        AssertEqual(0, exitCode, "CLI dry-run exit code");
        Assert(!cacheFactoryCalled, "CLI dry-run constructed the download cache");
        Assert(File.ReadAllBytes(targetPath).SequenceEqual(original), "CLI dry-run changed target");
        Assert(!File.Exists(targetPath + ".dlsss"), "CLI dry-run created a backup");
    }

    private static async Task TestUpdateAndRestoreAsync()
    {
        using var temporary = new TemporaryDirectory();
        var original = "original"u8.ToArray();
        var replacement = "replacement"u8.ToArray();
        var targetPath = Path.Combine(temporary.Path, "nvngx_dlssg.dll");
        var payloadPath = Path.Combine(temporary.Path, "candidate.dll");
        File.WriteAllBytes(targetPath, original);
        File.WriteAllBytes(payloadPath, replacement);

        var game = new SelectedGame("Fixture", temporary.Path, null);
        var candidate = Candidate(
            DllType.DlssFrameGeneration,
            "3.0.0.0",
            replacement);
        var target = new DetectedDll(
            candidate.Type,
            targetPath,
            Path.GetFileName(targetPath),
            Convert.ToHexString(MD5.HashData(original)),
            "2.0.0.0");
        var plan = new UpdatePlanner().Plan(
            [new ScanResult(game, [target], [])],
            new Dictionary<DllType, DllCatalogEntry>
            {
                [candidate.Type] = candidate,
            });
        var results = await DllOperations.ApplyUpdatesAsync(
            plan,
            (_, _) => Task.FromResult(payloadPath),
            CancellationToken.None).ConfigureAwait(false);
        Assert(AssertSingle(results).Success, "update failed");
        Assert(File.ReadAllBytes(targetPath).SequenceEqual(replacement), "target was not updated");
        var backupPath = targetPath + ".dlsss";
        Assert(File.ReadAllBytes(backupPath).SequenceEqual(original), "backup was not preserved");

        var secondReplacement = "replacement-two"u8.ToArray();
        var secondPayloadPath = Path.Combine(temporary.Path, "candidate-two.dll");
        File.WriteAllBytes(secondPayloadPath, secondReplacement);
        var secondCandidate = Candidate(
            DllType.DlssFrameGeneration,
            "3.1.0.0",
            secondReplacement);
        var secondTarget = target with
        {
            Md5 = candidate.Md5,
            Version = candidate.Version,
        };
        var secondPlan = new UpdatePlanner().Plan(
            [new ScanResult(game, [secondTarget], [])],
            new Dictionary<DllType, DllCatalogEntry>
            {
                [secondCandidate.Type] = secondCandidate,
            });
        var secondResults = await DllOperations.ApplyUpdatesAsync(
            secondPlan,
            (_, _) => Task.FromResult(secondPayloadPath),
            CancellationToken.None).ConfigureAwait(false);
        Assert(AssertSingle(secondResults).Success, "second update failed");
        Assert(
            File.ReadAllBytes(targetPath).SequenceEqual(secondReplacement),
            "target was not updated a second time");
        Assert(
            File.ReadAllBytes(backupPath).SequenceEqual(original),
            "second update overwrote the original backup");

        var restore = new RestorePlanItem(
            game,
            DllTypes.Get(candidate.Type),
            backupPath,
            targetPath,
            Path.GetFileName(targetPath));
        var restoreResults = DllOperations.ApplyRestores([restore]);
        Assert(AssertSingle(restoreResults).Success, "restore failed");
        Assert(File.ReadAllBytes(targetPath).SequenceEqual(original), "original was not restored");
        Assert(!File.Exists(backupPath), "restore did not consume the backup");
    }

    private static async Task TestMutationBoundaryAsync()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "game")).FullName;
        var targetPath = Path.Combine(gameRoot, "nvngx_dlssg.dll");
        var payloadPath = Path.Combine(temporary.Path, "payload.dll");
        var original = "original"u8.ToArray();
        var expected = "expected"u8.ToArray();
        var mismatched = "mismatched"u8.ToArray();
        File.WriteAllBytes(targetPath, original);
        File.WriteAllBytes(payloadPath, mismatched);

        var game = new SelectedGame("Fixture", gameRoot, null);
        var candidate = Candidate(
            DllType.DlssFrameGeneration,
            "3.0.0.0",
            expected);
        var target = new DetectedDll(
            candidate.Type,
            targetPath,
            Path.GetFileName(targetPath),
            Convert.ToHexString(MD5.HashData(original)),
            "2.0.0.0");
        var plan = new UpdatePlanner().Plan(
            [new ScanResult(game, [target], [])],
            new Dictionary<DllType, DllCatalogEntry>
            {
                [candidate.Type] = candidate,
            });

        var mismatchResults = await DllOperations.ApplyUpdatesAsync(
            plan,
            (_, _) => Task.FromResult(payloadPath),
            CancellationToken.None).ConfigureAwait(false);
        Assert(!AssertSingle(mismatchResults).Success, "mismatched payload was accepted");
        Assert(
            File.ReadAllBytes(targetPath).SequenceEqual(original),
            "mismatched payload changed the target");
        Assert(!File.Exists(targetPath + ".dlsss"), "mismatched payload created a backup");

        File.WriteAllBytes(payloadPath, expected);
        var outsidePath = Path.Combine(temporary.Path, "outside.dll");
        var outside = "outside"u8.ToArray();
        File.WriteAllBytes(outsidePath, outside);
        var linkCreated = false;
        File.Delete(targetPath);
        try
        {
            File.CreateSymbolicLink(targetPath, outsidePath);
            linkCreated = true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException)
        {
            File.WriteAllBytes(targetPath, original);
        }

        if (linkCreated)
        {
            var linkResults = await DllOperations.ApplyUpdatesAsync(
                plan,
                (_, _) => Task.FromResult(payloadPath),
                CancellationToken.None).ConfigureAwait(false);
            Assert(!AssertSingle(linkResults).Success, "symlink target was updated");
            Assert(
                File.ReadAllBytes(outsidePath).SequenceEqual(outside),
                "update followed a symlink outside the game");
            Assert(!File.Exists(targetPath + ".dlsss"), "symlink update created a backup");

            var backupPath = targetPath + ".dlsss";
            File.WriteAllBytes(backupPath, original);
            var restoreResults = DllOperations.ApplyRestores(
            [
                new RestorePlanItem(
                    game,
                    DllTypes.Get(candidate.Type),
                    backupPath,
                    targetPath,
                    Path.GetFileName(targetPath)),
            ]);
            Assert(!AssertSingle(restoreResults).Success, "restore accepted a symlink target");
            Assert(File.Exists(backupPath), "rejected restore consumed the backup");
            Assert(
                File.ReadAllBytes(outsidePath).SequenceEqual(outside),
                "restore followed a symlink outside the game");
        }
    }

    private static void TestPersistentLibraryState()
    {
        using var temporary = new TemporaryDirectory();
        var stateDirectory = Path.Combine(temporary.Path, "state");
        var manualRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Manual Game")).FullName;
        var groupRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Grouped Games")).FullName;
        var childOne = Directory.CreateDirectory(Path.Combine(groupRoot, "One")).FullName;
        var childTwo = Directory.CreateDirectory(Path.Combine(groupRoot, "Two")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(childOne, "Nested")).FullName;

        var store = new LibraryStateStore(stateDirectory);
        var library = new PersistentLibrary(store);
        AssertEqual(5, library.State.CardSize, "default grid card size");
        Assert(library.State.GridView, "default view is not grid");
        AssertEqual(15, library.State.Performance.ScanConcurrency, "standard scan concurrency");
        AssertEqual(38, library.State.Performance.ArtworkConcurrency, "standard art concurrency");
        var profile = new LinuxLibraryState { ScanConcurrency = 7, ArtworkConcurrency = 9 };
        profile.ApplyStorageProfile(true);
        Assert(profile.Performance == PerformanceLimits.Hdd && profile.HasSelectedStorageProfile, "HDD preset retained custom worker limits");
        profile.ScanConcurrency = 7; profile.ArtworkConcurrency = 9;
        profile.ApplyStorageProfile(false);
        Assert(profile.Performance == PerformanceLimits.Standard, "Standard preset retained custom worker limits");
        AssertEqual(1, library.AddManualGames([manualRoot, manualRoot]), "manual path deduplication");
        AssertEqual(2, library.AddImmediateChildren(groupRoot), "immediate child import");

        library.UpdateState(state =>
        {
            state.HddMode = true;
            state.HasCompletedInitialDeepScan = true;
            state.HasSelectedStorageProfile = true;
            state.AdditionalSteamRoots.Add(Path.Combine(temporary.Path, "Steam"));
            state.CustomScanPatterns.Add("*/custom/runtime");
            state.MediaWikiApiEndpoint = "https://example.invalid/w/api.php";
            state.MediaWikiImageHost = "images.example.invalid";
        });
        library.UpdateGamePreference(manualRoot, preference =>
        {
            preference.IsFavorite = true;
            preference.IsHidden = true;
            preference.Notes = "Fixture notes";
            preference.CustomArtworkPath = Path.Combine(temporary.Path, "cover.png");
        });
        library.RecordHistory(
            manualRoot,
            "DLL detected",
            "DLSS",
            "3.10.7",
            "Fixture history");
        AssertEqual(2, library.State.Performance.ScanConcurrency, "HDD scan concurrency");
        AssertEqual(1, library.State.Performance.ArtworkConcurrency, "HDD art concurrency");
        Assert(library.ExcludeSteamGame("4242"), "Steam exclusion was not added");

        var steamRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Steam Game")).FullName;
        var discovery = new SteamDiscoveryResult(
            [new SteamGame("4242", "Steam Fixture", steamRoot, temporary.Path, "fixture.acf")],
            []);
        var merged = library.Merge(discovery);
        AssertEqual(3, merged.Count, "manual games after Steam exclusion");
        Assert(merged.All(game => game.SteamAppId != "4242"), "excluded Steam game was merged");
        Assert(
            merged.All(game => !game.RootPath.EndsWith("Nested", StringComparison.Ordinal)),
            "multi-game import searched nested folders");

        var reloaded = new PersistentLibrary(store);
        Assert(reloaded.State.HddMode, "HDD mode was not persisted");
        Assert(reloaded.State.HasCompletedInitialDeepScan, "deep-scan completion was not persisted");
        Assert(reloaded.State.HasSelectedStorageProfile, "storage-profile selection was not persisted");
        AssertEqual(3, reloaded.State.ManualGames.Count, "persisted manual game count");
        AssertEqual(1, reloaded.State.CustomScanPatterns.Count, "persisted custom pattern count");
        var reloadedPreference = reloaded.FindGamePreference(manualRoot)
            ?? throw new InvalidOperationException("Persisted preference was not found.");
        Assert(reloadedPreference.IsFavorite, "favorite was not persisted");
        Assert(reloadedPreference.IsHidden, "hidden state was not persisted");
        Assert(reloadedPreference.Notes == "Fixture notes", "notes were not persisted");
        var history = reloaded.GetGameHistory(manualRoot);
        AssertEqual(1, history.Count, "history event count");
        AssertEqual("3.10.7", history[0].Version, "history version");
        AssertEqual(
            "https://example.invalid/w/api.php",
            reloaded.State.MediaWikiApiEndpoint,
            "MediaWiki endpoint");
        AssertEqual(
            "images.example.invalid",
            reloaded.State.MediaWikiImageHost,
            "MediaWiki image host");
        AssertEqual(1, reloaded.RestoreSteamGames(), "restored Steam exclusion count");
        AssertEqual(4, reloaded.Merge(discovery).Count, "restored Steam game merge");
        Assert(
            LibraryStateStore.TryValidateArtworkSource(
                "https://wiki.example.invalid/w/api.php",
                "uploads.example.invalid",
                out var validatedEndpoint,
                out var validatedHost,
                out _),
            "valid artwork source was rejected");
        AssertEqual(
            "https://wiki.example.invalid/w/api.php",
            validatedEndpoint,
            "validated artwork endpoint");
        AssertEqual("uploads.example.invalid", validatedHost, "validated artwork host");
        Assert(
            !LibraryStateStore.TryValidateArtworkSource(
                "http://wiki.example.invalid/w/api.php?unsafe=1",
                "https://uploads.example.invalid/path",
                out _,
                out _,
                out _),
            "unsafe artwork source was accepted");

        var stateJson = File.ReadAllText(store.StatePath);
        Assert(!stateJson.Contains("gridColumns", StringComparison.Ordinal),
            "legacy grid columns remained in persisted state");
        Assert(!stateJson.Contains("gridRows", StringComparison.Ordinal),
            "legacy grid rows remained in persisted state");
        Assert(!stateJson.Contains(nested, StringComparison.Ordinal),
            "nested folder leaked into persisted imports");
        Assert(
            !Directory.EnumerateFiles(stateDirectory, "*.tmp").Any(),
            "atomic state save left a temporary file");
        Assert(Directory.Exists(childTwo), "fixture child unexpectedly missing");

        var legacyStateDirectory = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "legacy-state")).FullName;
        File.WriteAllText(
            Path.Combine(legacyStateDirectory, "state.json"),
            "{\"schemaVersion\":1,\"gridColumns\":8,\"gridRows\":4}");
        var legacyStore = new LibraryStateStore(legacyStateDirectory);
        var migratedState = legacyStore.Load();
        AssertEqual(3, migratedState.CardSize, "legacy grid-density migration");
        AssertEqual(
            LinuxLibraryState.CurrentSchemaVersion,
            migratedState.SchemaVersion,
            "migrated state schema");
        legacyStore.Save(migratedState);
        var migratedJson = File.ReadAllText(legacyStore.StatePath);
        Assert(!migratedJson.Contains("gridColumns", StringComparison.Ordinal),
            "migrated state retained legacy columns");
        Assert(!migratedJson.Contains("gridRows", StringComparison.Ordinal),
            "migrated state retained legacy rows");
    }

    private static void TestResponsiveGridLayout()
    {
        var standard = ResponsiveGridLayout.Calculate(
            viewportWidth: 736,
            cardSize: 5,
            rasterizationScale: 1);
        AssertEqual(6, standard.ColumnCount, "standard grid column count");
        Assert(
            Math.Abs((standard.CardHeight / standard.CardWidth) - 1.5) < 0.000001,
            "grid card ratio is not 2:3");
        Assert(
            standard.CellWidth * standard.ColumnCount <= 735.000001,
            "standard grid did not retain one physical pixel");

        var wide = ResponsiveGridLayout.Calculate(
            viewportWidth: 1600,
            cardSize: 5,
            rasterizationScale: 1);
        Assert(
            wide.ColumnCount > standard.ColumnCount,
            "responsive grid did not add columns for a wider viewport");

        var smallest = ResponsiveGridLayout.Calculate(736, cardSize: 1, rasterizationScale: 1);
        var largest = ResponsiveGridLayout.Calculate(736, cardSize: 10, rasterizationScale: 1);
        AssertEqual(10, smallest.ColumnCount, "smallest card-size column count");
        AssertEqual(1, largest.ColumnCount, "largest card-size column count");

        const double fractionalWidth = 743.5;
        const double fractionalScale = 1.5;
        var fractional = ResponsiveGridLayout.Calculate(
            fractionalWidth,
            cardSize: 5,
            rasterizationScale: fractionalScale);
        var usedPhysicalPixels = fractional.CellWidth
            * fractional.ColumnCount
            * fractionalScale;
        var maximumPhysicalPixels = Math.Floor(fractionalWidth * fractionalScale) - 1;
        Assert(
            usedPhysicalPixels <= maximumPhysicalPixels + 0.000001,
            "fractional grid exceeded its physical-pixel authority");
        Assert(
            fractional.CardWidth >= ResponsiveGridLayout.MinimumCardWidth,
            "grid card fell below the 44-DIP floor");
    }

    private static void TestValidatedCliHelp()
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            CliHelp.Write();
        }
        finally
        {
            Console.SetOut(original);
        }

        var help = output.ToString();
        Assert(help.Contains("DLSS Swapper LLE Linux CLI", StringComparison.Ordinal),
            "CLI help omitted the product identity");
        Assert(!help.Contains("UNTESTED ON LINUX", StringComparison.OrdinalIgnoreCase),
            "CLI help retained the obsolete untested warning");
    }

    private static void TestFastScanLearning()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Fixture Game")).FullName;
        var knownDirectory = Directory.CreateDirectory(Path.Combine(
            gameRoot,
            "Project",
            "Engine",
            "Plugins",
            "Runtime",
            "NVIDIA",
            "DLSS",
            "Binaries",
            "ThirdParty",
            "Win64")).FullName;
        var stragglerDirectory = Directory.CreateDirectory(Path.Combine(
            gameRoot,
            "Project",
            "Custom",
            "Layer",
            "Runtime")).FullName;
        var knownDll = Path.Combine(knownDirectory, "nvngx_dlss.dll");
        var stragglerDll = Path.Combine(stragglerDirectory, "nvngx_dlssg.dll");
        File.WriteAllBytes(knownDll, "known"u8.ToArray());
        File.WriteAllBytes(stragglerDll, "straggler"u8.ToArray());

        var fast = FastScanPatternIndex.EnumerateFastCandidates(gameRoot);
        AssertEqual(1, fast.Files.Count, "initial fast candidate count");
        Assert(
            Path.GetFullPath(knownDll).Equals(
                Path.GetFullPath(AssertSingle(fast.Files)),
                StringComparison.OrdinalIgnoreCase),
            "case-insensitive POSIX path pattern did not find the built-in layout");

        var deep = FastScanPatternIndex.EnumerateDeepCandidates(gameRoot);
        AssertEqual(2, deep.Files.Count, "deep candidate count");
        Assert(
            FastScanPatternIndex.TryCreateAdaptivePattern(
                gameRoot,
                stragglerDll,
                out var learnedPattern),
            "straggler pattern was not learned");
        AssertEqual("*/custom/layer/runtime", learnedPattern, "learned pattern");

        var normalized = FastScanPatternIndex.NormalizeCustomPatterns(
            [learnedPattern, learnedPattern, "*/*/layer/runtime"]);
        AssertEqual(1, normalized.Count, "custom pattern antichain count");
        AssertEqual("*/*/layer/runtime", normalized[0], "broader retained pattern");

        var learnedFast = FastScanPatternIndex.EnumerateFastCandidates(gameRoot, normalized);
        AssertEqual(2, learnedFast.Files.Count, "learned fast candidate count");
        Assert(
            learnedFast.Files.Any(path => PathComparersForTests.Equals(path, stragglerDll)),
            "learned fast scan missed the straggler");
    }

    private static async Task TestMetadataOnlyFastScanAsync()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Metadata Game")).FullName;
        var dllPath = Path.Combine(gameRoot, "nvngx_dlssg.dll");
        var payload = "metadata-only"u8.ToArray();
        File.WriteAllBytes(dllPath, payload);
        var catalog = DllCatalog.Load(WriteManifest(temporary.Path, payload));
        var state = new LinuxLibraryState();
        var progressEvents = new List<LibraryScanProgress>();
        var service = new LibraryScanService(catalog);
        var result = await service.ScanFastAsync(
            [new SelectedGame("Metadata Game", gameRoot, null)],
            state,
            new Progress<LibraryScanProgress>(progressEvents.Add)).ConfigureAwait(false);

        var detected = AssertSingle(AssertSingle(result.Games).Dlls);
        Assert(!detected.HasHash, "fast scan hashed a DLL body");
        AssertEqual(string.Empty, detected.Md5, "fast scan MD5 placeholder");
        AssertEqual(payload.LongLength, detected.FileLength, "fast scan file length");
        AssertEqual(File.GetLastWriteTimeUtc(dllPath), detected.LastWriteTimeUtc, "fast scan write time");
        AssertEqual(0, result.LearnedPatternCount, "fast scan learned a pattern");
    }

    private static void TestMetadataOnlyNoOp()
    {
        var game = new SelectedGame("Fixture", "/fixture", null);
        var candidate = Candidate(DllType.DlssFrameGeneration, "3.10.0.0", "candidate");
        var detected = new DetectedDll(
            candidate.Type,
            "/fixture/nvngx_dlssg.dll",
            "nvngx_dlssg.dll",
            string.Empty,
            candidate.Version,
            123,
            DateTime.UnixEpoch);
        var plan = new UpdatePlanner().Plan(
            [new ScanResult(game, [detected], [])],
            new Dictionary<DllType, DllCatalogEntry> { [candidate.Type] = candidate });
        AssertEqual(UpdatePlanStatus.AlreadyCurrent, AssertSingle(plan).Status, "metadata no-op");
        Assert(!detected.HasHash, "metadata no-op acquired a hash");

        var exactDifferentBuild = detected with
        {
            Md5 = new string('A', 32),
        };
        var exactPlan = new UpdatePlanner().Plan(
            [new ScanResult(game, [exactDifferentBuild], [])],
            new Dictionary<DllType, DllCatalogEntry> { [candidate.Type] = candidate });
        AssertEqual(
            UpdatePlanStatus.Ready,
            AssertSingle(exactPlan).Status,
            "known different same-version build");
    }

    private static async Task TestRetrySafeDeepScanAsync()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "Deep Game")).FullName;
        var stragglerDirectory = Directory.CreateDirectory(Path.Combine(
            gameRoot,
            "Project",
            "Unusual",
            "Vendor",
            "Runtime")).FullName;
        var payload = "deep"u8.ToArray();
        File.WriteAllBytes(Path.Combine(stragglerDirectory, "nvngx_dlssg.dll"), payload);
        var catalog = DllCatalog.Load(WriteManifest(temporary.Path, payload));
        var store = new LibraryStateStore(Path.Combine(temporary.Path, "state"));
        var library = new PersistentLibrary(store);
        var service = new LibraryScanService(catalog);
        var games = new[] { new SelectedGame("Deep Game", gameRoot, null) };

        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            try
            {
                await service.ScanDeepAsync(
                    games,
                    library,
                    cancellationToken: canceled.Token).ConfigureAwait(false);
                throw new InvalidOperationException("Canceled deep scan unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                // Expected. The completion flag must remain false.
            }
        }

        Assert(!new PersistentLibrary(store).State.HasCompletedInitialDeepScan,
            "canceled deep scan committed completion");
        var completed = await service.ScanDeepAsync(games, library).ConfigureAwait(false);
        AssertEqual(1, completed.LearnedPatternCount, "deep scan learned pattern count");
        var reloaded = new PersistentLibrary(store);
        Assert(reloaded.State.HasCompletedInitialDeepScan, "completed deep scan was not committed");
        AssertEqual(1, reloaded.State.CustomScanPatterns.Count, "learned pattern persistence");

        var nextFast = await service.ScanFastAsync(games, reloaded.State).ConfigureAwait(false);
        AssertEqual(1, AssertSingle(nextFast.Games).Dlls.Count, "next fast scan missed learned path");
        AssertEqual(0, nextFast.LearnedPatternCount, "next fast scan relearned a pattern");
    }

    private static async Task TestMissingRootIsolationAsync()
    {
        using var temporary = new TemporaryDirectory();
        var gameRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "Live Game")).FullName;
        var payload = "live"u8.ToArray();
        File.WriteAllBytes(Path.Combine(gameRoot, "nvngx_dlssg.dll"), payload);
        var catalog = DllCatalog.Load(WriteManifest(temporary.Path, payload));
        var missingRoot = Path.Combine(temporary.Path, "Missing Game");
        var streamlineRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "Streamline Game")).FullName;
        File.WriteAllBytes(Path.Combine(streamlineRoot, "sl.common.dll"), payload);
        var result = await new LibraryScanService(catalog).ScanFastAsync(
            [
                new SelectedGame("Missing Game", missingRoot, null),
                new SelectedGame("Live Game", gameRoot, null),
                new SelectedGame("Streamline Game", streamlineRoot, null),
            ],
            new LinuxLibraryState()).ConfigureAwait(false);

        AssertEqual(3, result.Games.Count, "isolated scan result count");
        var streamline = result.Games.Single(scan => scan.Game.Name == "Streamline Game");
        Assert(streamline.Dlls.Count == 0 && streamline.StreamlineFiles.Count == 1, "Streamline-only scan was lost or treated as an ordinary DLL");
        var full = new DllScanner().Scan(streamline.Game, catalog);
        Assert(full.StreamlineFiles.Count == 1 && full.Dlls.Count == 0, "Full scan lost Streamline-only membership");
        var missing = result.Games.Single(scan => scan.Game.Name == "Missing Game");
        AssertEqual(0, missing.Dlls.Count, "missing root DLL count");
        AssertEqual(1, missing.Warnings.Count, "missing root warning count");
        var live = result.Games.Single(scan => scan.Game.Name == "Live Game");
        AssertEqual(1, live.Dlls.Count, "live root DLL count");
    }

    private static byte[] CreateIconPeFixture(bool pe64 = false, bool dib = false, bool named = false)
    {
        var data = new byte[1024];
        void W16(int offset, ushort value) => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
        void W32(int offset, uint value) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
        W16(0, 0x5a4d); W32(0x3c, 0x80); W32(0x80, 0x4550);
        W16(0x84, pe64 ? (ushort)0x8664 : (ushort)0x14c); W16(0x86, 1); W16(0x94, pe64 ? (ushort)240 : (ushort)224); W16(0x96, 0x102);
        const int optional = 0x98;
        W16(optional, pe64 ? (ushort)0x20b : (ushort)0x10b); W32(optional + 32, 4096); W32(optional + 36, 512);
        W32(optional + 56, 8192); W32(optional + 60, 512); W32(optional + (pe64 ? 108 : 92), 16);
        W32(optional + (pe64 ? 128 : 112), 0x1000); W32(optional + (pe64 ? 132 : 116), 512);
        var section = optional + (pe64 ? 240 : 224);
        ".rsrc"u8.CopyTo(data.AsSpan(section)); W32(section + 8, 512); W32(section + 12, 4096);
        W32(section + 16, 512); W32(section + 20, 512); W32(section + 36, 0x40000040);
        void Entry(int offset, uint id, uint target) { W32(512 + offset, id); W32(516 + offset, target); }
        W16(512 + 14, 2); Entry(16, 3, 0x80000020); Entry(24, 14, 0x80000050);
        W16(512 + 32 + 14, 1); Entry(48, 1, 0x80000038);
        W16(512 + 56 + 14, 1); Entry(72, 1033, 128);
        W16(512 + 80 + 14, 1); Entry(96, 10, 0x80000068);
        if (named)
        {
            W16(512 + 80 + 12, 1); W16(512 + 80 + 14, 0); W32(512 + 96, 0x800000c8);
            W16(512 + 200, 4); System.Text.Encoding.Unicode.GetBytes("MAIN").CopyTo(data, 512 + 202);
        }
        W16(512 + 104 + 14, 1); Entry(120, 1033, 144);
        using var fixtureBitmap = new SkiaSharp.SKBitmap(1, 1);
        fixtureBitmap.SetPixel(0, 0, SkiaSharp.SKColors.Red);
        using var fixturePng = fixtureBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var png = fixturePng.ToArray();
        if (dib)
        {
            png = new byte[48];
            void D32(int offset, uint value) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(png.AsSpan(offset), value);
            D32(0, 40); D32(4, 1); D32(8, 2); D32(20, 4);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(png.AsSpan(12), 1);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(png.AsSpan(14), 32);
            png[42] = 255; png[43] = 255;
        }
        W32(512 + 128, 0x1180); W32(512 + 132, (uint)png.Length);
        W32(512 + 144, 0x1120); W32(512 + 148, 20);
        const int group = 0x320;
        W16(group + 2, 1); W16(group + 4, 1); data[group + 6] = 1; data[group + 7] = 1;
        W16(group + 10, 1); W16(group + 12, 32); W32(group + 14, (uint)png.Length); W16(group + 18, 1);
        png.CopyTo(data, 0x380);
        return data;
    }

    private static void TestIconDecoding()
    {
        foreach (var pe64 in new[] { false, true })
        foreach (var dib in new[] { false, true })
        foreach (var named in new[] { false, true })
        {
            using var stream = new MemoryStream(CreateIconPeFixture(pe64, dib, named));
            var ico = DlssSwapper.Shared.PeIconReader.Extract(stream);
            using var bitmap = SkiaSharp.SKBitmap.Decode(ico);
            Assert(bitmap is { Width: 1, Height: 1 }, $"Icon decode failed: PE64={pe64}, DIB={dib}, named={named}");
            if (dib) Assert(bitmap!.GetPixel(0, 0).Red == 255 && bitmap.GetPixel(0, 0).Alpha == 255, "DIB pixel conversion failed");
        }
        // Two different payload formats in one group: preserve directory order and offsets.
        var multi = CreateIconPeFixture();
        Array.Resize(ref multi, 2048);
        void W16(int offset, ushort value) => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(multi.AsSpan(offset), value);
        void W32(int offset, uint value) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(multi.AsSpan(offset), value);
        W32(0x98 + 116, 1536); W32(0x178 + 8, 1536); W32(0x178 + 16, 1536);
        multi.AsSpan(512 + 56, 24).CopyTo(multi.AsSpan(512 + 700));
        W16(512 + 32 + 14, 2); W32(512 + 52, 0x80000000u + 700);
        W32(512 + 56, 2); W32(512 + 60, 0x80000000u + 512);
        W16(512 + 512 + 14, 1); W32(512 + 528, 1033); W32(512 + 532, 544);
        W32(512 + 544, 0x1000 + 600); W32(512 + 548, 48);
        CreateIconPeFixture(dib: true).AsSpan(0x380, 48).CopyTo(multi.AsSpan(512 + 600));
        W32(512 + 148, 34); W16(0x320 + 4, 2);
        multi.AsSpan(0x320 + 6, 14).CopyTo(multi.AsSpan(0x320 + 20));
        W32(0x320 + 28, 48); W16(0x320 + 32, 2);
        var extracted = DlssSwapper.Shared.PeIconReader.Extract(new MemoryStream(multi));
        var firstLength = BitConverter.ToInt32(extracted, 14);
        Assert(BitConverter.ToUInt16(extracted, 4) == 2 && BitConverter.ToInt32(extracted, 18) == 38
            && BitConverter.ToInt32(extracted, 34) == 38 + firstLength
            && extracted.AsSpan(38, firstLength).SequenceEqual(multi.AsSpan(0x380, firstLength))
            && extracted.AsSpan(38 + firstLength).SequenceEqual(multi.AsSpan(512 + 600, 48)),
            "Multi-image ICO order, offsets or payloads changed");
        using var decodedMulti = SkiaSharp.SKBitmap.Decode(extracted);
        Assert(decodedMulti is { Width: 1, Height: 1 }, "Multi-image ICO did not decode");
    }

    private static void TestPeIcons()
    {
        var pe = CreateIconPeFixture();
        using var input = new MemoryStream(pe, writable: false);
        var ico = DlssSwapper.Shared.PeIconReader.Extract(input);
        Assert(ico[2] == 1 && ico[4] == 1 && BitConverter.ToUInt32(ico, 18) == 22
            && ico.AsSpan(22, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "ICO reconstruction failed");
        input.Position = 0;
        Assert(DlssSwapper.Shared.PeIconReader.Extract(input, -10).SequenceEqual(ico), "Resource-ID icon selection failed");
        foreach (var index in new[] { 1, -11, int.MinValue })
        {
            input.Position = 0;
            try { DlssSwapper.Shared.PeIconReader.Extract(input, index); throw new Exception("Missing icon accepted"); }
            catch (InvalidDataException) { }
        }
        var broken = pe.ToArray(); broken[0x320 + 18] = 2;
        try { DlssSwapper.Shared.PeIconReader.Extract(new MemoryStream(broken)); throw new Exception("Missing image accepted"); }
        catch (InvalidDataException) { }
        broken = pe.ToArray(); System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(broken.AsSpan(512 + 20), 0x8fffffff);
        try { DlssSwapper.Shared.PeIconReader.Extract(new MemoryStream(broken)); throw new Exception("Bad resource offset accepted"); }
        catch (InvalidDataException) { }
        Assert(pe.SequenceEqual(CreateIconPeFixture()), "PE source bytes changed during extraction");
    }

    private static void TestIconReference()
    {
        var quoted = DlssSwapper.Shared.WindowsIconReference.Parse("\"C:\\Games\\A, B\\game.exe\", -42");
        Assert(quoted.Path == @"C:\Games\A, B\game.exe" && quoted.Index == -42, "Quoted icon/resource ID parsing failed");
        Assert(DlssSwapper.Shared.WindowsIconReference.Parse(@"C:\Games\game.exe,0") is { Path: @"C:\Games\game.exe", Index: 0 },
            "Default icon index remained in filename");
        Assert(DlssSwapper.Shared.WindowsIconReference.Parse(@"C:\Games\cover,art.png").Path == @"C:\Games\cover,art.png",
            "A comma within a filename was mistaken for an index");
        Assert(DlssSwapper.Shared.WindowsIconReference.Parse("\"C:\\Games\\cover.png\"").Index == 0, "Missing index did not default to zero");
        foreach (var invalid in new[] { "", "\"unclosed", "\"path.exe\",bad", "path\0.exe", "\"path.exe\" trailing" })
        {
            try { DlssSwapper.Shared.WindowsIconReference.Parse(invalid); throw new Exception("Invalid icon reference accepted"); }
            catch (FormatException) { }
        }
    }

    private static async Task TestEaLocalArtworkAsync()
    {
        using var temporary = new TemporaryDirectory();
        var icon = Path.Combine(temporary.Path, "cover.PNG"); File.WriteAllText(icon, "first fixture");
        using var input = new MemoryStream("[]"u8.ToArray());
        using var http = new HttpClient(new StubHttpMessageHandler(_ => throw new Exception("Local EA cover reached the network")));
        using var service = new ArtworkService(http, new RecordingArtworkProcessor(), Path.Combine(temporary.Path, "cache"),
            eaCatalog: EaArtworkCatalog.Read(input));
        var game = new SelectedGame("EA fixture", temporary.Path, null)
        { ProviderIdentity = new(GameProvider.Ea, "offer"), LocalIconPath = icon };
        var first = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        var second = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        Assert(first.Origin == ArtworkOrigin.ProviderLocal && second.Origin == ArtworkOrigin.ProviderCache
            && first.Path == second.Path, "EA local image was not selected and cached");
        File.WriteAllText(icon, "changed fixture");
        var changed = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        Assert(changed.Path != first.Path && File.ReadAllText(icon) == "changed fixture", "Changed icon retained stale cache or source was modified");
        var executable = Path.Combine(temporary.Path, "fixture.exe"); File.WriteAllBytes(executable, CreateIconPeFixture());
        var extracted = await service.ResolveAsync(game with { LocalIconPath = executable, LocalIconIndex = -10 }, new LinuxLibraryState(), []);
        Assert(extracted.Origin == ArtworkOrigin.ProviderLocal && File.ReadAllBytes(extracted.Path!)[2] == 1,
            "Executable icon did not reach the artwork processor");
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(temporary.Path, "state")));
        var merged = library.Merge(new SteamDiscoveryResult([], []),
            [new ProviderGame(new(GameProvider.Ea, "offer"), "EA fixture", temporary.Path, "metadata") { LocalIconPath = icon }]);
        Assert(merged.Single().LocalIconPath == icon, "Library merge discarded provider icon metadata");
    }

    private static async Task TestEaArtworkAsync()
    {
        using var temporary = new TemporaryDirectory();
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            [{"title":"Fixture One","packArtImage":{"path":"https://example.invalid/pack.png"},"keyArtImage":{"path":"https://example.invalid/key.png"},"logoImage":{"path":"https://example.invalid/logo.png"}},
             {"title":"Fixture Two","keyArtImage":{"path":"https://example.invalid/key.png"},"logoImage":{"path":"https://example.invalid/logo.png"}},
             {"title":"Fixture Three","logoImage":{"path":"https://example.invalid/logo.png"}}]
            """));
        var catalog = EaArtworkCatalog.Read(input);
        Assert(catalog.FindCover("Fixture One") == "https://example.invalid/pack.png"
            && catalog.FindCover("Fixture Two") == "https://example.invalid/key.png"
            && catalog.FindCover("Fixture Three") == "https://example.invalid/logo.png", "EA image priority changed");
        Assert(catalog.FindCover("zzzzzzzzzzzz") is null && catalog.FindCover("") is null, "EA low-score title was accepted");
        Assert(EaArtworkCatalog.LoadDefault().Warning is null, "Bundled EA catalog is missing or unreadable");
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert(request.RequestUri!.AbsoluteUri == "https://example.invalid/pack.png", "EA artwork selected the wrong source");
            return ImageResponse("fixture"u8.ToArray(), "image/png");
        });
        using var http = new HttpClient(handler);
        using var service = new ArtworkService(http, new RecordingArtworkProcessor(), temporary.Path, eaCatalog: catalog);
        var game = new SelectedGame("Fixture One", temporary.Path, null) { ProviderIdentity = new(GameProvider.Ea, "offer-id") };
        var first = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        var second = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        Assert(first.Origin == ArtworkOrigin.Provider && second.Origin == ArtworkOrigin.ProviderCache && handler.RequestCount == 1,
            "EA provider artwork was not wired or cached");
    }

    private static async Task TestUbisoftArtworkAsync()
    {
        const string expected = "https://ubistatic3-a.akamaihd.net/orbit/uplay_launcher_3_0/assets/cover.PNG";
        Assert(UbisoftPrefixDiscovery.ResolveCover("cover.PNG", null) == expected, "Direct Ubisoft thumbnail failed");
        Assert(UbisoftPrefixDiscovery.ResolveCover("cover_key", new Dictionary<string, Dictionary<string, string>>
            { ["default"] = new() { ["cover_key"] = "cover.PNG" } }) == expected, "Localized Ubisoft thumbnail failed");
        Assert(UbisoftPrefixDiscovery.ResolveCover(null, null) is null
            && UbisoftPrefixDiscovery.ResolveCover("missing", new Dictionary<string, Dictionary<string, string>>()) is null,
            "Missing Ubisoft thumbnail should not fail discovery");
        using var temporary = new TemporaryDirectory();
        var game = new SelectedGame("Ubisoft fixture", temporary.Path, null)
            { ProviderIdentity = new(GameProvider.Ubisoft, "42"), CoverUrl = expected };
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsoluteUri == expected
            ? ImageResponse("ubisoft-fixture"u8.ToArray(), "image/png") : throw new Exception("Unexpected Ubisoft artwork source"));
        using var http = new HttpClient(handler);
        using var service = new ArtworkService(http, new RecordingArtworkProcessor(), Path.Combine(temporary.Path, "cache"));
        var first = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        var second = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        Assert(first.Origin == ArtworkOrigin.Provider && second.Origin == ArtworkOrigin.ProviderCache
            && handler.RequestCount == 1 && File.Exists(first.Path), "Ubisoft cover cache was not reused");
    }

    private static async Task TestBattleNetArtworkAsync()
    {
        using var temporary = new TemporaryDirectory();
        var game = new SelectedGame("Diablo IV", temporary.Path, null)
        { ProviderIdentity = new(GameProvider.BattleNet, "fenris"), BattleNet = new("fenris", "Fen", null, true, "https://example.invalid/cover.png") };
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert(request.RequestUri!.Host == "example.invalid", "Battle.net metadata cover was not first");
            return ImageResponse("image-fixture"u8.ToArray(), "image/png");
        });
        using var http = new HttpClient(handler);
        using var service = new ArtworkService(http, new RecordingArtworkProcessor(), Path.Combine(temporary.Path, "cache"));
        var first = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        var second = await service.ResolveAsync(game, new LinuxLibraryState(), []);
        Assert(first.Origin == ArtworkOrigin.Provider && second.Origin == ArtworkOrigin.ProviderCache
            && handler.RequestCount == 1 && File.Exists(first.Path), "Provider cover cache was not reused");
        var fallbackHandler = new StubHttpMessageHandler(request => request.RequestUri!.Host == "example.invalid"
            ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            : request.RequestUri.AbsoluteUri == "https://dlss-swapper-downloads.beeradmoore.com/images/covers/battlenet/fenris.webp"
                ? ImageResponse("fallback-fixture"u8.ToArray(), "image/webp") : throw new Exception("Unexpected artwork source"));
        using var fallbackHttp = new HttpClient(fallbackHandler);
        using var fallback = new ArtworkService(fallbackHttp, new RecordingArtworkProcessor(), Path.Combine(temporary.Path, "fallback"));
        first = await fallback.ResolveAsync(game, new LinuxLibraryState(), []);
        second = await fallback.ResolveAsync(game, new LinuxLibraryState(), []);
        Assert(first.Origin == ArtworkOrigin.Provider && second.Origin == ArtworkOrigin.ProviderCache
            && fallbackHandler.RequestCount == 2, "Battle.net fallback order or missing-cover retry cache failed");
    }

    private static async Task TestArtworkResolutionAsync()
    {
        using var temporary = new TemporaryDirectory();
        var cacheRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "cache")).FullName;
        var libraryRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "SteamLibrary")).FullName;
        var steamGamePath = Directory.CreateDirectory(Path.Combine(
            libraryRoot,
            "steamapps",
            "common",
            "Steam Fixture")).FullName;
        var steamCache = Directory.CreateDirectory(Path.Combine(
            libraryRoot,
            "appcache",
            "librarycache")).FullName;
        var localCover = Path.Combine(steamCache, "42_library_600x900.jpg");
        File.WriteAllBytes(localCover, "local-cover"u8.ToArray());
        var knownSteamGame = new SteamGame(
            "42",
            "Steam Fixture",
            steamGamePath,
            libraryRoot,
            Path.Combine(libraryRoot, "steamapps", "appmanifest_42.acf"));
        var processor = new RecordingArtworkProcessor();
        var handler = new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("Local Steam artwork reached the network."));
        using var http = new HttpClient(handler);
        using var service = new ArtworkService(
            http,
            processor,
            cacheRoot,
            TimeSpan.Zero);
        var local = await service.ResolveAsync(
            new SelectedGame("Steam Fixture", steamGamePath, "42"),
            new LinuxLibraryState(),
            [knownSteamGame]).ConfigureAwait(false);
        AssertEqual(ArtworkOrigin.SteamLocal, local.Origin, "local Steam artwork origin");
        AssertEqual(localCover, local.Path!, "local Steam artwork path");
        AssertEqual(0, handler.RequestCount, "local Steam network request count");

        var originalXdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        try
        {
            var xdgDataHome = Directory.CreateDirectory(
                Path.Combine(temporary.Path, "xdg-data")).FullName;
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", xdgDataHome);
            var primarySteamCache = Directory.CreateDirectory(Path.Combine(
                xdgDataHome,
                "Steam",
                "appcache",
                "librarycache")).FullName;
            var primaryCover = Path.Combine(primarySteamCache, "43_library_600x900.jpg");
            File.WriteAllBytes(primaryCover, "primary-cover"u8.ToArray());
            var primary = await service.ResolveAsync(
                new SelectedGame("Secondary Library Fixture", steamGamePath, "43"),
                new LinuxLibraryState(),
                []).ConfigureAwait(false);
            AssertEqual(ArtworkOrigin.SteamLocal, primary.Origin,
                "primary Steam cache artwork origin");
            AssertEqual(primaryCover, primary.Path!, "primary Steam cache artwork path");
            AssertEqual(0, handler.RequestCount, "primary Steam cache network request count");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", originalXdgDataHome);
        }

        File.Delete(localCover);
        var directBytes = "direct-jpeg"u8.ToArray();
        var directHandler = new StubHttpMessageHandler(request =>
        {
            Assert(
                request.RequestUri?.Host == "steamcdn-a.akamaihd.net",
                "unexpected direct artwork request");
            return ImageResponse(directBytes, "image/jpeg");
        });
        using var directHttp = new HttpClient(directHandler);
        using var directService = new ArtworkService(
            directHttp,
            processor,
            cacheRoot,
            TimeSpan.Zero);
        var direct = await directService.ResolveAsync(
            new SelectedGame("Steam Fixture", steamGamePath, "42"),
            new LinuxLibraryState(),
            [knownSteamGame]).ConfigureAwait(false);
        AssertEqual(ArtworkOrigin.SteamCdn, direct.Origin, "Steam CDN artwork origin");
        Assert(File.ReadAllBytes(direct.Path!).SequenceEqual(directBytes),
            "Steam JPEG was re-encoded");
        var reused = await directService.ResolveAsync(
            new SelectedGame("Steam Fixture", steamGamePath, "42"),
            new LinuxLibraryState(),
            [knownSteamGame]).ConfigureAwait(false);
        AssertEqual(ArtworkOrigin.SteamCache, reused.Origin, "Steam cache reuse origin");
        AssertEqual(1, directHandler.RequestCount, "Steam cache repeated a download");

        var manualRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "Alan Wake 2")).FullName;
        var fallbackBytes = "fallback-image"u8.ToArray();
        var fallbackHandler = new StubHttpMessageHandler(request =>
        {
            var uri = request.RequestUri
                ?? throw new InvalidOperationException("Artwork request has no URI.");
            if (uri.Host == "store.steampowered.com")
            {
                return JsonResponse("{\"items\":[]}");
            }

            if (uri.Host == "en.wikipedia.org"
                && uri.Query.Contains("generator=search", StringComparison.Ordinal))
            {
                return JsonResponse(
                    "{\"query\":{\"pages\":[{\"title\":\"Alan Wake 2\",\"images\":[{\"title\":\"File:Alan Wake 2 box art.jpg\"}]}]}}");
            }

            if (uri.Host == "en.wikipedia.org"
                && uri.Query.Contains("imageinfo", StringComparison.Ordinal))
            {
                return JsonResponse(
                    "{\"query\":{\"pages\":[{\"title\":\"File:Alan Wake 2 box art.jpg\",\"imageinfo\":[{\"url\":\"https://upload.wikimedia.org/alan-wake-2.jpg\",\"mime\":\"image/jpeg\",\"width\":800,\"height\":1200,\"size\":4096}]}]}}");
            }

            if (uri.Host == "upload.wikimedia.org")
            {
                return ImageResponse(fallbackBytes, "image/jpeg");
            }

            throw new InvalidOperationException($"Unexpected fallback request: {uri}");
        });
        using var fallbackHttp = new HttpClient(fallbackHandler);
        var fallbackProcessor = new RecordingArtworkProcessor();
        using var fallbackService = new ArtworkService(
            fallbackHttp,
            fallbackProcessor,
            cacheRoot,
            TimeSpan.Zero);
        var fallback = await fallbackService.ResolveAsync(
            new SelectedGame("Alan Wake 2", manualRoot, null),
            new LinuxLibraryState(),
            []).ConfigureAwait(false);
        AssertEqual(ArtworkOrigin.MediaWiki, fallback.Origin, "MediaWiki artwork origin");
        Assert(File.Exists(fallback.Path), "MediaWiki artwork was not cached");
        AssertEqual(1, fallbackProcessor.SaveCount, "fallback processor count");
        AssertEqual(400, fallbackProcessor.MaximumWidth, "fallback maximum width");
        AssertEqual(600, fallbackProcessor.MaximumHeight, "fallback maximum height");
        Assert(fallbackProcessor.Source.SequenceEqual(fallbackBytes),
            "fallback processor source bytes");
        var fallbackRequestCount = fallbackHandler.RequestCount;
        var fallbackReused = await fallbackService.ResolveAsync(
            new SelectedGame("Alan Wake 2", manualRoot, null),
            new LinuxLibraryState(),
            []).ConfigureAwait(false);
        AssertEqual(
            ArtworkOrigin.MediaWikiCache,
            fallbackReused.Origin,
            "MediaWiki cache reuse origin");
        AssertEqual(
            fallbackRequestCount,
            fallbackHandler.RequestCount,
            "MediaWiki cache repeated a lookup");
    }

    private static string WriteManifest(string directory, byte[] payload)
    {
        var hash = Convert.ToHexString(MD5.HashData(payload));
        var root = new Dictionary<string, object?>();
        foreach (var definition in DllTypes.All)
        {
            root[definition.ManifestKey] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["version"] = "1.0.0.0",
                    ["version_number"] = 1UL,
                    ["md5_hash"] = hash,
                    ["zip_md5_hash"] = new string('0', 32),
                    ["download_url"] = $"https://example.invalid/{definition.ManifestKey}.zip",
                    ["file_size"] = payload.Length,
                    ["zip_file_size"] = 1,
                    ["is_signature_valid"] = true,
                    ["is_dev_file"] = false,
                },
            };
        }

        var path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(root));
        return path;
    }

    private static async Task TestDllImportCommitAsync()
    {
        using var temporary = new TemporaryDirectory();
        var knownBytes = StreamlineSafetyTests.DllBytes("known");
        var manifest = WriteManifest(temporary.Path, knownBytes);
        var catalog = DllCatalog.Load(manifest);
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(temporary.Path, "state")));
        using var cache = new DownloadCache(cacheRoot: Path.Combine(temporary.Path, "cache"));
        var workflow = new DllImportWorkflow(catalog, cache, library);
        using (var input = new MemoryStream(knownBytes)) await workflow.ImportAsync("libxess.dll", input, default);
        Assert(library.State.ImportedDlls.Count == 0, "known catalog match created an imported duplicate");
        var bytes = StreamlineSafetyTests.DllBytes("unknown");
        var refused = false;
        try { using var input = new MemoryStream(bytes); await workflow.ImportAsync("libxess.dll", input, default); }
        catch (IOException) { refused = true; }
        Assert(refused && library.State.ImportedDlls.Count == 0, "unknown DLL bypassed trust setting");
        catalog.Policy = new(AllowUntrusted: true);
        using (var input = new MemoryStream(bytes)) await workflow.ImportAsync("libxess.dll", input, default);
        using (var input = new MemoryStream(bytes)) await workflow.ImportAsync("libxess.dll", input, default);
        Assert(library.State.ImportedDlls.Count == 1, "repeat import duplicated records");
        var imported = new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.ImportedDlls.Single();
        Assert(imported.IsImported && imported.DownloadUri is null && !imported.IsSignatureValid, "import provenance was lost");
        Assert((await cache.AcquireAsync(imported, default)).WasDownloaded == false, "imported DLL tried downloading");
        File.WriteAllText(cache.GetCachedPath(imported), "corrupt");
        Assert(!cache.IsCached(imported), "corrupt import was labelled ready instead of requiring reimport");
        var corruptRejected = false;
        try { await cache.AcquireAsync(imported, default); } catch (IOException) { corruptRejected = true; }
        Assert(corruptRejected, "corrupt imported cache attempted network or survived validation");

        // A stale settings writer must not publish a previously unknown cache payload.
        var staleLibrary = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
        library.UpdateState(state => state.AllowDebugDlls = !state.AllowDebugDlls);
        var otherBytes = StreamlineSafetyTests.DllBytes("stale import");
        var otherHash = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(otherBytes));
        var staleRejected = false;
        try
        {
            using var input = new MemoryStream(otherBytes);
            await new DllImportWorkflow(catalog, cache, staleLibrary).ImportAsync("libxess.dll", input, default);
        }
        catch (IOException) { staleRejected = true; }
        Assert(staleRejected && catalog.FindByHash(imported.Type, otherHash) is null,
            "stale state writer published an import record");
        Assert(!Directory.EnumerateFiles(Path.Combine(temporary.Path, "cache"), "*", SearchOption.AllDirectories)
            .Any(path => path.Contains(otherHash)), "stale state writer left an unlisted payload");

        // A cache-copy failure leaves a visible, repeatable record, never an installation success.
        var cacheBlocker = Path.Combine(temporary.Path, "blocked-cache");
        File.WriteAllText(cacheBlocker, "fixture blocker");
        using var blockedCache = new DownloadCache(cacheRoot: cacheBlocker);
        var currentLibrary = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
        var failedCopy = false;
        try
        {
            using var input = new MemoryStream(otherBytes);
            await new DllImportWorkflow(catalog, blockedCache, currentLibrary).ImportAsync("libxess.dll", input, default);
        }
        catch (IOException error) { failedCopy = error.Message.Contains("reimport"); }
        Assert(failedCopy && currentLibrary.State.ImportedDlls.Any(entry => entry.Md5 == otherHash),
            "copy failure lost its recoverable record or retry guidance");
        using (var input = new MemoryStream(otherBytes))
            await new DllImportWorkflow(catalog, cache, currentLibrary).ImportAsync("libxess.dll", input, default);
        Assert(currentLibrary.State.ImportedDlls.Count(entry => entry.Md5 == otherHash) == 1,
            "recovery duplicated imported records");
        Assert(!(await cache.AcquireAsync(catalog.FindByHash(imported.Type, otherHash)!, default)).WasDownloaded,
            "reimport did not recover the cached payload");

        catalog.Policy = new();
        var verifiedBytes = StreamlineSafetyTests.DllBytes("verifier adapter fixture");
        var verifiedHash = Convert.ToHexString(MD5.HashData(verifiedBytes));
        using (var input = new MemoryStream(verifiedBytes))
            await new DllImportWorkflow(catalog, cache, currentLibrary, new FixtureSignatureVerifier(true))
                .ImportAsync("libxess.dll", input, default);
        Assert(catalog.GetEntries(imported.Type).Any(entry => entry.Md5 == verifiedHash && entry.IsSignatureValid),
            "successful verifier result did not make the imported record eligible");
        var reopenedCatalog = DllCatalog.Load(manifest);
        foreach (var entry in new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.ImportedDlls)
            reopenedCatalog.AddImported(entry);
        Assert(reopenedCatalog.GetEntries(imported.Type).Any(entry => entry.Md5 == verifiedHash),
            "verified import lost its verification result on restart");
        var rejectedSignature = false;
        try
        {
            using var input = new MemoryStream(verifiedBytes);
            await new DllImportWorkflow(catalog, cache, currentLibrary, new FixtureSignatureVerifier(false))
                .ImportAsync("libxess.dll", input, default);
        }
        catch (IOException) { rejectedSignature = true; }
        Assert(rejectedSignature, "reimport reused an old imported signature result without verification");
        var unavailable = await new DllSignatureVerifier(Path.Combine(temporary.Path, "missing-verifier"))
            .VerifyAsync(manifest, default);
        Assert(!unavailable.IsValid && unavailable.Message.Contains("unavailable"), "missing verifier was treated as valid");
        Assert(NgxModelIdentity.Identify("NGX DL SuperSampling")?.Type == DllType.Dlss
            && NgxModelIdentity.Identify("NVIDIA DLSS Ray Reconstruction")?.Type == DllType.DlssRayReconstruction
            && NgxModelIdentity.Identify("NVIDIA DLSS-G MFGLW")?.Type == DllType.DlssFrameGeneration
            && NgxModelIdentity.Identify("unrelated") is null, "model identity mapping changed");
        catalog.Policy = new(AllowUntrusted: true);
        var modelPath = Path.Combine(temporary.Path, "unrelated.bin");
        File.WriteAllBytes(modelPath, verifiedBytes);
        var rejectedModel = false;
        try { await new DllImportWorkflow(catalog, cache, currentLibrary, new FixtureSignatureVerifier(true)).ImportModelAsync(modelPath, default); }
        catch (IOException error) { rejectedModel = error.Message.Contains("not a recognized"); }
        Assert(rejectedModel, "untrusted preference or positive signature bypassed model identity");
        var stateBefore = File.ReadAllBytes(new LibraryStateStore(library.StateDirectory).StatePath);
        var changedRejected = false;
        try { await workflow.ImportModelAsync(modelPath, default, new string('0', 32)); }
        catch (IOException error) { changedRejected = error.Message.Contains("changed after inspection"); }
        Assert(changedRejected, "model selection hash was not checked against staged bytes");
        using var stopped = new CancellationTokenSource(); stopped.Cancel();
        var wasCancelled = false;
        try { await workflow.ImportModelAsync(Path.Combine(temporary.Path, "absent.bin"), stopped.Token); }
        catch (OperationCanceledException) { wasCancelled = true; }
        Assert(wasCancelled, "cancelled model import opened its source before cancellation");
        Assert(File.ReadAllBytes(new LibraryStateStore(library.StateDirectory).StatePath).SequenceEqual(stateBefore),
            "rejected or cancelled model import changed state");
    }

    private sealed class FixtureSignatureVerifier(bool valid) : IDllSignatureVerifier
    {
        public Task<DllSignatureResult> VerifyAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new DllSignatureResult(valid, "Fixture verifier result; not a cryptographic test."));
        }
    }

    private static async Task TestDllExportAsync()
    {
        using var temporary = new TemporaryDirectory();
        var bytes = StreamlineSafetyTests.DllBytes("export fixture");
        var catalog = DllCatalog.Load(WriteManifest(temporary.Path, bytes));
        using var cache = new DownloadCache(cacheRoot: Path.Combine(temporary.Path, "cache"));
        var entries = catalog.GetExportEntries().Take(2).ToArray();
        foreach (var entry in entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cache.GetCachedPath(entry))!);
            File.WriteAllBytes(cache.GetCachedPath(entry), bytes);
        }
        var destination = Path.Combine(temporary.Path, "export.zip");
        Assert(await DllExportWorkflow.ExportAsync(entries, cache, destination, default) == 2, "export count incorrect");
        var imported = 0;
        var results = await DllImportSources.ReadAsync([destination], async (name, stream, token) =>
        {
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, token);
            Assert(output.ToArray().SequenceEqual(bytes), "ZIP round trip changed DLL bytes");
            imported++;
            return "fixture read";
        }, default);
        Assert(imported == 2 && results.All(result => result.Success), "export was not readable by import");
        await DllExportWorkflow.ExportAsync([entries[0]], cache, destination, default);
        using (var single = ZipFile.OpenRead(destination))
            Assert(single.Entries.Single().FullName == DllTypes.Get(entries[0].Type).FileName,
                "single DLL export did not use the family filename at archive root");
        await DllExportWorkflow.ExportAsync([entries[0] with { IsImported = true }, entries[1]], cache, destination, default);
        using (var mixed = ZipFile.OpenRead(destination))
            Assert(mixed.Entries.Count == 2 && mixed.Entries.Count(entry => entry.FullName.StartsWith("Imported/")) == 1,
                "mixed export lost imported provenance or created duplicate paths");
        var original = File.ReadAllBytes(destination);
        File.WriteAllText(cache.GetCachedPath(entries[0]), "corrupt");
        var rejected = false;
        try { await DllExportWorkflow.ExportAsync(entries, cache, destination, default); }
        catch (IOException) { rejected = true; }
        Assert(rejected && File.ReadAllBytes(destination).SequenceEqual(original), "failed export replaced destination");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await DllExportWorkflow.ExportAsync(entries, cache, destination, cancelled.Token); }
        catch (OperationCanceledException) { }
        Assert(File.ReadAllBytes(destination).SequenceEqual(original), "cancelled export replaced destination");
        Assert(!Directory.EnumerateFiles(temporary.Path, "*.export-*").Any(), "export staging files leaked");
    }

    private static async Task TestCatalogRefreshAsync()
    {
        using var temporary = new TemporaryDirectory();
        var manifest = WriteManifest(temporary.Path, "refresh"u8.ToArray());
        var valid = File.ReadAllBytes(manifest);
        var destination = Path.Combine(temporary.Path, "saved.json");
        using var http = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert(request.RequestUri == DllCatalogRefresh.Source, "refresh used an unexpected catalog source");
            return new(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(valid) };
        }));
        var catalog = await DllCatalogRefresh.FetchAsync(http, destination, default);
        Assert(catalog.GetEntries().Count > 0 && File.ReadAllBytes(destination).SequenceEqual(valid), "valid catalog was not published");
        using var invalid = new HttpClient(new StubHttpMessageHandler(_ => new(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") }));
        var rejected = false;
        try { await DllCatalogRefresh.FetchAsync(invalid, destination, default); }
        catch (InvalidDataException) { rejected = true; }
        Assert(rejected && File.ReadAllBytes(destination).SequenceEqual(valid), "invalid refresh replaced valid catalog");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await DllCatalogRefresh.FetchAsync(http, destination, cancelled.Token); }
        catch (OperationCanceledException) { }
        Assert(File.ReadAllBytes(destination).SequenceEqual(valid), "cancelled refresh replaced catalog");
        Assert(!Directory.EnumerateFiles(temporary.Path, "*.refresh-*").Any(), "refresh leaked staging files");
    }

    private static async Task TestSavedCatalogAsync()
    {
        using var temporary = new TemporaryDirectory();
        var game = Directory.CreateDirectory(Path.Combine(temporary.Path, "game")).FullName;
        var state = Directory.CreateDirectory(Path.Combine(temporary.Path, "state")).FullName;
        var valid = WriteManifest(temporary.Path, "catalog selection"u8.ToArray());
        var saved = Path.Combine(state, "manifest.json");
        File.WriteAllText(saved, "{}");
        Task<int> Run(string[] arguments) => DlssSwapper.Linux.Cli.Program.RunAsync(arguments, false,
            () => throw new Exception("Scan must not construct a cache"), () => new LibraryStateStore(state));
        Assert(await Run(["scan", "--path", game]) == 1, "CLI silently ignored an invalid saved catalog");
        Assert(await Run(["scan", "--path", game, "--manifest", valid]) == 0, "explicit manifest did not override saved catalog");
        File.Copy(valid, saved, overwrite: true);
        Assert(await Run(["scan", "--path", game]) == 0, "CLI could not use refreshed saved catalog");
        Assert(!File.Exists(new LibraryStateStore(state).StatePath), "catalog read wrote library defaults");
    }

    private static void TestNgxDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        var models = Directory.CreateDirectory(Path.Combine(temporary.Path, "models")).FullName;
        var config = Path.Combine(temporary.Path, "ngx.json");
        File.WriteAllText(config, JsonSerializer.Serialize(new { ngx_models_path = models }));
        File.WriteAllBytes(Path.Combine(models, "unrelated.BIN"), StreamlineSafetyTests.DllBytes("unrelated"));
        File.WriteAllText(Path.Combine(models, "bad.bin"), "not PE");
        var before = File.ReadAllText(config);
        var result = NgxModelDiscovery.Discover([Path.Combine(temporary.Path, "missing.json"), config], default);
        Assert(result.ModelsPath == models && result.Models.Count == 0 && result.Warnings.Count == 1,
            "NGX discovery accepted an unrelated model or lost an unreadable payload warning");
        Assert(File.ReadAllText(config) == before && Directory.GetFiles(models).Length == 2,
            "read-only model discovery changed files");
        var invalid = Path.Combine(temporary.Path, "invalid.json");
        File.WriteAllText(invalid, "{\"ngx_models_path\":\"relative\"}");
        Assert(NgxModelDiscovery.Discover([invalid, config], default).ModelsPath is null,
            "invalid higher-priority configuration silently fell through");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var stopped = false;
        try { NgxModelDiscovery.Discover([config], cancelled.Token); }
        catch (OperationCanceledException) { stopped = true; }
        Assert(stopped, "model discovery ignored cancellation");
        var language = new Translations("en-US");
        var text = (Dictionary<string, string>)language.Values;
        text["Linux_NgxConfigReadFailed"] = "CONFIG {0}: {1}";
        text["Linux_NgxConfigModelsPath"] = "ABSOLUTE PATH REQUIRED";
        text["Linux_NgxCacheMissing"] = "CACHE {0}";
        text["Linux_NgxInspectFailed"] = "INSPECT {0}: {1}";
        var localized = NgxModelDiscovery.Discover([invalid, config], default, language);
        Assert(localized.ModelsPath is null && localized.Warnings.Single() == $"CONFIG {invalid}: ABSOLUTE PATH REQUIRED",
            "localized discovery lost priority, configuration path or failure detail");
        var missingCache = Path.Combine(temporary.Path, "absent-models");
        Assert(NgxModelDiscovery.Scan(missingCache, default, language).Warnings.Single() == $"CACHE {missingCache}",
            "localized cache warning lost its path");
        Assert(NgxModelDiscovery.Discover([config], default, language).Warnings.Single().StartsWith($"INSPECT {Path.Combine(models, "bad.bin")}: "),
            "localized discovery did not pass language into cache scanning");
    }

    private static void TestProviderMerge()
    {
        using var temporary = new TemporaryDirectory();
        var steamPath = Directory.CreateDirectory(Path.Combine(temporary.Path, "steam")).FullName;
        var manualPath = Directory.CreateDirectory(Path.Combine(temporary.Path, "manual")).FullName;
        var epicPath = Directory.CreateDirectory(Path.Combine(temporary.Path, "epic")).FullName;
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(temporary.Path, "state")));
        library.AddManualGames([manualPath]);
        var steam = new SteamDiscoveryResult([new SteamGame("42", "Steam fixture", steamPath, temporary.Path, "fixture.acf")], []);
        var original = library.Merge(steam);
        var identity = new ProviderGameIdentity(GameProvider.Epic, "42");
        var providers = new[]
        {
            new ProviderGame(identity, "Epic fixture", epicPath, "fixture.item"),
            new ProviderGame(new(GameProvider.Gog, "manual"), "Overlapping manual", manualPath, "fixture"),
            new ProviderGame(new(GameProvider.Epic, "steam"), "Overlapping Steam", steamPath, "fixture"),
        };
        var merged = library.Merge(steam, providers);
        var cliLibrary = GameSelector.Resolve(CliParser.Parse(["scan"]), steam, merged);
        Assert(cliLibrary.SequenceEqual(merged), "Default CLI scan lost merged manual/provider records");
        Assert(GameSelector.Resolve(CliParser.Parse(["update", "--all", "--dry-run"]), steam, merged).SequenceEqual(merged),
            "CLI --all differs from the merged library");
        Assert(GameSelector.Resolve(CliParser.Parse(["scan", "--app-id", "42"]), steam, []).Single().SteamAppId == "42",
            "Explicit Steam selection was changed by library exclusions");
        Assert(GameSelector.Resolve(CliParser.Parse(["scan", "--path", epicPath]), steam, []).Single().RootPath == epicPath,
            "Explicit path selection was changed by library exclusions");
        Assert(merged.Count == 3 && original.All(game => merged.Contains(game)), "provider merge changed existing Steam/manual games");
        Assert(merged.Single(game => game.RootPath == epicPath).ProviderIdentity == identity,
            "provider identity was lost or reused as Steam app ID");
        var disconnectedPrefix = Path.Combine(temporary.Path, "disconnected-prefix");
        var disconnectedConfig = Path.Combine(temporary.Path, "disconnected-config");
        library.UpdateState(state =>
        {
            state.ExcludedProviderGames.Add(identity);
            state.ProviderWinePrefixes = [disconnectedPrefix];
            state.LegendaryConfigDirectories = [disconnectedConfig];
            state.HeroicExecutable = Path.Combine(temporary.Path, "Heroic.AppImage");
        });
        var reopened = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
        Assert(reopened.Merge(steam, providers).SequenceEqual(original), "provider exclusion collided with Steam ID or failed to persist");
        Assert(reopened.State.ProviderWinePrefixes.SequenceEqual([disconnectedPrefix])
            && reopened.State.LegendaryConfigDirectories.SequenceEqual([disconnectedConfig]), "disconnected provider folders were lost on reload");
        Assert(reopened.State.HeroicExecutable == Path.Combine(temporary.Path, "Heroic.AppImage"), "Disconnected Heroic executable setting was lost");
        reopened.UpdateState(state => state.ExcludedProviderGames = []);
        Assert(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).Merge(steam, providers).Count == 3,
            "provider exclusion restoration did not persist");
        var alias = new ProviderGameIdentity(GameProvider.Epic, "app:App42");
        var linked = new[] { providers[0], providers[0] with { IdentityAliases = [alias] },
            providers[0] with { Identity = alias, InstallDirectory = Path.Combine(temporary.Path, "other-copy") } };
        reopened.UpdateState(state => state.ExcludedProviderGames = [alias]);
        Assert(reopened.Merge(steam, linked).SequenceEqual(original)
            && reopened.Merge(steam, linked.Reverse().ToArray()).SequenceEqual(original),
            "An old app-ID exclusion allowed the catalog identity to reappear through another client");
        reopened.UpdateState(state => state.ExcludedProviderGames = [identity]);
        Assert(reopened.Merge(steam, linked).SequenceEqual(original), "Catalog exclusion failed to cover app-ID aliases");
        reopened.UpdateState(state => state.ExcludedProviderGames = []);
        var firstRoute = new ProviderLaunch(ProviderLauncher.Legendary, "App42", temporary.Path);
        var secondRoute = new ProviderLaunch(ProviderLauncher.Heroic, "App42", Path.Combine(temporary.Path, "heroic"));
        var overlapping = new[] { providers[0], providers[0] with { Launch = firstRoute, IdentityAliases = [alias] },
            providers[0] with { Launch = secondRoute }, providers[0] with { Launch = firstRoute } };
        var combined = reopened.Merge(steam, overlapping).Single(game => game.RootPath == epicPath);
        Assert(combined.ProviderLaunchChoices.Count == 2 && combined.ProviderLaunchChoices.Contains(firstRoute)
            && combined.ProviderLaunchChoices.Contains(secondRoute) && combined.ProviderIdentityAliases.Contains(alias),
            "Overlapping provider sources lost launch choices/aliases or duplicated the same route");
        Assert(reopened.Merge(steam, overlapping.Reverse().ToArray()).Single(game => game.RootPath == epicPath)
            .ProviderLaunchChoices.Count == 2, "Provider source ordering lost a launch route");
    }

    private static void TestEpicDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        var manifests = Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "ProgramData", "Epic", "EpicGamesLauncher", "Data", "Manifests")).FullName;
        var gamePath = Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Games", "Fixture Game")).FullName;
        var data = new { AppCategories = new[] { "games" }, CatalogItemId = "epic-fixture", DisplayName = "Fixture Game", InstallLocation = @"C:\games\fixture game" };
        File.WriteAllText(Path.Combine(manifests, "game.ITEM"), JsonSerializer.Serialize(data));
        File.WriteAllText(Path.Combine(manifests, "broken.item"), "{");
        File.WriteAllText(Path.Combine(manifests, "incomplete.item"), JsonSerializer.Serialize(new
        { data.AppCategories, CatalogItemId = "incomplete", data.DisplayName, data.InstallLocation, bIsIncompleteInstall = true }));
        var result = EpicDiscovery.Discover([temporary.Path], default);
        Assert(result.Games.Count == 1 && result.Warnings.Count == 1, "Epic discovery failed manifest isolation or incomplete filtering");
        Assert(string.Equals(result.Games[0].InstallDirectory, gamePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && result.Games[0].Identity == new ProviderGameIdentity(GameProvider.Epic, "epic-fixture"),
            "Epic discovery lost identity or mapped Windows path incorrectly");
        var rejected = false;
        try { EpicDiscovery.ResolveWinePath(temporary.Path, @"C:\..\outside"); } catch (IOException) { rejected = true; }
        Assert(rejected, "Epic path traversal was accepted");
        Assert(Directory.GetFiles(manifests).Length == 3, "Epic discovery changed manifest files");
        Assert(result.Sources.Single().State == DiscoverySourceState.Partial, "Invalid Epic manifest did not mark its source partial");
        File.WriteAllText(Path.Combine(manifests, "offline.item"), JsonSerializer.Serialize(new
        { data.AppCategories, CatalogItemId = "offline", DisplayName = "Offline fixture", InstallLocation = @"C:\Games\Missing" }));
        var offline = EpicDiscovery.Discover([temporary.Path], default);
        Assert(offline.Games.Any(item => item.Identity.Id == "offline") && offline.Warnings.Any(item => item.Contains("unavailable")),
            "Epic dropped an installed but unavailable game");
        Assert(EpicDiscovery.Discover([Path.Combine(temporary.Path, "absent-prefix")], default).Sources.Single().State == DiscoverySourceState.Unavailable,
            "Missing Epic source was complete");
    }

    private static void TestProviderWineUris()
    {
        using var temporary = new TemporaryDirectory();
        var system = Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "windows", "system32")).FullName;
        var start = Path.Combine(system, "start.exe"); File.WriteAllText(start, "fixture; never executed");
        var runner = Path.Combine(temporary.Path, "wine"); File.WriteAllText(runner, "fixture; never executed");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(runner, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var ea = ProviderLaunch.ForEa(temporary.Path, "OFB-EAST:123&other=1");
        var request = ea.CreateStartInfo(wineRunner: runner);
        Assert(request.ArgumentList.SequenceEqual([start, "origin2://game/launch?offerIds=OFB-EAST%3A123%26other%3D1"])
            && request.Environment["WINEPREFIX"] == temporary.Path && !request.UseShellExecute,
            "EA URI did not preserve offer ID boundaries or prefix dispatch");
        var windowsPath = @"C:\Games\Fixture & café";
        var epic = ProviderLaunch.ForEpic(temporary.Path, "epic-id", windowsPath);
        request = epic.CreateStartInfo(wineRunner: runner);
        Assert(request.ArgumentList.SequenceEqual([start, "com.epicgames.launcher://apps/" + Uri.EscapeDataString(windowsPath)
            + "?action=launch&silent=true"]) && request.FileName == runner, "Epic URI lost its Windows path or selected runner");
        try { ProviderLaunch.ForEpic(temporary.Path, "id", "/host/path"); throw new Exception("Host path accepted in Epic URI"); }
        catch (IOException) { }
        File.Delete(start);
        try { ea.CreateStartInfo(wineRunner: runner); throw new Exception("Missing URI dispatcher accepted"); } catch (IOException) { }
    }

    private static void TestProviderWineLaunch()
    {
        using var temporary = new TemporaryDirectory();
        var client = Path.Combine(temporary.Path, "Battle.net.exe");
        var runner = Path.Combine(temporary.Path, "wine runner");
        File.WriteAllText(client, "not executed"); File.WriteAllText(runner, "not executed");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(runner, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var launch = new ProviderLaunch(ProviderLauncher.Wine, "fenris", temporary.Path)
        { WindowsExecutable = client, WindowsArgument = "--exec=launch Fen" };
        var request = launch.CreateStartInfo(wineRunner: runner);
        Assert(request.FileName == runner && !request.UseShellExecute && request.Environment["WINEPREFIX"] == temporary.Path
            && request.ArgumentList.SequenceEqual([client, "--exec=launch Fen"]), "Wine provider request lost prefix, runner or literal argument");
        try { launch.CreateStartInfo(); throw new Exception("Missing Wine runner accepted"); } catch (IOException) { }
        try { (launch with { ConfigurationDirectory = Path.Combine(temporary.Path, "missing") }).CreateStartInfo(wineRunner: runner);
            throw new Exception("Missing prefix accepted"); } catch (InvalidOperationException) { }
        try { (launch with { WindowsExecutable = Path.Combine(temporary.Path, "missing.exe") }).CreateStartInfo(wineRunner: runner);
            throw new Exception("Missing client accepted"); } catch (IOException) { }
        var statePath = Path.Combine(temporary.Path, "state");
        var library = new PersistentLibrary(new LibraryStateStore(statePath));
        library.UpdateState(state => state.ProviderWineRunners[temporary.Path] = runner);
        Assert(new LibraryStateStore(statePath).Load().ProviderWineRunners[temporary.Path] == runner, "Runner choice was not preserved");
        var games = library.Merge(new SteamDiscoveryResult([], []),
            [new ProviderGame(new(GameProvider.BattleNet, "fenris"), "Diablo IV", temporary.Path, "fixture") { Launch = launch }]);
        Assert(games.Single().ProviderLaunch == launch, "Library merge lost Wine launch request");
    }

    private static void TestBattleNetCatalog()
    {
        // Snapshot of every Windows definition before extraction, including intentionally hidden WoW entries.
        string[] expected =
        [
            "aqua|aqua|AQUA|Avowed",
            "aris|aris|ARIS|Doom: The Dark Ages",
            "ark|ark|ARK|The Outer Worlds 2",
            "auks|auks|AUKS|Call of Duty",
            "d1|drtl|D1|Diablo",
            "d3cn|d3cn|D3CN|暗黑破壞神III",
            "diablo3|d3|D3|Diablo III",
            "fenris|fenris|Fen|Diablo IV",
            "fore|fore|FORE|Call of Duty: Vanguard",
            "heroes|hero|Hero|Heroes of the Storm",
            "hs_beta|hsb|WTCG|Hearthstone",
            "lazarus|lazr|LAZR|Call of Duty: MW2 Campaign Remastered",
            "lbra|lbra|LBRA|Tony Hawk's Pro Skater 3+4",
            "nina|nina|NINA|Call of Duty: Modern Warfare II",
            "odin|odin|ODIN|Call of Duty: Modern Warfare",
            "osi|osi|OSI|Diablo II: Resurrected",
            "pinta|pinta|PNTA|Call of Duty: Modern Warfare III",
            "prometheus|pro|Pro|Overwatch",
            "rtro|rtro|RTRO|Blizzard Arcade Collection",
            "s1|s1|S1|StarCraft",
            "s2|s2|S2|StarCraft II",
            "scor|scor|SCOR|Sea of Thieves",
            "viper|viper|VIPR|Call of Duty: Black Ops 4",
            "w1r|w1r|W1R|Warcraft I: Remastered",
            "w1|war1|W1|Warcraft: Orcs & Humans",
            "w2r|w2r|W2R|Warcraft II Remastered",
            "w2|w2bn|W2|Warcraft II: Battle.net Edition",
            "w3|w3|W3|Warcraft III: Reforged",
            "wlby|wlby|WLBY|Crash Bandicoot 4: It's About Time",
            "wow_classic|wow_classic|Wow_wow_classic|World of Warcraft Classic",
            "wow|wow|WoW|World of Warcraft",
            "zeus|zeus|ZEUS|Call of Duty: Black Ops Cold War",
        ];
        var actual = DlssSwapper.Shared.BattleNetGameCatalog.Games.Values
            .Select(game => string.Join("|", game.Uid, game.ProductCode, game.LauncherId, game.Name))
            .Order(StringComparer.Ordinal).ToArray();
        Assert(actual.SequenceEqual(expected), "Shared Battle.net definitions differ from the Windows baseline");
        Assert(!DlssSwapper.Shared.BattleNetGameCatalog.Games.ContainsKey("FENRIS"), "UID matching changed casing behavior");
    }

    private static void TestBattleNetPrefixDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        var agent = Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "ProgramData", "Battle.net", "Agent")).FullName;
        Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Games", "Fixture"));
        var client = Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Client")).FullName;
        File.WriteAllText(Path.Combine(client, "Battle.net.exe"), "fixture; never executed");
        File.WriteAllText(Path.Combine(temporary.Path, "system.reg"), """
            WINE REGISTRY Version 2
            [Software\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Battle.net] 1
            "InstallLocation"="C:\\Client"
            """);
        var db = new DLSS_Swapper.Data.BattleNet.Proto.ProductDb();
        DLSS_Swapper.Data.BattleNet.Proto.ProductInstall Product(string id, bool installed = true) => new()
        {
            Uid = id, ProductCode = id,
            Settings = new() { InstallPath = @"C:\Games\Fixture" },
            CachedProductState = new() { BaseProductState = new() { Installed = installed, Playable = true } },
        };
        db.ProductInstalls.Add(new DLSS_Swapper.Data.BattleNet.Proto.ProductInstall { Uid = "broken" });
        db.ProductInstalls.Add(Product("fenris")); db.ProductInstalls.Add(Product("fenris"));
        db.ProductInstalls.Add(Product("new-game")); db.ProductInstalls.Add(Product("stale", false));
        db.ProductInstalls.Add(Product("WoW_classic")); db.ProductInstalls.Add(Product("agent"));
        var file = Path.Combine(agent, "product.db");
        var bytes = Google.Protobuf.MessageExtensions.ToByteArray(db); File.WriteAllBytes(file, bytes);
        var aggregate = Path.Combine(agent, "aggregate.json");
        File.WriteAllText(aggregate, """{"installed":[{"product_id":"new-game","name":"Actual game title","logo_art_uri":"https://example.invalid/cover.png"}]}""");
        var found = BattleNetPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Count == 2 && found.Warnings.Count == 1,
            "Battle.net malformed-state isolation, duplicate collapse, stale/WoW/agent filtering failed");
        var known = found.Games.Single(game => game.Identity.Id == "fenris");
        Assert(known.Launch is { Launcher: ProviderLauncher.Wine, WindowsArgument: "--exec=launch Fen" }, "Battle.net discovery omitted Wine launch");
        Assert(known.Name == "Diablo IV" && known.BattleNet is { LauncherId: "Fen", Playable: true }
            && known.BattleNet.ClientPath == Path.Combine(client, "Battle.net.exe"), "Battle.net launch metadata was lost or guessed");
        var unknown = found.Games.Single(game => game.Identity.Id == "new-game");
        Assert(unknown.Name == "Actual game title" && unknown.BattleNet?.LauncherId is null,
            "Battle.net aggregate title lost or unknown launcher ID fabricated");
        Assert(File.ReadAllBytes(file).SequenceEqual(bytes), "Battle.net discovery changed product.db");
        Assert(found.Sources.Single().State == DiscoverySourceState.Partial, "Incomplete Battle.net installation state was classified complete");
        var all = ProviderDiscovery.Discover(new LinuxLibraryState { ProviderWinePrefixes = [temporary.Path] }, default, false);
        Assert(all.Games.Count(game => game.Identity.Provider == GameProvider.BattleNet) == 2, "Shared discovery omitted Battle.net");
        Assert(all.Games.All(game => game.Source is { } source && all.Sources.Any(outcome => outcome.Kind == source.Kind && outcome.Path == source.Path)),
            "Aggregated provider games lost their discovery-source association");
        File.WriteAllText(aggregate, "invalid");
        found = BattleNetPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Count == 2 && found.Warnings.Count == 2
            && found.Games.Single(game => game.Identity.Id == "new-game").Name == "Fixture", "Bad aggregate hid installed games");
        File.WriteAllBytes(file, [255, 255, 255]);
        found = BattleNetPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Count == 0 && found.Warnings.Count == 1, "Malformed Battle.net database was not reported");
        Assert(found.Sources.Single().State == DiscoverySourceState.Unavailable, "Unreadable Battle.net database was classified complete");
        db.ProductInstalls.Clear();
        var missingGame = Product("fenris"); missingGame.Settings.InstallPath = @"C:\Games\Unavailable";
        db.ProductInstalls.Add(missingGame);
        File.WriteAllBytes(file, Google.Protobuf.MessageExtensions.ToByteArray(db));
        found = BattleNetPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Single().Identity.Id == "fenris" && found.Games.Single().InstallDirectory.EndsWith("Unavailable")
            && found.Sources.Single().State == DiscoverySourceState.Complete, "Unavailable Battle.net game lost registered identity or source completeness");
        db.ProductInstalls.Clear(); File.WriteAllBytes(file, Google.Protobuf.MessageExtensions.ToByteArray(db));
        Assert(BattleNetPrefixDiscovery.Discover([temporary.Path], default).Sources.Single().State == DiscoverySourceState.Complete,
            "Empty Battle.net database was not complete");
        Assert(BattleNetPrefixDiscovery.Discover([Path.Combine(temporary.Path, "missing-prefix")], default).Sources.Single().State == DiscoverySourceState.Unavailable,
            "Missing Battle.net database was classified complete");
    }

    private static void TestEaPrefixDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "EA Desktop"));
        foreach (var name in new[] { "Fixture", "Bad", "Other" })
            Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Games", name, "__Installer"));
        var metadata = Path.Combine(temporary.Path, "drive_c", "Games", "Fixture", "__Installer", "installerdata.xml");
        File.WriteAllText(metadata, "<game><contentID>OFB-EAST:123</contentID></game>");
        File.WriteAllText(Path.Combine(temporary.Path, "drive_c", "Games", "Bad", "__Installer", "installerdata.xml"), "<invalid");
        File.WriteAllText(Path.Combine(temporary.Path, "drive_c", "Games", "Other", "__Installer", "installerdata.xml"),
            "<game><contentID>not-an-ea-install</contentID></game>");
        var systemPath = Path.Combine(temporary.Path, "system.reg");
        File.WriteAllText(systemPath, """
            WINE REGISTRY Version 2
            [Software\\Wow6432Node\\Electronic Arts\\EA Desktop] 1
            "InstallLocation"="C:\\EA Desktop"
            [Software\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Bad] 1
            "DisplayName"="Broken metadata"
            "InstallLocation"="C:\\Games\\Bad"
            "UninstallString"="C:\\EAInstaller\\Cleanup.exe"
            [Software\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Fixture] 1
            "DisplayName"="EA fixture"
            "InstallLocation"="C:\\Games\\Fixture"
            "UninstallString"="C:\\EAInstaller\\Cleanup.exe"
            [Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Other] 1
            "DisplayName"="Not EA"
            "InstallLocation"="C:\\Games\\Other"
            "UninstallString"="C:\\Other\\Cleanup.exe"
            """);
        var userPath = Path.Combine(temporary.Path, "user.reg");
        File.WriteAllText(userPath, """
            WINE REGISTRY Version 2
            [Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Fixture] 1
            "DisplayName"="EA fixture"
            "InstallLocation"="C:\\Games\\Fixture"
            "UninstallString"="C:\\eainstaller\\cleanup.EXE"
            """);
        var systemBefore = File.ReadAllBytes(systemPath); var userBefore = File.ReadAllBytes(userPath);
        var metadataBefore = File.ReadAllBytes(metadata);
        var found = EaPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Sources.Single().State == DiscoverySourceState.Partial, "Malformed EA installer metadata was classified complete");
        Assert(found.Games.Count == 1 && found.Games[0].Identity == new ProviderGameIdentity(GameProvider.Ea, "OFB-EAST:123")
            && found.Games[0].Name == "EA fixture" && found.Warnings.Count == 1,
            "EA identity, uninstall filter, registry-view deduplication or malformed metadata isolation failed");
        Assert(File.ReadAllBytes(systemPath).SequenceEqual(systemBefore) && File.ReadAllBytes(userPath).SequenceEqual(userBefore)
            && File.ReadAllBytes(metadata).SequenceEqual(metadataBefore), "EA discovery modified source data");
        File.WriteAllText(systemPath, "WINE REGISTRY Version 2\n[Software\\\\Electronic Arts\\\\EA Desktop] 1\n\"InstallLocation\"=\"C:\\\\EA Desktop\"\n");
        Assert(EaPrefixDiscovery.Discover([temporary.Path], default).Games.Single().Name == "EA fixture",
            "EA current-user installation was omitted");
        Assert(EaPrefixDiscovery.Discover([temporary.Path], default).Sources.Single().State == DiscoverySourceState.Complete,
            "Valid EA metadata was not classified complete");
        var merged = ProviderDiscovery.Discover(new LinuxLibraryState { ProviderWinePrefixes = [temporary.Path] }, default, false);
        Assert(merged.Games.Count(game => game.Identity.Provider == GameProvider.Ea) == 1, "Shared discovery omitted EA");
        Assert(merged.Games.All(game => game.Source is { } source && merged.Sources.Any(outcome => outcome.Kind == source.Kind && outcome.Path == source.Path)),
            "EA aggregate lost discovery-source associations");
        File.WriteAllText(metadata, "<game />");
        found = EaPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Count == 0 && found.Warnings.Count == 1, "EA missing identity was accepted silently");
        File.WriteAllText(metadata, "<!DOCTYPE game [<!ENTITY name 'not allowed'>]><game><contentID>&name;</contentID></game>");
        found = EaPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Count == 0 && found.Warnings.Count == 1, "EA discovery accepted a document type declaration");
        File.WriteAllText(systemPath, "WINE REGISTRY Version 2\n");
        Assert(EaPrefixDiscovery.Discover([temporary.Path], default).Games.Count == 0, "EA missing launcher check was bypassed");
        Assert(EaPrefixDiscovery.Discover([temporary.Path], default).Sources.Single().State == DiscoverySourceState.Complete,
            "Valid empty EA source was not complete");
        Assert(EaPrefixDiscovery.Discover([Path.Combine(temporary.Path, "missing-prefix")], default).Sources.Single().State == DiscoverySourceState.Unavailable,
            "Missing EA registry was classified as an empty successful source");
    }

    private static void TestUbisoftPrefixDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Games", "Fixture"));
        var cacheDirectory = Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Launcher", "cache", "configuration")).FullName;
        var registry = Path.Combine(temporary.Path, "system.reg");
        File.WriteAllText(registry, """
            WINE REGISTRY Version 2
            [Software\\Wow6432Node\\Ubisoft\\Launcher] 1234
            "InstallDir"="C:\\Launcher"
            [Software\\Wow6432Node\\Ubisoft\\Launcher\\Installs\\42] 1234
            "InstallDir"="C:\\Games\\Fixture"
            [Software\\Wow6432Node\\Ubisoft\\Launcher\\Installs\\43] 1234
            "InstallDir"="C:\\Games\\Fixture"
            [Software\\Wow6432Node\\Ubisoft\\Launcher\\Installs\\44] 1234
            "InstallDir"="C:\\Games\\Fixture"
            """);
        static byte[] Varint(int number)
        {
            var bytes = new List<byte>();
            do { bytes.Add((byte)((number & 127) | (number > 127 ? 128 : 0))); number >>= 7; } while (number > 0);
            return bytes.ToArray();
        }
        static byte[] Record(byte id, string yaml)
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(yaml);
            byte[] body = [8, id, 16, id, 26, ..Varint(payload.Length), ..payload];
            return [10, ..Varint(body.Length), ..body];
        }
        var cache = Path.Combine(cacheDirectory, "configurations");
        byte[] data = [..Record(44, "root: [invalid"), ..Record(43, "root:\n  installer:\n    game_identifier: DLC\n"),
            ..Record(42, "root:\n  logo_image: null\n  thumb_image: cover.PNG\n  installer:\n    game_identifier: 'Ubisoft fixture title'\n  start_game: {}\n")];
        File.WriteAllBytes(cache, data);
        var before = File.ReadAllBytes(registry);
        var found = UbisoftPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Count == 1 && found.Games[0].Identity == new ProviderGameIdentity(GameProvider.Ubisoft, "42")
            && found.Games[0].Name == "Ubisoft fixture title" && found.Warnings.Count == 1,
            "Ubisoft registry/YAML identity, DLC exclusion or malformed-record recovery failed");
        Assert(File.ReadAllBytes(registry).SequenceEqual(before) && File.ReadAllBytes(cache).SequenceEqual(data),
            "Ubisoft discovery modified source data");
        Assert(found.Games[0].CoverUrl == "https://ubistatic3-a.akamaihd.net/orbit/uplay_launcher_3_0/assets/cover.PNG",
            "A null logo must not hide a valid Ubisoft thumbnail");
        var merged = ProviderDiscovery.Discover(new LinuxLibraryState { ProviderWinePrefixes = [temporary.Path] }, default, false);
        Assert(merged.Games.Count(game => game.Identity.Provider == GameProvider.Ubisoft) == 1,
            "Shared discovery omitted Ubisoft games");
        var local = Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Local", "Ubisoft Game Launcher", "cache", "configuration")).FullName;
        File.WriteAllText(Path.Combine(temporary.Path, "user.reg"), """
            WINE REGISTRY Version 2
            [Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Folders] 1234
            "Local AppData"="C:\\Local"
            """);
        File.WriteAllBytes(Path.Combine(local, "configurations"), Record(42,
            "root:\n  installer:\n    game_identifier: 'Preferred local cache'\n  start_game: {}\n"));
        Assert(UbisoftPrefixDiscovery.Discover([temporary.Path], default).Games.Single().Name == "Preferred local cache",
            "Ubisoft discovery did not prefer Local AppData metadata");
        File.WriteAllText(registry, File.ReadAllText(registry).Replace(@"Games\\Fixture", @"Games\\Unavailable"));
        before = File.ReadAllBytes(registry);
        var unavailable = UbisoftPrefixDiscovery.Discover([temporary.Path], default);
        Assert(unavailable.Games.Single().Identity == new ProviderGameIdentity(GameProvider.Ubisoft, "42")
            && unavailable.Games.Single().InstallDirectory.EndsWith("Unavailable")
            && unavailable.Games.Single().Name == "Preferred local cache"
            && unavailable.Warnings.Any(warning => warning.Contains("is unavailable")),
            "Unavailable Ubisoft installation lost its registered metadata");
        Assert(File.ReadAllBytes(registry).SequenceEqual(before), "Unavailable Ubisoft discovery changed registry data");
        Assert(found.Sources.Single().State == DiscoverySourceState.Partial, "Damaged Ubisoft metadata was classified complete");
        Assert(unavailable.Sources.Single().State == DiscoverySourceState.Partial, "Missing configuration records were classified complete");
        File.WriteAllText(registry, "WINE REGISTRY Version 2\n");
        Assert(UbisoftPrefixDiscovery.Discover([temporary.Path], default).Sources.Single().State == DiscoverySourceState.Complete,
            "Empty valid Ubisoft registry was not complete");
        Assert(UbisoftPrefixDiscovery.Discover([Path.Combine(temporary.Path, "missing-prefix")], default).Sources.Single().State == DiscoverySourceState.Unavailable,
            "Missing Ubisoft registry was classified complete");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { UbisoftPrefixDiscovery.Discover([temporary.Path], cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
    }

    private static void TestUbisoftConfigurationReader()
    {
        byte[] valid = [10, 9, 8, 42, 16, 0, 26, 3, 65, 66, 67];
        var parsed = DlssSwapper.Shared.UbisoftConfigurationReader.Read(valid);
        Assert(parsed.Warnings.Count == 0 && parsed.Records.Single() is { InstallId: 42, LaunchId: 42, Offset: 8, Length: 3 },
            "Ubisoft framing lost identity, fallback launch ID or payload bounds");
        byte[] malformedThenValid = [10, 2, 26, 127, ..valid];
        parsed = DlssSwapper.Shared.UbisoftConfigurationReader.Read(malformedThenValid);
        Assert(parsed.Records.Count == 1 && parsed.Warnings.Count == 1, "Malformed bounded record hid a later valid record");
        for (var length = 1; length < valid.Length; length++)
            Assert(DlssSwapper.Shared.UbisoftConfigurationReader.Read(valid.AsSpan(0, length)).Records.Count == 0,
                "Truncated Ubisoft payload was accepted");
        parsed = DlssSwapper.Shared.UbisoftConfigurationReader.Read([10, 255, 255, 255, 255, 255]);
        Assert(parsed.Records.Count == 0 && parsed.Warnings.Count == 1, "Overflowing record length was accepted");
    }

    private static void TestGogPrefixDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "drive_c", "Games", "Fixture"));
        var registry = Path.Combine(temporary.Path, "system.reg");
        File.WriteAllText(registry, """
            WINE REGISTRY Version 2
            [Software\\GOG.com\\Games\\broken] 1234
            "gameID"="bad"
            "gameName"="unterminated
            [Software\\Wow6432Node\\GOG.com\\Games\\123] 1234
            "gameID"="123"
            "gameName"="Caf\xe9 \106ixture \"quoted\""
            "path"="C:\\Games\\Fixture"
            [Software\\GOG.com\\Games\\456] 1234
            "gameID"="456"
            "gameName"="DLC"
            "path"="C:\\Games\\Fixture"
            "dependsOn"="123"
            """);
        var before = File.ReadAllBytes(registry);
        var found = GogPrefixDiscovery.Discover([temporary.Path], default);
        Assert(found.Games.Count == 1 && found.Games[0].Identity == new ProviderGameIdentity(GameProvider.Gog, "123")
            && found.Games[0].Name == "Café Fixture \"quoted\"" && found.Warnings.Count == 1,
            "GOG registry Unicode/escape decoding, malformed-entry recovery or DLC handling failed");
        Assert(File.ReadAllBytes(registry).SequenceEqual(before), "GOG registry discovery modified source data");
        File.WriteAllText(registry, File.ReadAllText(registry).Replace(@"Games\\Fixture", @"Games\\Unavailable"));
        before = File.ReadAllBytes(registry);
        var unavailable = GogPrefixDiscovery.Discover([temporary.Path], default);
        Assert(unavailable.Games.Count == 1 && unavailable.Games[0].Identity == found.Games[0].Identity
            && unavailable.Games[0].InstallDirectory.EndsWith("Unavailable")
            && unavailable.Warnings.Any(warning => warning.Contains("is unavailable")),
            "Unavailable GOG installation lost its registered identity");
        Assert(File.ReadAllBytes(registry).SequenceEqual(before), "Unavailable discovery modified registry");
        Assert(found.Sources.Single().State == DiscoverySourceState.Partial, "Malformed registry was classified complete");
        File.WriteAllText(registry, "WINE REGISTRY Version 2\n");
        var empty = GogPrefixDiscovery.Discover([temporary.Path], default);
        Assert(empty.Games.Count == 0 && empty.Sources.Single().State == DiscoverySourceState.Complete,
            "Valid empty registry was not classified complete");
        var missing = GogPrefixDiscovery.Discover([Path.Combine(temporary.Path, "missing-prefix")], default);
        Assert(missing.Games.Count == 0 && missing.Sources.Single().State == DiscoverySourceState.Unavailable,
            "Missing registry was classified as an empty successful discovery");
    }

    private static void TestHeroicGogDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        var heroic = Directory.CreateDirectory(Path.Combine(temporary.Path, "heroic")).FullName;
        var game = Directory.CreateDirectory(Path.Combine(temporary.Path, "game")).FullName;
        var store = Directory.CreateDirectory(Path.Combine(heroic, "gog_store")).FullName;
        var cache = Directory.CreateDirectory(Path.Combine(heroic, "store_cache")).FullName;
        var installed = Path.Combine(store, "installed.json");
        File.WriteAllText(installed, JsonSerializer.Serialize(new { installed = new object[]
        {
            new { appName = "123", install_path = game },
            new { appName = "456", install_path = game, is_dlc = true },
            new { appName = "bad", install_path = "relative" },
        } }));
        var titlePath = Path.Combine(cache, "gog_library.json");
        File.WriteAllText(titlePath, "{\"games\":[{\"app_name\":\"123\",\"title\":\"GOG fixture\"}]}");
        var before = File.ReadAllBytes(installed);
        var result = HeroicGogDiscovery.Discover([heroic], default);
        Assert(result.Games.Count == 1 && result.Games[0].Identity == new ProviderGameIdentity(GameProvider.Gog, "123")
            && result.Games[0].Name == "GOG fixture" && result.Warnings.Count == 1, "GOG identity/title/DLC validation failed");
        var gogRequest = result.Games[0].Launch!.CreateStartInfo();
        Assert(gogRequest.FileName == "heroic" && gogRequest.ArgumentList.Single() == "heroic://launch?appName=123&runner=gog"
            && gogRequest.Environment["XDG_CONFIG_HOME"] == temporary.Path, "GOG launch used the wrong store or configuration");
        var flatpakRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic")).FullName;
        Directory.CreateDirectory(Path.Combine(flatpakRoot, "gog_store"));
        File.Copy(installed, Path.Combine(flatpakRoot, "gog_store", "installed.json"));
        var flatpakGog = HeroicGogDiscovery.Discover([flatpakRoot], default).Games.Single().Launch!.CreateStartInfo();
        Assert(flatpakGog.FileName == "flatpak" && flatpakGog.ArgumentList.Last().EndsWith("runner=gog"), "GOG Flatpak routing failed");
        File.WriteAllText(titlePath, "invalid optional metadata");
        result = HeroicGogDiscovery.Discover([heroic], default);
        Assert(result.Games.Count == 1 && result.Games[0].Name == "GOG game 123" && result.Warnings.Count == 2,
            "Invalid optional GOG titles removed an installed game");
        var shared = ProviderDiscovery.Discover(new LinuxLibraryState
            { LegendaryConfigDirectories = [Path.Combine(heroic, "legendaryConfig", "legendary")] }, default, false);
        Assert(shared.Games.Single().Identity.Provider == GameProvider.Gog, "Shared discovery requires Epic installations to discover GOG");
        var explicitRoot = ProviderDiscovery.Discover(new LinuxLibraryState { HeroicConfigDirectories = [heroic] }, default, false);
        Assert(explicitRoot.Games.Single().Identity.Provider == GameProvider.Gog, "Explicit Heroic root did not discover GOG");
        Assert(File.ReadAllBytes(installed).SequenceEqual(before), "GOG discovery changed installed metadata");
        Assert(result.Sources.Single().State == DiscoverySourceState.Partial, "Invalid GOG record did not mark its source partial");
        File.WriteAllText(installed, JsonSerializer.Serialize(new { installed = new[]
            { new { appName = "offline", install_path = Path.Combine(temporary.Path, "missing-game") } } }));
        var offline = HeroicGogDiscovery.Discover([heroic], default);
        Assert(offline.Games.Single().Identity.Id == "offline" && offline.Sources.Single().State == DiscoverySourceState.Complete,
            "GOG unavailable installation or optional title failure incorrectly removed source membership");
        File.WriteAllText(installed, "{");
        Assert(HeroicGogDiscovery.Discover([heroic], default).Sources.Single().State == DiscoverySourceState.Unavailable,
            "Unreadable GOG installed metadata was complete");
    }

    private static void TestLegendaryDiscovery()
    {
        using var temporary = new TemporaryDirectory();
        var game = Directory.CreateDirectory(Path.Combine(temporary.Path, "game")).FullName;
        var path = Path.Combine(temporary.Path, "installed.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["valid"] = new { app_name = "App42", title = "Legendary fixture", install_path = game },
            ["dlc"] = new { app_name = "DLC", title = "DLC", install_path = game, is_dlc = true },
            ["preload"] = new { app_name = "Preload", title = "Preload", install_path = game, is_preloaded = true },
            ["invalid"] = new { title = "Missing identity" },
        }));
        var before = File.ReadAllBytes(path);
        var result = LegendaryDiscovery.Discover([temporary.Path], default);
        var sharedDiscovery = ProviderDiscovery.Discover(new LinuxLibraryState { LegendaryConfigDirectories = [temporary.Path] }, default, false);
        Assert(sharedDiscovery.Games.Count == 1 && sharedDiscovery.Games[0].Identity.Id == "app:App42",
            "Shared CLI/GUI provider discovery lost configured sources");
        Assert(result.Games.Count == 1 && result.Warnings.Count == 1 && result.Games[0].Identity.Id == "app:App42",
            "Legendary discovery lost identity or accepted DLC/preload/invalid data");
        Assert(File.ReadAllBytes(path).SequenceEqual(before), "Legendary discovery modified launcher metadata");
        Assert(result.Sources.Single().State == DiscoverySourceState.Partial
            && sharedDiscovery.Sources.Any(source => source.Path == path && source.State == DiscoverySourceState.Partial),
            "Malformed record did not mark its source partial or aggregate lost its outcome");
        var disconnectedConfig = Directory.CreateDirectory(Path.Combine(temporary.Path, "disconnected-config")).FullName;
        var disconnectedRoot = Path.Combine(temporary.Path, "missing-game-directory");
        File.WriteAllText(Path.Combine(disconnectedConfig, "installed.json"), JsonSerializer.Serialize(new
        {
            installed = new { app_name = "Offline42", title = "Unavailable fixture", install_path = disconnectedRoot }
        }));
        var disconnected = LegendaryDiscovery.Discover([disconnectedConfig], default);
        Assert(disconnected.Games.Single().Identity.Id == "app:Offline42"
            && disconnected.Games.Single().InstallDirectory == disconnectedRoot
            && disconnected.Games.Single().Launch?.AppName == "Offline42"
            && disconnected.Warnings.Any(warning => warning.Contains("unavailable")),
            "Unavailable installed game lost its identity instead of retaining a warning");
        Assert(disconnected.Sources.Single().State == DiscoverySourceState.Complete,
            "Unavailable game directory incorrectly made readable metadata incomplete");
        File.WriteAllText(Path.Combine(disconnectedConfig, "installed.json"), "{}");
        Assert(LegendaryDiscovery.Discover([disconnectedConfig], default).Sources.Single().State == DiscoverySourceState.Complete,
            "Successfully read empty metadata was not complete");
        File.WriteAllText(Path.Combine(disconnectedConfig, "installed.json"), "{");
        Assert(LegendaryDiscovery.Discover([disconnectedConfig], default).Sources.Single().State == DiscoverySourceState.Unavailable,
            "Unreadable metadata was treated as complete");
        Assert(LegendaryDiscovery.Discover([Path.Combine(temporary.Path, "absent-config")], default).Sources.Single().State == DiscoverySourceState.Unavailable,
            "Missing metadata was treated as an empty complete source");
        var metadata = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var metadataPath = Path.Combine(metadata, "App42.json");
        File.WriteAllText(metadataPath, "{\"app_name\":\"App42\",\"metadata\":{\"id\":\"catalog42\"}}");
        var matched = LegendaryDiscovery.Discover([temporary.Path], default).Games.Single();
        Assert(matched.Identity.Id == "catalog42" && matched.IdentityAliases.Single().Id == "app:App42"
            && matched.Launch?.AppName == "App42", "Catalog matching replaced the launch ID or lost the old exclusion alias");
        File.WriteAllText(metadataPath, "{\"app_name\":\"OtherApp\",\"metadata\":{\"id\":\"wrong\"}}");
        var mismatched = LegendaryDiscovery.Discover([temporary.Path], default);
        Assert(mismatched.Games.Single().Identity.Id == "app:App42" && mismatched.Warnings.Count == 2,
            "Mismatched optional metadata removed the game or adopted an unrelated identity");
        var launch = result.Games[0].Launch!;
        var request = launch.CreateStartInfo();
        Assert(request.FileName == "legendary" && !request.UseShellExecute
            && request.ArgumentList.SequenceEqual(new[] { "launch", "App42" })
            && request.Environment["LEGENDARY_CONFIG_PATH"] == temporary.Path,
            "Legendary request lost the source configuration or launch identity");
        var heroicDirectory = Directory.CreateDirectory(Path.Combine(temporary.Path, "heroic", "legendaryConfig", "legendary")).FullName;
        File.Copy(path, Path.Combine(heroicDirectory, "installed.json"));
        var heroic = LegendaryDiscovery.Discover([heroicDirectory], default).Games.Single().Launch!;
        Assert(heroic.Launcher == ProviderLauncher.Heroic && heroic.CreateStartInfo().FileName == "heroic"
            && heroic.CreateStartInfo().ArgumentList.Single() == "heroic://launch?appName=App42&runner=legendary", "Heroic discovery used the wrong runner");
        var escaped = (heroic with { AppName = "Name & other=value" }).CreateStartInfo();
        Assert(escaped.Environment["XDG_CONFIG_HOME"] == temporary.Path, "Custom Heroic configuration was not passed to the launcher");
        var executable = Path.Combine(temporary.Path, "Heroic fixture.AppImage");
        File.WriteAllText(executable, "fixture only; never executed");
        Assert(heroic.CreateStartInfo(executable).FileName == executable, "Explicit Heroic executable was ignored");
        var unavailableRejected = false;
        try { heroic.CreateStartInfo(Path.Combine(temporary.Path, "missing.AppImage")); }
        catch (InvalidOperationException) { unavailableRejected = true; }
        Assert(unavailableRejected, "Unavailable Heroic executable silently fell back to a different launcher");
        Assert(!escaped.UseShellExecute && escaped.ArgumentList.Single().Contains("appName=Name%20%26%20other%3Dvalue&runner=legendary"),
            "Heroic launch identity escaped into URI parameters");
        var flatpakDirectory = Directory.CreateDirectory(Path.Combine(temporary.Path, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic", "legendaryConfig", "legendary")).FullName;
        File.Copy(path, Path.Combine(flatpakDirectory, "installed.json"));
        var flatpakRequest = LegendaryDiscovery.Discover([flatpakDirectory], default).Games.Single().Launch!.CreateStartInfo();
        Assert(flatpakRequest.FileName == "flatpak" && flatpakRequest.ArgumentList.SequenceEqual(new[]
            { "run", "com.heroicgameslauncher.hgl", "heroic://launch?appName=App42&runner=legendary" }), "Flatpak Heroic routed to a different installation");
        foreach (var invalid in new[] { launch with { AppName = "--offline" }, launch with { ConfigurationDirectory = "relative" } })
        {
            var rejected = false;
            try { invalid.CreateStartInfo(); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "Invalid provider launch request was accepted");
        }
    }

    private static void TestCatalogPolicy()
    {
        using var temporary = new TemporaryDirectory();
        var path = WriteManifest(temporary.Path, "policy"u8.ToArray());
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var family = DllTypes.All[0];
        var seed = document[family.ManifestKey]![0]!;
        var entries = new System.Text.Json.Nodes.JsonArray();
        for (var flags = 0; flags < 4; flags++)
        {
            var entry = seed.DeepClone(); entry["is_dev_file"] = (flags & 1) != 0;
            entry["is_signature_valid"] = (flags & 2) == 0; entry["version_number"] = flags + 1;
            entries.Add(entry);
        }
        document[family.ManifestKey] = entries; File.WriteAllText(path, document.ToJsonString());
        var catalog = DllCatalog.Load(path);
        Assert(catalog.GetEntries(family.Type).Count == 1, "default policy allowed debug/untrusted releases");
        foreach (var debug in new[] { false, true }) foreach (var untrusted in new[] { false, true })
        {
            catalog.Policy = new(debug, untrusted);
            var allowed = catalog.GetEntries(family.Type);
            Assert(allowed.Count == (debug ? 2 : 1) * (untrusted ? 2 : 1), "filter combination wrong");
            Assert(allowed.All(entry => (debug || !entry.IsDevFile) && (untrusted || entry.IsSignatureValid)), "disallowed entry survived");
            Assert(catalog.GetLatest(family.Type) == allowed[0], "latest selection ignored policy");
        }
        var store = new LibraryStateStore(Path.Combine(temporary.Path, "state"));
        var library = new PersistentLibrary(store);
        library.UpdateState(state => { state.AllowDebugDlls = true; state.AllowUntrustedDlls = true; });
        var reopened = new PersistentLibrary(new LibraryStateStore(store.StateDirectory));
        Assert(reopened.State.AllowDebugDlls && reopened.State.AllowUntrustedDlls, "selection preferences were not saved");
    }

    private static void TestDllRecordDetails()
    {
        using var temporary = new TemporaryDirectory();
        var path = WriteManifest(temporary.Path, "metadata fixture"u8.ToArray());
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var item = document[DllTypes.All[0].ManifestKey]![0]!;
        item["additional_label"] = "Unicode — 測試";
        item["internal_name"] = "Internal build";
        item["internal_name_extra"] = "Extra details";
        item["file_description"] = "Description with spaces";
        File.WriteAllText(path, document.ToJsonString());
        var entry = DllCatalog.Load(path).GetEntries(DllTypes.All[0].Type).Single();
        var catalog = DllCatalog.Load(path);
        var scan = new ScanResult(new SelectedGame("Fixture", temporary.Path, null),
            [new DetectedDll(entry.Type, Path.Combine(temporary.Path, "fixture.dll"), "fixture.dll", entry.Md5, entry.Version)], []);
        var choices = GameDllChoices.Eligible(scan, entry.Type, catalog);
        Assert(GameDllChoices.Current(scan, entry.Type, choices) == entry, "current hash was not preselected");
        var mixed = scan with { Dlls = scan.Dlls.Concat([scan.Dlls[0] with { Md5 = new string('A', 32) }]).ToArray() };
        Assert(GameDllChoices.Current(mixed, entry.Type, choices) is null, "mixed installed builds were represented as one current build");
        var details = DllRecordDetails.Describe(entry);
        foreach (var expected in new[] { "Unicode — 測試", "Internal build", "Extra details", "Description with spaces", entry.Md5, entry.ZipMd5, $"DLL size: {entry.FileSize} bytes", "Download size: 1 bytes" })
            Assert(details.Contains(expected), "DLL information lost " + expected);
        var minimal = DllRecordDetails.Describe(entry with { AdditionalLabel = null, InternalName = "", InternalNameExtra = " ", FileDescription = null });
        Assert(!minimal.Contains("Label:") && !minimal.Contains("Internal name:") && !minimal.Contains("Description:"), "empty optional fields were exposed");
        var translations = new Translations("en-US");
        var values = (Dictionary<string, string>)translations.Values;
        values["LibraryPage_DllRecordInfo_FileSize"] = "Fixture DLL size";
        values["Linux_DllDetailsInvalid"] = "Fixture invalid";
        values["Linux_DllDetailsValid"] = "Fixture valid";
        values["Linux_DllDetailsLine"] = "{1} | {0}";
        values["Linux_DllDetailsHashNotice"] = "Fixture checksum is not signature proof";
        var localized = DllRecordDetails.Describe(entry with { IsSignatureValid = false }, translations);
        foreach (var expected in new[] { "Unicode — 測試", entry.Md5, entry.ZipMd5,
            $"{entry.FileSize} bytes | Fixture DLL size", "Fixture invalid |", "Fixture checksum is not signature proof" })
            Assert(localized.Contains(expected), "translated DLL details lost " + expected);
        Assert(DllRecordDetails.Describe(entry with { IsSignatureValid = true }, translations).Contains("Fixture valid |"),
            "translated catalog signature status changed");
    }

    private static void TestDllLibraryCache()
    {
        using var temporary = new TemporaryDirectory();
        var payload = System.Text.Encoding.UTF8.GetBytes("catalog-cache-fixture");
        var catalog = DllCatalog.Load(WriteManifest(temporary.Path, payload));
        AssertEqual(DllTypes.All.Count, catalog.GetEntries().Count, "eligible catalog entry count");
        var entry = catalog.GetLatest(DllType.Dlss);
        AssertEqual(
            entry.Md5,
            catalog.Resolve(DllType.Dlss, $"{entry.Version}@{entry.Md5[..8]}").Md5,
            "exact version and build selection");

        var previousCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        try
        {
            Environment.SetEnvironmentVariable(
                "XDG_CACHE_HOME",
                Path.Combine(temporary.Path, "cache"));
            using var cache = new DownloadCache();
            var cachePath = cache.GetCachedPath(entry);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllBytes(cachePath, payload);
            Assert(cache.IsCached(entry), "downloaded catalog entry was not visible");
            Assert(cache.Remove(entry), "cached catalog entry was not removed");
            Assert(!cache.IsCached(entry), "removed catalog entry remained visible");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", previousCacheHome);
        }
    }

    private static void TestFilesystemInspection()
    {
        const string mountInfo =
            "36 25 0:31 / / rw,relatime - ext4 /dev/root rw\n"
            + "37 36 0:32 / /mnt/games rw,relatime - btrfs /dev/sdb rw\n"
            + "38 36 0:33 / /mnt/windows rw,relatime - fuseblk /dev/sdc rw\n"
            + "39 36 0:34 / /mnt/Steam\\040Library rw,relatime - btrfs /dev/sdd rw\n";
        var results = FilesystemInspector.InspectPaths(
            [
                "/mnt/games/SteamLibrary/steamapps/common/Game",
                "/mnt/windows/Game",
                "/mnt/Steam Library/steamapps/common/Another Game",
            ],
            mountInfo);

        AssertEqual("btrfs", results[0].Type, "longest mount match");
        Assert(results[0].Warning is null, "Btrfs should not warn");
        AssertEqual("fuseblk", results[1].Type, "FUSE type");
        Assert(results[1].Warning is not null, "FUSE should warn");
        AssertEqual("/mnt/Steam Library", results[2].MountPoint, "mount escape decoding");
    }

    private static async Task TestCliPersistentStateAsync()
    {
        using var temporary = new TemporaryDirectory();
        var game = Directory.CreateDirectory(Path.Combine(temporary.Path, "Fixture Game"));
        var stateDirectory = Path.Combine(
            temporary.Path,
            "config",
            "dlss-swapper-lle");
        Func<LibraryStateStore> createStore = () => new LibraryStateStore(stateDirectory);

        var addGameExit = await DlssSwapper.Linux.Cli.Program.RunAsync(
            ["state", "add-game", game.FullName],
            includeDefaultSteamRoots: false,
            static () => throw new InvalidOperationException("State commands must not create a download cache."),
            createStore).ConfigureAwait(false);
        var addPatternExit = await DlssSwapper.Linux.Cli.Program.RunAsync(
            ["state", "add-pattern", "Engine/Plugins/*/Binaries/Win64"],
            includeDefaultSteamRoots: false,
            static () => throw new InvalidOperationException("State commands must not create a download cache."),
            createStore).ConfigureAwait(false);
        var coveredPatternExit = await DlssSwapper.Linux.Cli.Program.RunAsync(
            ["state", "add-pattern", "Engine/Binaries/Win64"],
            includeDefaultSteamRoots: false,
            static () => throw new InvalidOperationException("State commands must not create a download cache."),
            createStore).ConfigureAwait(false);

        var state = createStore().Load();
        AssertEqual(0, addGameExit, "state add-game exit");
        AssertEqual(0, addPatternExit, "state add-pattern exit");
        AssertEqual(0, coveredPatternExit, "built-in-covered state pattern exit");
        AssertEqual(1, state.ManualGames.Count, "persisted manual game");
        AssertEqual(1, state.CustomScanPatterns.Count, "persisted custom pattern");
        async Task<int> RunStateFixture(params string[] operands) => await DlssSwapper.Linux.Cli.Program.RunAsync(
            new[] { "state" }.Concat(operands).ToArray(), false,
            static () => throw new InvalidOperationException("State commands must not acquire files."), createStore);
        var disconnected = Path.Combine(temporary.Path, "disconnected");
        foreach (var command in new[] { "add-provider-prefix", "add-legendary-config", "set-heroic-executable" })
            AssertEqual(0, await RunStateFixture(command, disconnected), command);
        state = createStore().Load();
        Assert(state.ProviderWinePrefixes.SequenceEqual([disconnected]) && state.LegendaryConfigDirectories.SequenceEqual([disconnected])
            && state.HeroicExecutable == disconnected, "CLI provider settings did not survive reload");
        AssertEqual(2, await RunStateFixture("add-provider-prefix", "relative"), "relative provider path rejected");
        foreach (var command in new[] { "remove-provider-prefix", "remove-legendary-config" })
            AssertEqual(0, await RunStateFixture(command, disconnected), command);
        AssertEqual(0, await RunStateFixture("clear-heroic-executable"), "clear Heroic setting");
        var seeded = new PersistentLibrary(createStore());
        seeded.UpdateState(saved => { saved.ExcludedSteamAppIds = ["42"]; saved.ExcludedProviderGames = [new(GameProvider.Epic, "42")]; });
        AssertEqual(0, await RunStateFixture("restore-providers"), "restore provider exclusions");
        state = createStore().Load();
        Assert(state.ExcludedSteamAppIds.SequenceEqual(["42"]) && state.ExcludedProviderGames.Count == 0
            && state.ProviderWinePrefixes.Count == 0 && state.LegendaryConfigDirectories.Count == 0
            && state.HeroicExecutable is null, "Provider state operations altered Steam exclusions or failed to clear settings");

        var rejected = false;
        try
        {
            CliParser.Parse(["reset"]);
        }
        catch (UsageException)
        {
            rejected = true;
        }

        Assert(rejected, "reset must require --yes");
    }

    private static void TestResetCoordination()
    {
        using var temporary = new TemporaryDirectory();
        var statePath = Path.Combine(temporary.Path, "config", "dlss-swapper-lle");
        var cachePath = Directory.CreateDirectory(Path.Combine(temporary.Path, "cache", "dlss-swapper-lle")).FullName;
        var store = new LibraryStateStore(statePath);
        var state = store.Load();
        store.Save(state);
        var before = File.ReadAllText(store.StatePath);
        var cacheFile = Path.Combine(cachePath, "keep-until-reset");
        File.WriteAllText(cacheFile, "fixture");
        using (var heldLock = new FileStream(Path.Combine(statePath, ".state.write.lock"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var rejected = false;
            try { new LocalDataResetService(store, cachePath).Reset(); }
            catch (IOException) { rejected = true; }
            Assert(rejected, "reset should reject an active writer");
            AssertEqual(before, File.ReadAllText(store.StatePath), "blocked reset changed state");
            Assert(File.Exists(cacheFile), "blocked reset deleted cache");
        }
        var unexpectedCache = Directory.CreateDirectory(Path.Combine(temporary.Path, "not-app-data")).FullName;
        var invalidRejected = false;
        try { new LocalDataResetService(store, unexpectedCache).Reset(); }
        catch (InvalidOperationException) { invalidRejected = true; }
        Assert(invalidRejected && File.Exists(store.StatePath), "target validation happened after state deletion");
        new LocalDataResetService(store, cachePath).Reset();
        var staleRejected = false;
        try { store.Save(state); } catch (IOException) { staleRejected = true; }
        Assert(staleRejected && !File.Exists(store.StatePath), "stale writer resurrected reset state");
    }

    private static void TestLocalDataReset()
    {
        using var temporary = new TemporaryDirectory();
        var stateDirectory = Directory.CreateDirectory(Path.Combine(
            temporary.Path,
            "config",
            "dlss-swapper-lle")).FullName;
        var cacheDirectory = Directory.CreateDirectory(Path.Combine(
            temporary.Path,
            "cache",
            "dlss-swapper-lle")).FullName;
        var preserved = Directory.CreateDirectory(Path.Combine(
            temporary.Path,
            "SteamLibrary",
            "DLSS Swapper Artwork")).FullName;
        File.WriteAllText(Path.Combine(stateDirectory, "state.json"), "{}");
        File.WriteAllText(Path.Combine(cacheDirectory, "cached.dll"), "fixture");
        File.WriteAllText(Path.Combine(preserved, "cover.jpg"), "fixture");

        new LocalDataResetService(
            new LibraryStateStore(stateDirectory),
            cacheDirectory).Reset();

        Assert(!Directory.Exists(stateDirectory), "state directory should be removed");
        Assert(!Directory.Exists(cacheDirectory), "application cache should be removed");
        Assert(File.Exists(Path.Combine(preserved, "cover.jpg")),
            "SteamLibrary-adjacent artwork must be preserved");
    }

    private static DllCatalogEntry Candidate(
        DllType type,
        string version,
        string content) =>
        Candidate(type, version, System.Text.Encoding.UTF8.GetBytes(content));

    private static DllCatalogEntry Candidate(
        DllType type,
        string version,
        byte[] content)
    {
        var hash = Convert.ToHexString(MD5.HashData(content));
        return new DllCatalogEntry(
            type,
            version,
            1,
            hash,
            new string('0', 32),
            new Uri("https://example.invalid/candidate.zip"),
            content.Length,
            1,
            true,
            false);
    }

    private static string HashOf(string content) =>
        Convert.ToHexString(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(content)));

    private static string EscapeVdf(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage ImageResponse(byte[] bytes, string mediaType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
    }

    private static StringComparer PathComparersForTests { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static T AssertSingle<T>(IReadOnlyList<T> values)
    {
        AssertEqual(1, values.Count, "single value");
        return values[0];
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string context)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{context}: expected '{expected}', got '{actual}'.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = Directory.CreateDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"dlss-swapper-linux-tests-{Guid.NewGuid():N}")).FullName;
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        internal StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class RecordingArtworkProcessor : IArtworkImageProcessor
    {
        internal int SaveCount { get; private set; }

        internal int MaximumWidth { get; private set; }

        internal int MaximumHeight { get; private set; }

        internal byte[] Source { get; private set; } = [];

        public async Task SavePortraitAsync(
            ReadOnlyMemory<byte> source,
            string destinationPath,
            int maximumWidth,
            int maximumHeight,
            CancellationToken cancellationToken)
        {
            SaveCount++;
            MaximumWidth = maximumWidth;
            MaximumHeight = maximumHeight;
            Source = source.ToArray();
            await File.WriteAllBytesAsync(destinationPath, Source, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
