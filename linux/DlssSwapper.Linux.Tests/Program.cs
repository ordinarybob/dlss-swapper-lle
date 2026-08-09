using System.Security.Cryptography;
using System.Text.Json;
using DlssSwapper.Linux.Cli;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Tests;

internal static class Program
{
    public static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("Steam VDF/ACF fixture discovery", RunSync(TestSteamDiscovery)),
            ("Immediate-root selection deduplicates", RunSync(TestImmediateRootSelection)),
            ("Scanner skips directory symlinks", RunSync(TestScannerSkipsSymlinks)),
            ("Detected-family and compatibility planning", RunSync(TestPlanning)),
            ("Dry-run planner does not mutate", RunSync(TestPlannerDoesNotMutate)),
            ("CLI dry-run never reaches cache or writes", TestCliDryRunBoundaryAsync),
            ("Mutation boundary rejects tampering", TestMutationBoundaryAsync),
            ("Adjacent backup, update, and restore", TestUpdateAndRestoreAsync),
            ("Persistent library state and exclusions", RunSync(TestPersistentLibraryState)),
            ("Fast scan learns deep-scan stragglers", RunSync(TestFastScanLearning)),
            ("Fast library scan records metadata without hashing", TestMetadataOnlyFastScanAsync),
            ("Matching version is a metadata-only no-op", RunSync(TestMetadataOnlyNoOp)),
            ("Initial deep scan completion is retry safe", TestRetrySafeDeepScanAsync),
            ("Library scan isolates missing game roots", TestMissingRootIsolationAsync),
            ("Artwork cache, CDN, and strict fallback", TestArtworkResolutionAsync),
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

    private static Func<Task> RunSync(Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

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
        AssertEqual(6, library.State.GridColumns, "default grid columns");
        AssertEqual(5, library.State.GridRows, "default grid rows");
        AssertEqual(15, library.State.Performance.ScanConcurrency, "standard scan concurrency");
        AssertEqual(38, library.State.Performance.ArtworkConcurrency, "standard art concurrency");
        AssertEqual(1, library.AddManualGames([manualRoot, manualRoot]), "manual path deduplication");
        AssertEqual(2, library.AddImmediateChildren(groupRoot), "immediate child import");

        library.State.HddMode = true;
        library.State.HasCompletedInitialDeepScan = true;
        library.State.AdditionalSteamRoots.Add(Path.Combine(temporary.Path, "Steam"));
        library.State.CustomScanPatterns.Add("*/custom/runtime");
        library.State.MediaWikiApiEndpoint = "https://example.invalid/w/api.php";
        library.State.MediaWikiImageHost = "images.example.invalid";
        library.Save();
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
        AssertEqual(3, reloaded.State.ManualGames.Count, "persisted manual game count");
        AssertEqual(1, reloaded.State.CustomScanPatterns.Count, "persisted custom pattern count");
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
        Assert(!stateJson.Contains(nested, StringComparison.Ordinal),
            "nested folder leaked into persisted imports");
        Assert(
            !Directory.EnumerateFiles(stateDirectory, "*.tmp").Any(),
            "atomic state save left a temporary file");
        Assert(Directory.Exists(childTwo), "fixture child unexpectedly missing");
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
        var result = await new LibraryScanService(catalog).ScanFastAsync(
            [
                new SelectedGame("Missing Game", missingRoot, null),
                new SelectedGame("Live Game", gameRoot, null),
            ],
            new LinuxLibraryState()).ConfigureAwait(false);

        AssertEqual(2, result.Games.Count, "isolated scan result count");
        var missing = result.Games.Single(scan => scan.Game.Name == "Missing Game");
        AssertEqual(0, missing.Dlls.Count, "missing root DLL count");
        AssertEqual(1, missing.Warnings.Count, "missing root warning count");
        var live = result.Games.Single(scan => scan.Game.Name == "Live Game");
        AssertEqual(1, live.Dlls.Count, "live root DLL count");
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
        var service = new ArtworkService(
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
        var directService = new ArtworkService(
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
        var fallbackService = new ArtworkService(
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
