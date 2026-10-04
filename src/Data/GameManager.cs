using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Collections;
using DLSS_Swapper.Data.BattleNet;
using DLSS_Swapper.Data.Xbox;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Windows.System;

namespace DLSS_Swapper.Data;

internal partial class GameManager : ObservableObject
{
    public static GameManager Instance { get; private set; } = new GameManager();

    // The locked list serves workers; the observable collection is updated only on the UI thread.
    List<Game> _synchronisedAllGames = new List<Game>();
    ObservableCollection<Game> _allGames { get; } = new ObservableCollection<Game>();

    public CollectionViewSource GroupedGameCollectionViewSource { get; init; }
    public CollectionViewSource UngroupedGameCollectionViewSource { get; init; }

    [ObservableProperty]
    public partial bool ShowHiddenGames { get; set; } = false;

    object gameLock = new object();

    GameGroup allGamesGroup;
    GameGroup favouriteGamesGroup;

    public AdvancedCollectionView AllGamesView { get; init; }
    public AdvancedCollectionView FavouriteGamesView { get; init; }

    Dictionary<GameLibrary, GameGroup> libraryGameGroups = new Dictionary<GameLibrary, GameGroup>();
    Dictionary<GameLibrary, AdvancedCollectionView> libraryGamesView = new Dictionary<GameLibrary, AdvancedCollectionView>();

    readonly SemaphoreSlim _loadGate = new(1, 1);
    readonly List<Action> _pendingUiChanges = new();
    bool _batchUiChanges;

    static bool IsVisibleForSwappableFilter(Game game, bool hideNonDLSSGames)
    {
        return hideNonDLSSGames == false
            || game.HasSwappableItems;
    }

