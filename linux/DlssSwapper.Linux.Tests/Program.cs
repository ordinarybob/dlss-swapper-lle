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
}
