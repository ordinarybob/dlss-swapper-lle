using DLSS_Swapper.Data.Steam.Manifest;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ValveKeyValue;

namespace DLSS_Swapper.Data.Steam;

internal partial class SteamLibrary : IGameLibrary
{
    static readonly Regex AppManifestFileNameRegex = new(
        @"^appmanifest_(?<app_id>\d+)\.acf$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public GameLibrary GameLibrary => GameLibrary.Steam;
    public string Name => "Steam";

    public Type GameType => typeof(SteamGame);

    static SteamLibrary? instance;
    public static SteamLibrary Instance => instance ??= new SteamLibrary();

    GameLibrarySettings? _gameLibrarySettings;
    public GameLibrarySettings? GameLibrarySettings => _gameLibrarySettings ??= GameManager.Instance.GetGameLibrarySettings(GameLibrary);

    static string _installPath = string.Empty;

    private SteamLibrary()
    {

    }

    internal static string? TryResolveAppIdFromInstallPath(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        try
        {
            var normalizedInstallPath = PathHelpers.NormalizePath(installPath);
            var gameDirectory = new DirectoryInfo(normalizedInstallPath);
            var commonDirectory = gameDirectory.Parent;
            var steamAppsDirectory = commonDirectory?.Parent;
            if (commonDirectory is null
                || steamAppsDirectory is null
                || commonDirectory.Name.Equals("common", StringComparison.OrdinalIgnoreCase) == false
                || steamAppsDirectory.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase) == false)
            {
                return null;
            }

            var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            foreach (var manifestPath in Directory.EnumerateFiles(
                steamAppsDirectory.FullName,
                "appmanifest_*.acf",
                SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var fileStream = File.OpenRead(manifestPath);
                    var manifest = serializer.Deserialize<AppManifestACF>(fileStream);
                    if (manifest is null
                        || string.IsNullOrWhiteSpace(manifest.AppId)
                        || string.IsNullOrWhiteSpace(manifest.InstallDir))
                    {
                        continue;
                    }

                    var manifestInstallPath = PathHelpers.NormalizePath(
                        Path.Combine(commonDirectory.FullName, manifest.InstallDir));
                    if (manifestInstallPath.Equals(
                        normalizedInstallPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return manifest.AppId;
                    }
                }
                catch (Exception err)
                {
                    Logger.Warning($"Unable to inspect Steam manifest {manifestPath} for manual artwork. {err.Message}");
                }
            }
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to match manual install path {installPath} to a Steam manifest. {err.Message}");
        }

        return null;
    }

    public bool IsInstalled()
    {
        return string.IsNullOrEmpty(GetInstallPath()) == false
            || FindStandaloneSteamAppsPaths().Count > 0;
    }

    readonly string[] _defaultHiddenGames = [
        "228980", // Steamworks Common Redistributables
];

    public async Task<List<Game>> ListGamesAsync(bool forceNeedsProcessing = false)
    {
        var cachedGames = GameManager.Instance.GetGames<SteamGame>();

        var installPath = GetInstallPath();


        // I hope this runs on a background thread. 
        // Tasks are whack.

        // Base steamapps folder contains libraryfolders.vdf which has references to other steamapps folders and individual installed Steam games.
        // All of these folders contain appmanifest_[some_id].acf which contains information about the game.

        var baseSteamAppsFolder = string.IsNullOrWhiteSpace(installPath)
            ? string.Empty
            : Path.Combine(installPath, "steamapps");
        var libraryFoldersFile = string.IsNullOrWhiteSpace(baseSteamAppsFolder)
            ? string.Empty
            : Path.Combine(baseSteamAppsFolder, "libraryfolders.vdf");
        FileInfo? libraryFoldersFileInfo = File.Exists(libraryFoldersFile)
            ? new FileInfo(libraryFoldersFile)
            : null;

        var steamAppsPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(baseSteamAppsFolder))
        {
            steamAppsPaths.Add(baseSteamAppsFolder);
        }

        var standaloneSteamAppsPaths = libraryFoldersFileInfo is null
            ? FindStandaloneSteamAppsPaths()
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        steamAppsPaths.UnionWith(standaloneSteamAppsPaths);
        var steamDiscoveryStartedAt = Stopwatch.StartNew();
        var indexedAppManifestPaths = new Dictionary<string, List<string>>();
        var knownAppManifestPaths = new Dictionary<string, string>();
        var discoveredAppManifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enumeratedAppManifests = new List<(string AppId, string Path, string SteamAppsPath)>();
        var knownInstallPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var kvSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

        try
        {
            if (libraryFoldersFileInfo is not null)
            {
                using var fileStream = File.OpenRead(libraryFoldersFile);
                var libraryFoldersVDF = kvSerializer.Deserialize<Dictionary<string, LibraryFoldersVDF>>(fileStream);
                foreach (var libraryFolderVDF in libraryFoldersVDF)
                {
                    var path = PathHelpers.NormalizePath(libraryFolderVDF.Value.Path);
                    path = Path.Combine(path, "steamapps");

                    if (string.IsNullOrWhiteSpace(path) == false && Directory.Exists(path))
                    {
                        steamAppsPaths.Add(path);

                        foreach (var steamApp in libraryFolderVDF.Value.Apps)
                        {
                            var appManifestPath = Path.Combine(path, $"appmanifest_{steamApp.Key}.acf");
                            if (indexedAppManifestPaths.TryGetValue(steamApp.Key, out var expectedPaths) == false)
                            {
                                expectedPaths = [];
                                indexedAppManifestPaths.Add(steamApp.Key, expectedPaths);
                            }
                            if (expectedPaths.Contains(appManifestPath, StringComparer.OrdinalIgnoreCase) == false)
                            {
                                expectedPaths.Add(appManifestPath);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Unable to process {libraryFoldersFile}");
            DebuggerHelper.BreakIfAttached();
        }

        // Look for newly installed games that have not reached libraryfolders.vdf yet.
        // Older unindexed manifests are likely stale leftovers and must not be loaded.
        foreach (var steamAppPath in steamAppsPaths)
        {
            var commonPath = Path.Combine(steamAppPath, "common");
            try
            {
                if (Directory.Exists(commonPath))
                {
                    foreach (var installedDirectory in Directory.EnumerateDirectories(
                        commonPath,
                        "*",
                        SearchOption.TopDirectoryOnly))
                    {
                        knownInstallPaths.Add(PathHelpers.NormalizePath(installedDirectory));
                    }
                }
            }
            catch (Exception err)
            {
                Logger.Error(err, $"Unable to enumerate Steam install directories in {commonPath}.");
            }

            foreach (var appManifestPath in Directory.EnumerateFiles(
                steamAppPath,
                "appmanifest_*.acf",
                SearchOption.TopDirectoryOnly))
            {
                var match = AppManifestFileNameRegex.Match(Path.GetFileName(appManifestPath));
                if (match.Success == false)
                {
                    continue;
                }

                var appId = match.Groups["app_id"].Value;
                discoveredAppManifestPaths.Add(appManifestPath);
                enumeratedAppManifests.Add((appId, appManifestPath, steamAppPath));
            }
        }

        foreach (var indexedAppManifest in indexedAppManifestPaths)
        {
            foreach (var indexedAppManifestPath in indexedAppManifest.Value)
            {
                if (discoveredAppManifestPaths.Contains(indexedAppManifestPath))
                {
                    if (knownAppManifestPaths.TryAdd(indexedAppManifest.Key, indexedAppManifestPath) == false)
                    {
                        Logger.Error(
                            $"Found app {indexedAppManifest.Key} in more than one indexed Steam library: " +
                            $"{knownAppManifestPaths[indexedAppManifest.Key]}, {indexedAppManifestPath}");
                    }
                }
                else
                {
                    Logger.Error($"Expected manifest path was not found - {indexedAppManifestPath}");
                }
            }
        }

        foreach (var enumeratedAppManifest in enumeratedAppManifests)
        {
            if (knownAppManifestPaths.ContainsKey(enumeratedAppManifest.AppId))
            {
                continue;
            }

            var appManifestFileInfo = new FileInfo(enumeratedAppManifest.Path);
            if (standaloneSteamAppsPaths.Contains(enumeratedAppManifest.SteamAppsPath)
                || libraryFoldersFileInfo is null
                || appManifestFileInfo.LastWriteTime > libraryFoldersFileInfo.LastWriteTime)
            {
                knownAppManifestPaths[enumeratedAppManifest.AppId] = enumeratedAppManifest.Path;
            }
            else
            {
                Logger.Error(
                    $"Found potential rogue file when loading Steam manifests: " +
                    $"appId {enumeratedAppManifest.AppId}, {enumeratedAppManifest.Path}");
            }
        }
        Logger.Info(
            $"Discovered {knownAppManifestPaths.Count:N0} Steam manifest(s) and " +
            $"{knownInstallPaths.Count:N0} install director(ies) in " +
            $"{steamDiscoveryStartedAt.Elapsed.TotalSeconds:N2} seconds.");

        var cachedGamesByPlatformId = cachedGames
            .GroupBy(static game => game.PlatformId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var appManifestPaths = knownAppManifestPaths.Values.ToArray();
        var parsedGames = new SteamGame?[appManifestPaths.Length];
        var manifestStartedAt = Stopwatch.StartNew();
        var activeManifestWorkers = 0;
        var peakManifestWorkers = 0;
        var manifestWorkerCount = Settings.Instance.RecursiveScanConcurrency;
        Parallel.ForEach(
            Enumerable.Range(0, appManifestPaths.Length),
            new ParallelOptions { MaxDegreeOfParallelism = manifestWorkerCount },
            manifestIndex =>
        {
            var appManifestPath = appManifestPaths[manifestIndex];

            try
            {
                var activeWorkers = Interlocked.Increment(ref activeManifestWorkers);
                var currentPeak = Volatile.Read(ref peakManifestWorkers);
                while (activeWorkers > currentPeak)
                {
                    var observedPeak = Interlocked.CompareExchange(
                        ref peakManifestWorkers,
                        activeWorkers,
                        currentPeak);
                    if (observedPeak == currentPeak)
                    {
                        break;
                    }

                    currentPeak = observedPeak;
                }

                using (var fileStream = File.OpenRead(appManifestPath))
                {
                    var manifestSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                    var appManifestACF = manifestSerializer.Deserialize<AppManifestACF>(fileStream);

                    if (appManifestACF is null || string.IsNullOrEmpty(appManifestACF.AppId))
                    {
                        Logger.Error($"Unable to parse app manifest - {appManifestPath}");
                        return;
                    }

                    var game = new SteamGame(appManifestACF.AppId);

                    if (Enum.TryParse(appManifestACF.StateFlags, out SteamStateFlag stateFlags) == false)
                    {
                        // The AppState couldn't be parsed from the appmanifest_*.acf
                        Logger.Error($"Unable to parse StateFlags {appManifestACF.StateFlags} for app {appManifestACF.AppId} in {appManifestPath}");
                        return;
                    }
                    game.StateFlags = stateFlags;
                    game.Title = appManifestACF.Name;

                    var baseDir = Path.GetDirectoryName(appManifestPath);
                    if (string.IsNullOrEmpty(baseDir))
                    {
                        return;
                    }

                    var installDir = PathHelpers.NormalizePath(Path.Combine(baseDir, "common", appManifestACF.InstallDir));
                    if (knownInstallPaths.Contains(installDir) == false
                        && Directory.Exists(installDir) == false)
                    {
                        // If the install directory does not exist, skip this game.
                        Logger.Error($"SteamLibary could not load game {game.Title} ({game.PlatformId}) because install path does not exist: {installDir}");
                        return;
                    }
                    game.InstallPath = installDir;
                    parsedGames[manifestIndex] = game;
                }
            }
            catch (Exception err)
            {
                Logger.Error(err, $"Unable to process Steam manifest {appManifestPath}.");
                return;
            }
            finally
            {
                Interlocked.Decrement(ref activeManifestWorkers);
            }
        });
        Logger.Info(
            $"Parsed and validated {parsedGames.Count(static game => game is not null):N0} of " +
            $"{appManifestPaths.Length:N0} Steam manifest(s) in " +
            $"{manifestStartedAt.Elapsed.TotalSeconds:N2} seconds; " +
            $"peak workers: {peakManifestWorkers}/{manifestWorkerCount}.");

        var registrationStartedAt = Stopwatch.StartNew();
        var games = new List<Game>(parsedGames.Length);
        foreach (var game in parsedGames)
        {
            if (game is null)
            {
                continue;
            }

            cachedGamesByPlatformId.TryGetValue(game.PlatformId, out var cachedGame);
            var activeGame = cachedGame ?? game;

            if (activeGame.IsHidden is null && _defaultHiddenGames.Contains(activeGame.PlatformId))
            {
                activeGame.IsHidden = true;
            }

            activeGame.Title = game.Title;
            activeGame.InstallPath = game.InstallPath;
            activeGame.StateFlags = game.StateFlags;

            if (activeGame.IsInIgnoredPath())
            {
                continue;
            }

            await activeGame.SaveToDatabaseAsync().ConfigureAwait(false);

            if (cachedGame is null)
            {
                activeGame.NeedsProcessing = true;
            }

            if (activeGame.NeedsProcessing == true || forceNeedsProcessing == true)
            {
                activeGame.ProcessGame(
                    forceNeedsProcessing: forceNeedsProcessing,
                    installPathValidated: true);
            }

            games.Add(activeGame);
        }
        Logger.Info(
            $"Registered {games.Count:N0} Steam game(s) in " +
            $"{registrationStartedAt.Elapsed.TotalSeconds:N2} seconds.");
        var discoveredPlatformIds = games
            .Select(static game => game.PlatformId)
            .ToHashSet(StringComparer.Ordinal);


        if (libraryFoldersFileInfo is null)
        {
            // Standalone discovery is additive because it cannot prove that an
            // undiscovered library is uninstalled or currently available.
            foreach (var cachedGame in cachedGames)
            {
                if (discoveredPlatformIds.Contains(cachedGame.PlatformId) == false)
                {
                    games.Add(cachedGame);
                }
            }
        }
        else
        {
            // The Steam client index is authoritative for installed libraries.
            foreach (var cachedGame in cachedGames)
            {
                if (discoveredPlatformIds.Contains(cachedGame.PlatformId) == false)
                {
                    await cachedGame.DeleteAsync().ConfigureAwait(false);
                }
            }
        }

        games.Sort();

        return games;
    }

    static HashSet<string> FindStandaloneSteamAppsPaths()
    {
        var steamAppsPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || drive.IsReady == false)
            {
                continue;
            }

            var steamAppsPath = Path.Combine(
                drive.RootDirectory.FullName,
                "SteamLibrary",
                "steamapps");
            if (Directory.Exists(steamAppsPath) == false)
            {
                continue;
            }

            try
            {
                if (Directory.EnumerateFiles(
                    steamAppsPath,
                    "appmanifest_*.acf",
                    SearchOption.TopDirectoryOnly).Any())
                {
                    steamAppsPaths.Add(steamAppsPath);
                }
            }
            catch (Exception err)
            {
                Logger.Error(err, $"Unable to inspect Steam library {steamAppsPath}.");
            }
        }

        return steamAppsPaths;
    }

    public static string GetInstallPath()
    {
        if (string.IsNullOrEmpty(_installPath) == false)
        {
            return _installPath;
        }

        try
        {
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (var steamRegistryKey = hklm.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    // if steamRegistryKey is null then Steam is not installed.
                    if (steamRegistryKey is null)
                    {
                        return string.Empty;
                    }

                    var installPath = steamRegistryKey.GetValue("InstallPath") as string ?? string.Empty;
                    if (string.IsNullOrEmpty(installPath) == false && Directory.Exists(installPath))
                    {
                        _installPath = installPath;
                    }

                    return _installPath;
                }
            }
        }
        catch (Exception err)
        {
            _installPath = string.Empty;
            Logger.Error(err);
            return string.Empty;
        }
    }

    public async Task LoadGamesFromCacheAsync()
    {
        try
        {
            SteamGame[] games;
            using (await Database.Instance.Mutex.LockAsync())
            {
                games = await Database.Instance.Connection.Table<SteamGame>().ToArrayAsync().ConfigureAwait(false);
            }
            foreach (var game in games)
            {
                if (game.IsInIgnoredPath())
                {
                    continue;
                }

                if (Directory.Exists(game.InstallPath) == false)
                {
                    Logger.Warning($"{Name} library could not load game {game.Title} ({game.PlatformId}) from cache because install path does not exist: {game.InstallPath}");
                    // We remove the list of known game assets, but not the game itself.
                    // Removing the game will remove its history, notes, and other data.
                    // We don't want to do this in case it is just a temporary issue.
                    await game.RemoveGameAssetsFromCacheAsync().ConfigureAwait(false);
                    continue;
                }

                await game.LoadGameAssetsFromCacheAsync().ConfigureAwait(false);
                GameManager.Instance.AddGame(game);
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
            DebuggerHelper.BreakIfAttached();
        }
    }
}