    Predicate<object> GetPredicateForAllGames(bool hideNonDLSSGames, string? filterText = null)
    {
        return (obj) =>
        {
            var game = (Game)obj;

            if (ShowHiddenGames == false && game.IsHidden == true)
            {
                return false;
            }

            bool matchesText = string.IsNullOrEmpty(filterText) || game.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase);
            return IsVisibleForSwappableFilter(game, hideNonDLSSGames) && matchesText;
        };
    }

    Predicate<object> GetPredicateForFavouriteGames(bool hideNonDLSSGames, string? filterText = null)
    {
        return (obj) =>
        {
            var game = (Game)obj;

            if (ShowHiddenGames == false && game.IsHidden == true)
            {
                return false;
            }

            bool matchesText = string.IsNullOrEmpty(filterText) || game.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase);
            return game.IsFavourite && IsVisibleForSwappableFilter(game, hideNonDLSSGames) && matchesText;
        };
    }

    Predicate<object> GetPredicateForLibraryGames(GameLibrary library, bool hideNonDLSSGames, string? filterText = null)
    {
        return (obj) =>
        {
            var game = (Game)obj;

            if (ShowHiddenGames == false && game.IsHidden == true)
            {
                return false;
            }

            bool matchesText = string.IsNullOrEmpty(filterText) || game.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase);
            return game.GameLibrary == library && IsVisibleForSwappableFilter(game, hideNonDLSSGames) && matchesText;
        };
    }

    private GameManager()
    {
        FavouriteGamesView = new AdvancedCollectionView(_allGames, true);
        FavouriteGamesView.Filter = GetPredicateForFavouriteGames(Settings.Instance.HideNonDLSSGames);
        FavouriteGamesView.ObserveFilterProperty(nameof(ShowHiddenGames));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.IsFavourite));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.HasSwappableItems));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.IsEligibilityPending));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.IsHidden));

        AllGamesView = new AdvancedCollectionView(_allGames, true);
        AllGamesView.Filter = GetPredicateForAllGames(Settings.Instance.HideNonDLSSGames);
        AllGamesView.ObserveFilterProperty(nameof(ShowHiddenGames));
        AllGamesView.ObserveFilterProperty(nameof(Game.HasSwappableItems));
        AllGamesView.ObserveFilterProperty(nameof(Game.IsEligibilityPending));
        AllGamesView.ObserveFilterProperty(nameof(Game.IsHidden));

        allGamesGroup = new GameGroup(string.Empty, null, AllGamesView);
        favouriteGamesGroup = new GameGroup("Favourites", null, FavouriteGamesView);

        var groupedList = new ObservableCollection<GameGroup>()
        {
            favouriteGamesGroup,
        };

        var ungroupedList = new List<GameGroup>()
        {
            favouriteGamesGroup,
            allGamesGroup,
        };

        foreach (var gameLibraryEnum in GetGameLibraries(false))
        {
            var gameLibrary = IGameLibrary.GetGameLibrary(gameLibraryEnum);

            var gameView = new AdvancedCollectionView(_allGames, true);
            gameView.Filter = GetPredicateForLibraryGames(gameLibraryEnum, Settings.Instance.HideNonDLSSGames);
            gameView.ObserveFilterProperty(nameof(Game.HasSwappableItems));
            gameView.ObserveFilterProperty(nameof(Game.IsEligibilityPending));
            gameView.ObserveFilterProperty(nameof(ShowHiddenGames));
            gameView.ObserveFilterProperty(nameof(Game.IsHidden));

            libraryGamesView[gameLibraryEnum] = gameView;

            var gameGroup = new GameGroup(gameLibrary.Name, gameLibrary.GameLibrary, gameView);
            groupedList.Add(gameGroup);
            libraryGameGroups[gameLibraryEnum] = gameGroup;
        }

        ApplySort(Settings.Instance.GameSortMode);

        GroupedGameCollectionViewSource = new CollectionViewSource()
        {
            IsSourceGrouped = true,
            Source = groupedList,
            ItemsPath = new PropertyPath("Games"),
        };

        UngroupedGameCollectionViewSource = new CollectionViewSource()
        {
            IsSourceGrouped = true,
            Source = ungroupedList,
            ItemsPath = new PropertyPath("Games"),
        };

        WeakReferenceMessenger.Default.Register<GameLibrariesOrderChangedMessage>(this, (sender, message) =>
        {
            var groupedGameLibraryList = groupedList.ToList();

            groupedList.Clear();

            groupedList.Add(groupedGameLibraryList[0]);
            groupedGameLibraryList.RemoveAt(0);

            // Add each of the items in the order that is from settings.
            foreach (var gameLibrarySetting in Settings.Instance.GameLibrarySettings)
            {
                var groupedItem = groupedGameLibraryList.Single(x => x.GameLibrary == gameLibrarySetting.GameLibrary);
                groupedList.Add(groupedItem);
                groupedGameLibraryList.Remove(groupedItem);
            }

            if (groupedGameLibraryList.Count > 0)
            {
                Logger.Error($"Somehow extra grouped items were left over. {string.Join(", ", groupedGameLibraryList)}");
            }
        });

    }

    public void SetGridDensityHeaderWidth(double width)
    {
        if (double.IsFinite(width) == false || width <= 0)
        {
            return;
        }

        allGamesGroup.GridDensityHeaderWidth = width;
        favouriteGamesGroup.GridDensityHeaderWidth = width;
        foreach (var gameGroup in libraryGameGroups.Values)
        {
            gameGroup.GridDensityHeaderWidth = width;
        }
    }

    IEnumerable<AdvancedCollectionView> GetSortableViews()
    {
        yield return FavouriteGamesView;
        yield return AllGamesView;

        foreach (var gameView in libraryGamesView.Values)
        {
            yield return gameView;
        }
    }

    public void ApplySort(GameSortMode sortMode)
    {
        if (Enum.IsDefined(sortMode) == false)
        {
            sortMode = GameSortMode.NameAscending;
        }

        foreach (var gameView in GetSortableViews())
        {
            using (gameView.DeferRefresh())
            {
                gameView.SortDescriptions.Clear();

                if (sortMode == GameSortMode.NameAscending)
                {
                    gameView.SortDescriptions.Add(new SortDescription(nameof(Game.Title), SortDirection.Ascending));
                    continue;
                }

                gameView.SortDescriptions.Add(new SortDescription(nameof(Game.HasCurrentDLSSForSort), SortDirection.Descending));
                gameView.SortDescriptions.Add(new SortDescription(
                    nameof(Game.CurrentDLSSVersionSortKey),
                    sortMode == GameSortMode.DlssNewestFirst ? SortDirection.Descending : SortDirection.Ascending));
                gameView.SortDescriptions.Add(new SortDescription(nameof(Game.Title), SortDirection.Ascending));
            }
        }
    }

    public async Task LoadGamesFromCacheAsync()
    {
        var loadStopwatch = Stopwatch.StartNew();
        await _loadGate.WaitAsync().ConfigureAwait(false);
        BeginUiBatch();
        try
        {

            foreach (var gameLibraryEnum in GameManager.Instance.GetGameLibraries(true))
            {
                var gameLibrary = IGameLibrary.GetGameLibrary(gameLibraryEnum);
                if (gameLibrary.IsEnabled)
                {
                    await gameLibrary.LoadGamesFromCacheAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await EndUiBatchAsync().ConfigureAwait(false);
            _loadGate.Release();
            Logger.Info(
                $"Loaded {GetSynchronisedGamesListCopy().Count:N0} cached game(s) " +
                $"in {loadStopwatch.Elapsed.TotalSeconds:N2} seconds without filesystem validation.");
        }
    }

    internal async Task AddCachedGamesAsync<TGame>(IEnumerable<TGame> games)
        where TGame : Game
    {
        foreach (var game in games)
        {
            if (game.IsInIgnoredPath())
            {
                continue;
            }

            await game.LoadGameAssetsFromCacheAsync().ConfigureAwait(false);
            AddGame(game);
        }
    }

    public async Task LoadGamesAsync(
        bool forceNeedsProcessing = false,
        bool exhaustiveScan = false,
        Func<Task>? candidateLibraryReady = null)
    {
        var loadStopwatch = Stopwatch.StartNew();
        Logger.Info("Game library discovery started.");
        await _loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            BeginUiBatch();
            GameDatabaseWriteBatch.Instance.Begin();
            // Dispose the scan session before the finally block releases the
            // load gate. Batch creation failures must release that gate too.
            using var gameAssetPathIndex = GameAssetPathIndex.BeginBatch(exhaustiveScan);
            var tasks = new List<Task<List<Game>>>();
            foreach (var gameLibraryEnum in GameManager.Instance.GetGameLibraries(true))
            {
                var gameLibrary = IGameLibrary.GetGameLibrary(gameLibraryEnum);
                if (gameLibrary.IsEnabled)
                {
                    tasks.Add(Task.Run(() => gameLibrary.ListGamesAsync(forceNeedsProcessing)));
                }
            }

            // Add games to the game library when each library task completes.
            while (tasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(completedTask);

                foreach (var game in await completedTask.ConfigureAwait(false))
                {
                    AddGame(game);
                }
            }

            Logger.Info($"Game library discovery registered all scan work in {loadStopwatch.Elapsed.TotalSeconds:N2} seconds.");
            var assetScanTask = gameAssetPathIndex.CompleteAsync();
            await FlushPendingUiChangesAsync().ConfigureAwait(false);
            try
            {
                await gameAssetPathIndex.WhenCandidateLibraryReadyAsync().ConfigureAwait(false);
                await FlushPendingUiChangesAsync().ConfigureAwait(false);
                await App.CurrentApp.RunOnUIThreadAsync(async () =>
                {
                    if (candidateLibraryReady is not null)
                    {
                        await candidateLibraryReady().ConfigureAwait(true);
                    }
                    await Task.Yield();
                }).ConfigureAwait(false);
            }
            finally
            {
                gameAssetPathIndex.ReleaseExhaustiveScan();
            }
            await assetScanTask.ConfigureAwait(false);
            await GameScanQueue.Instance.WhenIdleAsync().ConfigureAwait(false);
            Logger.Info($"Game library discovery and processing completed in {loadStopwatch.Elapsed.TotalSeconds:N2} seconds.");
        }
        finally
        {
            try
            {
                await GameDatabaseWriteBatch.Instance.EndAndFlushAsync().ConfigureAwait(false);
            }
            finally
            {
                await EndUiBatchAsync().ConfigureAwait(false);
                _loadGate.Release();
            }
        }
    }

    public ICollectionView GetGameCollection(string? filterText = null)
    {
        using (FavouriteGamesView.DeferRefresh())
        {
            FavouriteGamesView.Filter = GetPredicateForFavouriteGames(Settings.Instance.HideNonDLSSGames, filterText);
        }

        using (AllGamesView.DeferRefresh())
        {
            AllGamesView.Filter = GetPredicateForAllGames(Settings.Instance.HideNonDLSSGames, filterText);
        }

        if (Settings.Instance.GroupGameLibrariesTogether)
        {
            // Only refresh libraries when we are going to the grouped view.
            foreach (var keyValuePair in libraryGamesView)
            {
                using (keyValuePair.Value.DeferRefresh())
                {
                    keyValuePair.Value.Filter = GetPredicateForLibraryGames(keyValuePair.Key, Settings.Instance.HideNonDLSSGames, filterText);
                }
            }

            return GroupedGameCollectionViewSource.View;
        }
        else
        {
            return UngroupedGameCollectionViewSource.View;
        }
    }

    public List<Game> GetSynchronisedGamesListCopy()
    {
        lock (gameLock)
        {
            var list = new List<Game>(_synchronisedAllGames);
            return list;
        }
    }

    public Game AddGame(Game game, bool scrollIntoView = false)
    {
        lock (gameLock)
        {
            if (_synchronisedAllGames.Contains(game) == true)
            {
                var oldGame = _synchronisedAllGames.First(x => x.Equals(game));

                void UpdateExistingGame()
                {
                    oldGame.UpdateFromGame(game);
                }

                if (_batchUiChanges)
                {
                    _pendingUiChanges.Add(UpdateExistingGame);
                }
                else
                {
                    App.CurrentApp.RunOnUIThread(UpdateExistingGame);
                }

                Debug.WriteLine($"Reusing old game: {game.Title}");
                return oldGame;
            }
            else
            {
                Debug.WriteLine($"Adding new game: {game.Title}");

                _synchronisedAllGames.Add(game);

                // Attach an existing local cover before the card enters the UI.
                // Artwork loading is independent of detected DLL families.
                if (game.PrimeCachedCoverImage() == false)
                {
                    GameCoverHydrationQueue.Instance.Enqueue(game);
                }

                void AddNewGame()
                {
                    _allGames.Add(game);

                    if (scrollIntoView)
                    {
                        App.CurrentApp.MainWindow.GameGridPage?.ScrollToGame(game);
                    }
                }

                if (_batchUiChanges)
                {
                    _pendingUiChanges.Add(AddNewGame);
                }
                else
                {
                    App.CurrentApp.RunOnUIThread(AddNewGame);
                }

                return game;
            }
        }
    }

    internal bool ContainsGame(Game game)
    {
        lock (gameLock)
        {
            return _synchronisedAllGames.Contains(game);
        }
    }

    void BeginUiBatch()
    {
        lock (gameLock)
        {
            _batchUiChanges = true;
        }
    }

    async Task EndUiBatchAsync()
    {
        await FlushPendingUiChangesAsync().ConfigureAwait(false);
        lock (gameLock)
        {
            _batchUiChanges = false;
        }
    }

    async Task FlushPendingUiChangesAsync()
    {
        while (true)
        {
            List<Action> changes;
            lock (gameLock)
            {
                if (_pendingUiChanges.Count == 0)
                {
                    return;
                }

                var batchSize = Math.Min(Settings.Instance.UiCollectionBatchSize, _pendingUiChanges.Count);
                changes = _pendingUiChanges.GetRange(0, batchSize);
                _pendingUiChanges.RemoveRange(0, batchSize);
            }

            var nextChange = 0;
            while (nextChange < changes.Count)
            {
                await App.CurrentApp.RunOnUIThreadAsync(() =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    do
                    {
                        changes[nextChange++]();
                    }
                    while (nextChange < changes.Count && stopwatch.ElapsedMilliseconds < 8);

                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                await Task.Yield();
            }
        }
    }

    public void RemoveGame(Game game)
    {
        lock (gameLock)
        {
            _synchronisedAllGames.Remove(game);

            App.CurrentApp.RunOnUIThread(() =>
            {
                _allGames.Remove(game);
            });
        }
    }

    public void RemoveAllGames()
    {
        lock (gameLock)
        {
            // TODO: Cancel loading of games here
            _synchronisedAllGames.Clear();

            App.CurrentApp.RunOnUIThread(() =>
            {
                _allGames.Clear();
            });
        }
    }

    public TGame? GetGame<TGame>(string platformId) where TGame : Game
    {
        lock (gameLock)
        {
            foreach (var game in _synchronisedAllGames)
            {
                if (game is TGame platformGame)
                {
                    if (game.PlatformId == platformId)
                    {
                        return platformGame;
                    }
                }
            }
        }

        return null;
    }

    public List<TGame> GetGames<TGame>() where TGame : Game
    {
        lock (gameLock)
        {
            var games = new List<TGame>();
            foreach (var game in _synchronisedAllGames)
            {
                if (game is TGame tGame)
                {
                    games.Add(tGame);
                }
            }
            return games;
        }
    }

    public bool CheckIfGameIsAdded(string installPath)
    {
        lock (gameLock)
        {
            foreach (var game in _synchronisedAllGames)
            {
                if (game.InstallPath?.Equals(installPath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public GameLibrarySettings? GetGameLibrarySettings(GameLibrary gameLibrary)
    {
        return Settings.Instance.GameLibrarySettings.FirstOrDefault(x => x.GameLibrary == gameLibrary);
    }

    public List<GameLibrary> GetGameLibraries(bool onlyEnabled)
    {
        var gameLibrariesToReturn = new List<GameLibrary>();

        foreach (var gameLibrarySetting in Settings.Instance.GameLibrarySettings)
        {
            if (gameLibrarySetting.IsEnabled == false && onlyEnabled == true)
            {
                continue;
            }

            gameLibrariesToReturn.Add(gameLibrarySetting.GameLibrary);
        }

        return gameLibrariesToReturn;

    }

    public bool CanLaunchGame(Game game)
    {
        if (game is DLSS_Swapper.Data.ManuallyAdded.ManuallyAddedGame manual)
            return !string.IsNullOrWhiteSpace(manual.LaunchExecutable)
                && !DLSS_Swapper.Data.ManuallyAdded.ManualLaunchManifest.IsExcluded(manual.LaunchExecutable)
                && manual.LaunchExecutable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && File.Exists(manual.LaunchExecutable);
        // Xbox App games are only valid if ApplicationId is loaded.
        if (game.GameLibrary == GameLibrary.XboxApp)
        {
            if (game is XboxGame xboxGame && string.IsNullOrWhiteSpace(xboxGame.ApplicationId) == false)
            {
                return true;
            }
        }

        // We can only launch Battle.net games if Battle.net client is installed
        if (game.GameLibrary == GameLibrary.BattleNet)
        {
            if (game is BattleNetGame battleNetGame)
            {
                if (string.IsNullOrWhiteSpace(battleNetGame.LauncherId) == false)
                {
                    return true;
                }
            }
        }

        return game.GameLibrary switch
        {
            GameLibrary.Steam => true,
            GameLibrary.EpicGamesStore => true,
            GameLibrary.EAApp => true,
            _ => false,
        };
    }

    public async Task LaunchGameAsync(Game game)
    {
        if (CanLaunchGame(game) == false)
        {
            Logger.Error($"Cannot launch game {game.Title} from {game.GameLibrary}");
            return;
        }

        if (game is DLSS_Swapper.Data.ManuallyAdded.ManuallyAddedGame manual)
        {
            var manifest = DLSS_Swapper.Data.ManuallyAdded.ManualLaunchManifest.Validate(
                manual.LaunchExecutable!, manual.LaunchArguments ?? "", manual.LaunchWorkingDirectory ?? "");
            var startInfo = new ProcessStartInfo(manifest.Executable)
            {
                UseShellExecute = true,
                Arguments = manifest.Arguments,
                WorkingDirectory = manifest.WorkingDirectory,
            };
            using var process = Process.Start(startInfo);
        }
        else if (game.GameLibrary == GameLibrary.Steam)
        {
            await Launcher.LaunchUriAsync(new Uri($"steam://rungameid/{game.PlatformId}"));
        }
        else if (game.GameLibrary == GameLibrary.EpicGamesStore)
        {
            var installPathString = Uri.EscapeDataString(game.InstallPath);
            await Launcher.LaunchUriAsync(new Uri($"com.epicgames.launcher://apps/{installPathString}?action=launch&silent=true"));
        }
        else if (game.GameLibrary == GameLibrary.EAApp)
        {
            await Launcher.LaunchUriAsync(new Uri($"origin2://game/launch?offerIds={game.PlatformId}"));
        }
        else if (game.GameLibrary == GameLibrary.XboxApp)
        {
            if (game is XboxGame xboxGame)
            {
                var launchCode = $"shell:appsFolder\\{xboxGame.PlatformId}!{xboxGame.ApplicationId}";
                var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                startInfo.ArgumentList.Add(launchCode);
                using var process = Process.Start(startInfo);
            }
        }
        else if (game.GameLibrary == GameLibrary.BattleNet)
        {
            if (game is BattleNetGame battleNetGame && File.Exists(BattleNetLibrary.Instance.ClientPath))
            {
                var startInfo = new ProcessStartInfo(BattleNetLibrary.Instance.ClientPath) { UseShellExecute = true };
                startInfo.ArgumentList.Add($"--exec=launch {battleNetGame.LauncherId}");
                using var process = Process.Start(startInfo);
            }
        }
    }
}
