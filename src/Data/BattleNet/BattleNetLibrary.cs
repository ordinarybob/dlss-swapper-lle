using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLSS_Swapper.Data.BattleNet.Proto;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using Microsoft.Win32;

namespace DLSS_Swapper.Data.BattleNet;

internal partial class BattleNetLibrary : IGameLibrary
{
    public GameLibrary GameLibrary => GameLibrary.BattleNet;
    public string Name => "Battle.net";

    public Type GameType => typeof(BattleNetGame);

    static BattleNetLibrary? instance;
    public static BattleNetLibrary Instance => instance ??= new BattleNetLibrary();

    GameLibrarySettings? _gameLibrarySettings;
    public GameLibrarySettings? GameLibrarySettings => _gameLibrarySettings ??= GameManager.Instance.GetGameLibrarySettings(GameLibrary);

    readonly string _productDbPath;

    public string ClientPath { get; private set; } = string.Empty;
    string _installPath = string.Empty;

    private static readonly System.Collections.Frozen.FrozenDictionary<string, DlssSwapper.Shared.BattleNetGameDefinition> _knownGames = DlssSwapper.Shared.BattleNetGameCatalog.Games;

    // Ignore the Battle.net agent installation and all World of Warcraft installations.
    // WoW DLL swaps lead to disconnects without exception, and it only supports XeLL anyway.
    [GeneratedRegex(@"^(agent|agent_beta|beta|battle\.net|wow.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex IgnoredGameIdRegex();


    private BattleNetLibrary()
    {
        var allUsersProfile = Environment.GetEnvironmentVariable("ALLUSERSPROFILE");
        allUsersProfile ??= Environment.ExpandEnvironmentVariables("%ProgramData%");
        _productDbPath = Path.Combine(allUsersProfile, "Battle.net", "Agent", "product.db");
    }

    public bool IsInstalled()
    {
        var agentPath = Directory.GetParent(_productDbPath)?.FullName;
        return Directory.Exists(agentPath) && File.Exists(_productDbPath);
    }

    public async Task<List<Game>> ListGamesAsync(bool forceNeedsProcessing)
    {
        if (IsInstalled() == false)
        {
            return [];
        }

        var games = new List<Game>();
        var tempFile = Path.GetTempFileName();

        var cachedGames = GameManager.Instance.GetGames<BattleNetGame>();

        try
        {
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (var bnet = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net"))
                {
                    if (bnet is not null)
                    {
                        var installPath = bnet.GetValue("InstallLocation")?.ToString();
                        if (string.IsNullOrWhiteSpace(installPath) == false)
                        {
                            var clientPath = Path.Combine(installPath, "Battle.net.exe");
                            if (File.Exists(clientPath))
                            {
                                _installPath = installPath;
                                ClientPath = clientPath;
                            }
                            else
                            {
                                Logger.Error($"Battle.net.exe not found at {clientPath}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not get BattleNet client path.");
        }

        var installedAggregates = new Dictionary<string, AggregateItem>();

        if (string.IsNullOrWhiteSpace(ClientPath) == false)
        {
            try
            {
                var aggregateJsonPath = Path.Combine(Directory.GetParent(_productDbPath)?.FullName ?? string.Empty, "aggregate.json");
                if (File.Exists(aggregateJsonPath) == true)
                {
                    using (var fileStream = File.OpenRead(aggregateJsonPath))
                    {
                        var aggregate = await JsonSerializer.DeserializeAsync(
                            fileStream,
                            SourceGenerationContext.Default.Aggregate).ConfigureAwait(false);
                        if (aggregate is not null)
                        {
                            foreach (var aggregateItem in aggregate.Installed)
                            {
                                installedAggregates.Add(aggregateItem.ProductId, aggregateItem);
                            }
                        }
                        else
                        {
                            Logger.Error($"Could not deserialize aggregate {aggregateJsonPath}");
                        }
                    }
                }
                else
                {
                    Logger.Error($"Could not find {aggregateJsonPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not load installed aggregates.");
            }
        }

        try
        {

            File.Copy(_productDbPath, tempFile, true);
            ProductDb? productDb;
            await using (var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose))
            {
                productDb = ProductDb.Parser.ParseFrom(fileStream);
            }

            if (productDb is null)
            {
                Logger.Error("Could not load product.db in Battle.net.");
                return [];
            }

            foreach (var product in productDb.ProductInstalls)
            {
                if (IgnoredGameIdRegex().IsMatch(product.Uid))
                {
                    continue;
                }

                var installationState = product.CachedProductState?.BaseProductState;
                if (!DiscoveryMetadata.HasBattleNetInstallationState(installationState?.Installed, product.Settings?.InstallPath))
                {
                    Logger.Warning($"Skipping Battle.net entry {product.Uid}: installation state or settings are missing.");
                    continue;
                }
                // Uninstalled games sometimes remain in the product.db
                if (installationState!.Installed == false)
                {
                    continue;
                }

                var gameId = product.Uid;
                var gamePath = product.Settings!.InstallPath;

                if (string.IsNullOrWhiteSpace(gameId))
                {
                    Logger.Error("Issue loading Battle.net Game, no gameId found.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(gamePath))
                {
                    Logger.Error($"Issue loading Battle.net Game {gameId}, no gamePath found.");
                    continue;
                }

                if (Directory.Exists(gamePath) == false)
                {
                    Logger.Error($"Issue loading Battle.net Game {gameId}, installation directory {gamePath} does not exist.");
                    continue;
                }

                var cachedGame = GameManager.Instance.GetGame<BattleNetGame>(gameId);
                var activeGame = cachedGame ?? new BattleNetGame(gameId);

                // Cached games may already be bound to the visible game list.
                await App.CurrentApp.RunOnUIThreadAsync(() =>
                {
                    if (_knownGames.TryGetValue(product.Uid, out var battleNetLauncherGame))
                    {
                        activeGame.Title = battleNetLauncherGame.Name;
                        activeGame.LauncherId = battleNetLauncherGame.LauncherId;
                    }
                    else
                    {
                        Logger.Error($"Battle.Net game title not found for UID ({product.Uid}) in install path ({product.Settings.InstallPath}).");

                        activeGame.Title = string.Empty;
                        activeGame.LauncherId = string.Empty;
                    }

                    if (installedAggregates.TryGetValue(product.ProductCode, out var aggregate))
                    {
                        // If title isn't set, try use it from the aggregates.
                        if (string.IsNullOrWhiteSpace(activeGame.Title))
                        {
                            activeGame.Title = aggregate.Name;
                        }

                        // Set the cover photo.
                        activeGame.RemoteCoverImage = aggregate.LogoArtUri;
                    }
                    else
                    {
                        Logger.Error($"Battle.Net game aggregate not found for ProductCode ({product.ProductCode}).");
                    }

                    activeGame.Title = DiscoveryMetadata.BattleNetTitle(activeGame.Title, null, gamePath);
                    activeGame.InstallPath = PathHelpers.NormalizePath(gamePath);
                    activeGame.StatePlayable = installationState.Playable;
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                if (activeGame.IsInIgnoredPath())
                {
                    continue;
                }

                if (Directory.Exists(activeGame.InstallPath) == false)
                {
                    Logger.Warning($"{Name} library could not load game {activeGame.Title} ({activeGame.PlatformId}) because install path does not exist: {activeGame.InstallPath}");
                    continue;
                }

                await activeGame.SaveToDatabaseAsync().ConfigureAwait(false);

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
        }
        catch (FileNotFoundException err)
        {
            Logger.Error($"Battle.net product.db not found: {err.Message}");
            return [];
        }
        catch (IOException err)
        {
            Logger.Error($"I/O Error while reading Battle.net product.db: {err.Message}");
            return [];
        }

        games.Sort();

        // Delete games that are no longer loaded, they are likely uninstalled
        foreach (var cachedGame in cachedGames)
        {
            // Game is to be deleted.
            if (games.Contains(cachedGame) == false)
            {
                await cachedGame.DeleteAsync().ConfigureAwait(false);
            }
        }

        return games;
    }

    public async Task LoadGamesFromCacheAsync()
    {
        try
        {
            BattleNetGame[] games;
            using (await Database.Instance.Mutex.LockAsync())
            {
                games = await Database.Instance.Connection.Table<BattleNetGame>().ToArrayAsync().ConfigureAwait(false);
            }

            await GameManager.Instance.AddCachedGamesAsync(games).ConfigureAwait(false);
        }
        catch (Exception err)
        {
            Logger.Error(err);
            DebuggerHelper.BreakIfAttached();
        }
    }
}
