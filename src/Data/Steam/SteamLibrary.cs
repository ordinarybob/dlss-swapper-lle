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
        var knownAppManifestPaths = new Dictionary<string, string>();

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
                            // If the appManifestPath does not exist it is likely the game was freshly uninstalled.
                            var appManifestPath = Path.Combine(path, $"appmanifest_{steamApp.Key}.acf");
                            if (File.Exists(appManifestPath))
                            {
                                if (knownAppManifestPaths.ContainsKey(steamApp.Key) == false)
                                {
                                    knownAppManifestPaths[steamApp.Key] = appManifestPath;
                                }
                                else
                                {
                                    Logger.Error($"Went to add {steamApp.Key} to knownAppManifestPaths, but this key already exists.");
                                }
                            }
                            else
                            {
                                Logger.Error($"Expected manifest path was not found - {appManifestPath}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Unable to process {libraryFoldersFile}");
            Debugger.Break();
        }

        // Look for newly installed games that have not reached libraryfolders.vdf yet.
        // Older unindexed manifests are likely stale leftovers and must not be loaded.
        foreach (var steamAppPath in steamAppsPaths)
        {
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
                if (knownAppManifestPaths.ContainsKey(appId))
                {
                    continue;
                }

                var appManifestFileInfo = new FileInfo(appManifestPath);
                if (standaloneSteamAppsPaths.Contains(steamAppPath)
                    || libraryFoldersFileInfo is null
                    || appManifestFileInfo.LastWriteTime > libraryFoldersFileInfo.LastWriteTime)
                {
                    knownAppManifestPaths[appId] = appManifestPath;
                }
                else
                {
                    Logger.Error(
                        $"Found potential rogue file when loading Steam manifests: appId {appId}, {appManifestPath}");
                }
            }
        }

        var games = new List<Game>();

        foreach (var appManifestPath in knownAppManifestPaths.Values)
        {
            SteamGame? game;

            try
            {
                using (var fileStream = File.OpenRead(appManifestPath))
                {
                    var appManifestACF = kvSerializer.Deserialize<AppManifestACF>(fileStream);

                    if (appManifestACF is null || string.IsNullOrEmpty(appManifestACF.AppId))
                    {
                        Logger.Error($"Unable to parse app manifest - {appManifestPath}");
                        continue;
                    }

                    game = new SteamGame(appManifestACF.AppId);

                    if (Enum.TryParse(appManifestACF.StateFlags, out SteamStateFlag stateFlags) == false)
                    {
                        // The AppState couldn't be parsed from the appmanifest_*.acf
                        Logger.Error($"Unable to parse StateFlags {appManifestACF.StateFlags} for app {appManifestACF.AppId} in {appManifestPath}");
                        continue;
                    }
                    game.StateFlags = stateFlags;
                    game.Title = appManifestACF.Name;

                    var baseDir = Path.GetDirectoryName(appManifestPath);
                    if (string.IsNullOrEmpty(baseDir))
                    {
                        continue;
                    }

                    var installDir = PathHelpers.NormalizePath(Path.Combine(baseDir, "common", appManifestACF.InstallDir));
                    if (Directory.Exists(installDir) == false)
                    {
                        // If the install directory does not exist, skip this game.
                        Logger.Error($"SteamLibary could not load game {game.Title} ({game.PlatformId}) because install path does not exist: {installDir}");
                        continue;
                    }
                    game.InstallPath = installDir;
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
                continue;
            }

            var cachedGame = GameManager.Instance.GetGame<SteamGame>(game.PlatformId);
            var activeGame = cachedGame ?? game;

            if (activeGame.IsHidden is null && _defaultHiddenGames.Contains(activeGame.PlatformId))
            {
                activeGame.IsHidden = true;
            }


            activeGame.Title = game.Title;  // TODO: Will this be a problem if the game is already loaded
            activeGame.InstallPath = game.InstallPath;
            activeGame.StateFlags = game.StateFlags;

            if (activeGame.IsInIgnoredPath())
            {
                continue;
            }

            await activeGame.SaveToDatabaseAsync().ConfigureAwait(false);

            // If the game is not from cache, force processing
            if (cachedGame is null)
            {
                activeGame.NeedsProcessing = true;
            }

            if (activeGame.NeedsProcessing == true || forceNeedsProcessing == true)
            {
                activeGame.ProcessGame(forceNeedsProcessing: forceNeedsProcessing);
            }

            games.Add(activeGame);
        }


        if (libraryFoldersFileInfo is null)
        {
            // Standalone discovery is additive because it cannot prove that an
            // undiscovered library is uninstalled or currently available.
            foreach (var cachedGame in cachedGames)
            {
                if (games.Contains(cachedGame) == false)
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
                if (games.Contains(cachedGame) == false)
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
            Debugger.Break();
        }
    }
}
