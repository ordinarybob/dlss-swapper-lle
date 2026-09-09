using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Messages;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace DLSS_Swapper.Pages;

public enum GameGridViewType
{
    GridView,
    ListView,
}

public partial class GameGridPageModel : ObservableObject
{
    const int ScanProgressBatchSize = 100;

    GameGridPage gameGridPage;
    readonly DispatcherQueueTimer _visibleGameCountTimer;

    [ObservableProperty]
    public partial Game? SelectedGame { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(CanUseHeaderControls))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(CanToggleSelectionMode))]
    [NotifyPropertyChangedFor(nameof(CanApplyBatchDll))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelectedGames))]
    [NotifyPropertyChangedFor(nameof(VisibleGameCountText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBatchDllCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedGamesCommand))]
    public partial bool IsGameListLoading { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(CanToggleSelectionMode))]
    [NotifyPropertyChangedFor(nameof(CanApplyBatchDll))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelectedGames))]
    [NotifyPropertyChangedFor(nameof(VisibleGameCountText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBatchDllCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedGamesCommand))]
    public partial bool IsDLSSLoading { get; set; } = true;

    public bool IsLoading => (IsGameListLoading || IsDLSSLoading);

    public bool CanUseHeaderControls => IsGameListLoading == false && IsSelectionMode == false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(CanToggleSelectionMode))]
    [NotifyPropertyChangedFor(nameof(VisibleGameCountText))]
    public partial bool IsBackgroundScanRunning { get; set; }

    public bool CanRefresh => IsLoading == false
        && IsBackgroundScanRunning == false
        && IsSelectionMode == false;

    public bool CanToggleSelectionMode => IsLoading == false
        && IsBackgroundScanRunning == false
        && IsBatchUpdateRunning == false;

    [ObservableProperty]
    public partial string ScanProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ICollectionView? CurrentCollectionView { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleGameCountText))]
    public partial int VisibleGameCount { get; set; }

    public string VisibleGameCountText => IsLoading || IsBackgroundScanRunning
        ? string.Empty
        : $"({VisibleGameCount:N0})";

    [ObservableProperty]
    public partial double GridViewCardSize { get; set; } = Settings.Instance.GridViewCardSize;


    // Placeholder card size used until the first layout pass measures the real
    // grid viewport; UpdateResponsiveGridLayout keeps both in sync at 2:3.
    [ObservableProperty]
    public partial double GridViewCardWidth { get; set; } = 112;

    [ObservableProperty]
    public partial double GridViewCardHeight { get; set; } = 168;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameGridViewIcon))]
    [NotifyPropertyChangedFor(nameof(IsGridView))]
    public partial GameGridViewType GameGridViewType { get; set; } = Settings.Instance.GameGridViewType;

    public bool IsGridView => GameGridViewType == global::DLSS_Swapper.Pages.GameGridViewType.GridView;

    public FontIcon GameGridViewIcon => GameGridViewType switch
    {
        GameGridViewType.GridView => new FontIcon() { Glyph = "\xF0E2" },
        GameGridViewType.ListView => new FontIcon() { Glyph = "\xE8FD" },
        _ => new FontIcon() { },
    };

    [ObservableProperty]
    public partial GameSortMode GameSortMode { get; set; } = Settings.Instance.GameSortMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseHeaderControls))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    public partial bool IsSelectionMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyBatchDll))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelectedGames))]
    [NotifyPropertyChangedFor(nameof(CanToggleSelectionMode))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBatchDllCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedGamesCommand))]
    public partial bool IsBatchUpdateRunning { get; set; }

    public List<Game> SelectedGames { get; } = new List<Game>();

    public string SelectedGamesCountText =>
        ResourceHelper.GetFormattedResourceTemplate(
            "GamesPage_SelectionMode_CountTemplate",
            SelectedGames.Count);

    public bool CanApplyBatchDll =>
        SelectedGames.Count > 0
        && IsLoading == false
        && IsBatchUpdateRunning == false
        && SelectedGames.All(game => game.Processing == false);

    public bool CanRemoveSelectedGames =>
        SelectedGames.Count > 0
        && IsLoading == false
        && IsBatchUpdateRunning == false
        && SelectedGames.All(game => game.Processing == false);

    bool AreAllVisibleGamesSelected =>
        gameGridPage.GetVisibleItemCount() > 0
        && gameGridPage.GetVisibleSelectedCount() == gameGridPage.GetVisibleItemCount();

    public string SelectAllButtonText => AreAllVisibleGamesSelected
        ? ResourceHelper.GetString("GamesPage_SelectionMode_DeselectAll")
        : ResourceHelper.GetString("GamesPage_SelectionMode_SelectAll");

    public GameGridPageModelTranslationProperties TranslationProperties { get; } = new GameGridPageModelTranslationProperties();

    public GameGridPageModel(GameGridPage gameGridPage)
    {
        ArgumentNullException.ThrowIfNull(gameGridPage);
        WeakReferenceMessenger.Default.Register<GameLibrariesStateChangedMessage>(this, async (sender, message) =>
        {
            GameManager.Instance.RemoveAllGames();
            await InitialLoadAsync();
        });
        WeakReferenceMessenger.Default.Register<GridDensityChangedMessage>(this, (sender, message) =>
        {
            GridViewCardSize = Settings.Instance.GridViewCardSize;
            gameGridPage.RefreshResponsiveGridLayout();
        });

        this.gameGridPage = gameGridPage;
        _visibleGameCountTimer = gameGridPage.DispatcherQueue.CreateTimer();
        _visibleGameCountTimer.Interval = TimeSpan.FromMilliseconds(250);
        _visibleGameCountTimer.IsRepeating = false;
        _visibleGameCountTimer.Tick += (_, _) => UpdateVisibleGameCount();
        GameManager.Instance.AllGamesView.VectorChanged += (_, _) => QueueVisibleGameCountUpdate();
        ApplyGameGroupFilter();
    }

    void QueueVisibleGameCountUpdate()
    {
        if (IsLoading == false
            && IsBackgroundScanRunning == false
            && _visibleGameCountTimer.IsRunning == false)
        {
            _visibleGameCountTimer.Start();
        }
    }

    void UpdateVisibleGameCount()
    {
        if (IsLoading || IsBackgroundScanRunning)
        {
            return;
        }

        VisibleGameCount = GameManager.Instance.AllGamesView.Count;
    }

    void PublishVisibleGameCount()
    {
        _visibleGameCountTimer.Stop();
        VisibleGameCount = GameManager.Instance.AllGamesView.Count;
    }

    partial void OnGridViewCardSizeChanged(double value)
    {
        if (double.IsFinite(value))
        {
            Settings.Instance.GridViewCardSize = (int)Math.Round(value);
        }
    }

    [RelayCommand]
    void ToggleSelectionMode()
    {
        if (IsSelectionMode == false)
        {
            IsSelectionMode = true;
            gameGridPage.EnterSelectionMode();
            return;
        }

        ExitSelectionMode();
    }

    [RelayCommand]
    void ToggleSelectAll()
    {
        if (AreAllVisibleGamesSelected)
        {
            gameGridPage.DeselectAllVisible();
        }
        else
        {
            gameGridPage.SelectAllVisible();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyBatchDll))]
    async Task ApplyBatchDllAsync()
    {
        var games = SelectedGames.ToList();
        if (games.Count == 0
            || IsLoading
            || IsBatchUpdateRunning
            || games.Any(game => game.Processing))
        {
            return;
        }

        var pickerDialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_Batch_Title"),
            PrimaryButtonText = ResourceHelper.GetString("General_Apply"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        pickerDialog.Resources["ContentDialogMaxWidth"] = 680d;
        var picker = new BatchDllPickerControl(pickerDialog, games);
        pickerDialog.Content = picker;

        if (await pickerDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var dllSelections = picker.ViewModel.PlannedDllActions;
        var presetSelections = picker.ViewModel.PlannedPresetActions;
        var streamlineSelections = picker.ViewModel.PlannedStreamlineActions;
        if (dllSelections.Count == 0 && presetSelections.Count == 0 && streamlineSelections.Count == 0)
        {
            return;
        }

        var partialApproved = false;
        if (streamlineSelections.Count > 0 && DLSS_Swapper.Data.Streamline.StreamlineBatchUpdateWorkflow.HasPartialSets(
            picker.ViewModel.StreamlineGames, streamlineSelections))
        {
            var warning = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = "Mixed Streamline versions",
                Content = "This selection updates only part of Streamline in one or more games. Mixing versions is not recommended: it can cause crashes or broken graphics features. Update the complete component set whenever possible. Continue with this partial selection?",
                PrimaryButtonText = "Continue with partial sets",
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await warning.ShowAsync() != ContentDialogResult.Primary) return;
            partialApproved = true;
        }
        if (IsLoading || IsBatchUpdateRunning || games.Any(game => game.Processing)) return;
        IsBatchUpdateRunning = true;
        try
        {
            var results = await DllUpdateWorkflow.ApplyAsync(games, dllSelections);
            results.AddRange(await DLSS_Swapper.Data.Streamline.StreamlineBatchUpdateWorkflow.ApplyAsync(
                picker.ViewModel.StreamlineGames, streamlineSelections, partialApproved, picker.ViewModel.SelectedStreamlineRelease));
            results.AddRange(await BatchPresetUpdateWorkflow.ApplyAsync(games, presetSelections));
            var summaryDialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_Batch_Summary_Title"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Close,
                Content = new BatchSwapSummaryControl(results),
            };
            await summaryDialog.ShowAsync();
        }
        finally
        {
            IsBatchUpdateRunning = false;
        }

        ExitSelectionMode();
    }

    internal void UpdateSelection(IList<object> addedItems, IList<object> removedItems)
    {
        foreach (var item in removedItems)
        {
            if (item is Game game)
            {
                var selectedIndex = SelectedGames.FindIndex(
                    selectedGame => ReferenceEquals(selectedGame, game));
                if (selectedIndex >= 0)
                {
                    game.PropertyChanged -= SelectedGame_PropertyChanged;
                    SelectedGames.RemoveAt(selectedIndex);
                }
            }
        }

        foreach (var item in addedItems)
        {
            if (item is Game game
                && SelectedGames.Any(selectedGame =>
                    ReferenceEquals(selectedGame, game)) == false)
            {
                SelectedGames.Add(game);
                game.PropertyChanged += SelectedGame_PropertyChanged;
            }
        }

        NotifySelectionChanged();
    }

    void ExitSelectionMode()
    {
        gameGridPage.ExitSelectionMode();
        foreach (var game in SelectedGames)
        {
            game.PropertyChanged -= SelectedGame_PropertyChanged;
        }
        SelectedGames.Clear();
        IsSelectionMode = false;
        NotifySelectionChanged();
    }

    void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedGamesCountText));
        OnPropertyChanged(nameof(SelectAllButtonText));
        OnPropertyChanged(nameof(CanApplyBatchDll));
        OnPropertyChanged(nameof(CanRemoveSelectedGames));
        ApplyBatchDllCommand.NotifyCanExecuteChanged();
        RemoveSelectedGamesCommand.NotifyCanExecuteChanged();
    }

    void SelectedGame_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Game.Processing))
        {
            return;
        }

        OnPropertyChanged(nameof(CanApplyBatchDll));
        OnPropertyChanged(nameof(CanRemoveSelectedGames));
        ApplyBatchDllCommand.NotifyCanExecuteChanged();
        RemoveSelectedGamesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedGames))]
    async Task RemoveSelectedGamesAsync()
    {
        var games = SelectedGames.ToArray();
        if (games.Length == 0 || CanRemoveSelectedGames == false)
        {
            return;
        }

        var manuallyAddedCount = games.Count(
            game => game.GameLibrary == Interfaces.GameLibrary.ManuallyAdded);
        var discoveredCount = games.Length - manuallyAddedCount;
        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetFormattedResourceTemplate(
                "GamesPage_SelectionMode_RemoveTitleTemplate",
                games.Length),
            PrimaryButtonText = ResourceHelper.GetString("General_Remove"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            Content = ResourceHelper.GetFormattedResourceTemplate(
                "GamesPage_SelectionMode_RemoveDescriptionTemplate",
                games.Length,
                manuallyAddedCount,
                discoveredCount),
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        IsBatchUpdateRunning = true;
        gameGridPage.BeginSuppressSelectionEvents();
        var failedGames = new List<Game>();
        try
        {
            foreach (var game in games)
            {
                if (game.GameLibrary == Interfaces.GameLibrary.ManuallyAdded)
                {
                    if (await game.DeleteAsync() == false)
                    {
                        failedGames.Add(game);
                    }
                }
                else
                {
                    var previousHiddenState = game.IsHidden;
                    game.IsHidden = true;
                    if (await game.SaveToDatabaseAsync() == false)
                    {
                        game.IsHidden = previousHiddenState;
                        failedGames.Add(game);
                    }
                }
            }
        }
        finally
        {
            IsBatchUpdateRunning = false;
            ExitSelectionMode();
        }

        if (failedGames.Count > 0)
        {
            var failureDialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetFormattedResourceTemplate(
                    "GamesPage_SelectionMode_RemoveFailedTemplate",
                    failedGames.Count),
            };
            await failureDialog.ShowAsync();
        }
    }

    public async Task InitialLoadAsync()
    {
        IsGameListLoading = true;
        IsDLSSLoading = true;
        ScanProgressText = ResourceHelper.GetString("General_Loading");

        await GameManager.Instance.LoadGamesFromCacheAsync();

        IsGameListLoading = false;
        gameGridPage.PrepareVisibleCoverWait();
        var runInitialDeepScan = Settings.Instance.HasCompletedInitialDeepScan == false;

        try
        {
            await LoadGamesWithProgressAsync(
                forceNeedsProcessing: runInitialDeepScan,
                exhaustiveScan: runInitialDeepScan,
                candidateLibraryReady: async () =>
                {
                    IsBackgroundScanRunning = true;
                    IsDLSSLoading = false;
                    await gameGridPage.WaitForVisibleCoverAsync();
                });

            if (runInitialDeepScan)
            {
                Settings.Instance.HasCompletedInitialDeepScan = true;
            }
        }
        finally
        {
            PublishVisibleGameCount();
            IsBackgroundScanRunning = false;
            IsDLSSLoading = false;
        }

    }

    async Task LoadGamesWithProgressAsync(
        bool forceNeedsProcessing,
        bool exhaustiveScan = false,
        Func<Task>? candidateLibraryReady = null)
    {
        var scanQueue = GameScanQueue.Instance;
        var initialProgress = scanQueue.GetProgress();
        var loadTask = GameManager.Instance.LoadGamesAsync(
            forceNeedsProcessing,
            exhaustiveScan,
            candidateLibraryReady);

        try
        {
            while (loadTask.IsCompleted == false)
            {
                UpdateScanProgress(scanQueue.GetProgress(), initialProgress);
                await Task.WhenAny(loadTask, Task.Delay(250));
            }

            await loadTask;
        }
        finally
        {
            ScanProgressText = string.Empty;
        }
    }

    void UpdateScanProgress(GameScanQueue.Progress currentProgress, GameScanQueue.Progress initialProgress)
    {
        var enqueued = currentProgress.Enqueued - initialProgress.Enqueued;
        if (enqueued <= 0)
        {
            ScanProgressText = ResourceHelper.GetString("General_Loading");
            return;
        }

        var completed = Math.Clamp(currentProgress.Completed - initialProgress.Completed, 0, enqueued);
        var completedBatch = completed / ScanProgressBatchSize * ScanProgressBatchSize;
        if (completedBatch == 0 && completed < enqueued)
        {
            ScanProgressText = ResourceHelper.GetString("General_Loading");
            return;
        }

        var displayedCompleted = completed == enqueued ? completed : completedBatch;
        ScanProgressText = $"{ResourceHelper.GetString("General_Loading")} {displayedCompleted:N0} / {enqueued:N0}";
    }

    string _activeSearch = string.Empty;

    public void SearchForGameEvent(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            throw new ArgumentException("Sender must be a TextBox");
        }

        _activeSearch = textBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(_activeSearch))
        {
            CurrentCollectionView = GameManager.Instance.GetGameCollection();
            return;
        }
        CurrentCollectionView = GameManager.Instance.GetGameCollection(_activeSearch);
    }

    (EasyContentDialog Dialog, CheckBox DontShowAgainCheckbox) CreateManualImportNotice(
        string descriptionResource,
        string primaryButtonResource)
    {
        var dontShowAgainCheckbox = new CheckBox()
        {
            Content = ResourceHelper.GetString("General_DontShowAgain"),
            MinWidth = 0,
        };

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            PrimaryButtonText = ResourceHelper.GetString(primaryButtonResource),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel()
            {
                Children =
                {
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Text = ResourceHelper.GetString(descriptionResource),
                    },
                    dontShowAgainCheckbox,
                },
                Orientation = Orientation.Vertical,
                Spacing = 8,
            },
        };

        // The stock ContentDialog minimum height and padding are intended for
        // larger prompts. These notices only need one explanation and one opt-out.
        dialog.Resources["ContentDialogMinWidth"] = 0d;
        dialog.Resources["ContentDialogMaxWidth"] = 480d;
        dialog.Resources["ContentDialogMinHeight"] = 0d;
        dialog.Resources["ContentDialogPadding"] = new Thickness(16, 12, 16, 12);

        return (dialog, dontShowAgainCheckbox);
    }

    [RelayCommand]
    async Task AddManualGameButtonAsync()
    {
        if (Settings.Instance.DontShowManuallyAddingGamesNotice == false)
        {
            var (dialog, dontShowAgainCheckbox) = CreateManualImportNotice(
                "GamesPage_ManuallyAdding_SingleFolderDescription",
                "GamesPage_ManuallyAdding_SelectGameFolder");

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.None)
            {
                return;
            }


            if (result == ContentDialogResult.Primary)
            {
                // Only dismiss the notice for good once the user has proceeded to add games.
                if (dontShowAgainCheckbox.IsChecked == true)
                {
                    Settings.Instance.DontShowManuallyAddingGamesNotice = true;
                }
                await AddGameManually();
            }
        }
        else
        {
            await AddGameManually();
        }
    }

    [RelayCommand]
    async Task AddManualGamesButtonAsync()
    {
        if (Settings.Instance.DontShowAddMultipleGameFoldersNotice == false)
        {
            var (explanation, dontShowAgainCheckbox) = CreateManualImportNotice(
                "GamesPage_ManuallyAdding_MultipleFoldersDescription",
                "GamesPage_ManuallyAdding_SelectGameFolders");
            if (await explanation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            if (dontShowAgainCheckbox.IsChecked == true)
            {
                Settings.Instance.DontShowAddMultipleGameFoldersNotice = true;
            }
        }

        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);
            var folders = FileSystemHelper.OpenMultipleFolders(
                hWnd,
                okButtonLabel: ResourceHelper.GetString("GamesPage_ManuallyAdding_SelectGameFolders"));
            if (folders.Count == 0)
            {
                return;
            }
            await ImportManualGamesAsync(folders);
        }
        catch (Exception err)
        {
            await ShowManualGameImportErrorAsync(err);
        }
    }

    [RelayCommand]
    async Task AddManualGamesDirectoryButtonAsync()
    {
        if (Settings.Instance.DontShowAddMultiGameDirectoryNotice == false)
        {
            var (explanation, dontShowAgainCheckbox) = CreateManualImportNotice(
                "GamesPage_ManuallyAdding_MultiGameDirectoryDescription",
                "GamesPage_ManuallyAdding_SelectMultiGameDirectory");
            if (await explanation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            if (dontShowAgainCheckbox.IsChecked == true)
            {
                Settings.Instance.DontShowAddMultiGameDirectoryNotice = true;
            }
        }

        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);
            var parentFolder = FileSystemHelper.OpenFolder(
                hWnd,
                okButtonLabel: ResourceHelper.GetString("GamesPage_ManuallyAdding_SelectMultiGameDirectory"));
            if (string.IsNullOrWhiteSpace(parentFolder))
            {
                return;
            }
            if (IsTopLevelDirectory(parentFolder))
            {
                await ShowTopLevelDirectoryNotSupportedAsync();
                return;
            }

            var folders = await Task.Run(() => Directory
                .EnumerateDirectories(parentFolder, "*", SearchOption.TopDirectoryOnly)
                .ToArray());
            await ImportManualGamesAsync(folders);
        }
        catch (Exception err)
        {
            await ShowManualGameImportErrorAsync(err);
        }
    }

    async Task ImportManualGamesAsync(IEnumerable<string> candidatePaths)
    {
        var importedGames = new List<ManuallyAddedGame>();
        var added = 0;
        var alreadyPresent = 0;
        var failed = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidatePath in candidatePaths)
        {
            if (IsTopLevelDirectory(candidatePath))
            {
                failed.Add($"{candidatePath}: {ResourceHelper.GetString("GamesPage_ManuallyAdding_TopLevelDirectoryNotSupported")}");
                continue;
            }

            var installPath = PathHelpers.NormalizePath(candidatePath);
            if (seen.Add(installPath) == false)
            {
                continue;
            }
            if (Directory.Exists(installPath) == false)
            {
                failed.Add($"{installPath}: {ResourceHelper.GetString("GamesPage_ManuallyAdding_BulkDirectoryMissing")}");
                continue;
            }
            if (GameManager.Instance.CheckIfGameIsAdded(installPath))
            {
                alreadyPresent++;
                continue;
            }

            ManuallyAddedGame? game = null;
            try
            {
                game = ManuallyAddedGame.CreateForInstallPath(installPath);
                if (!await game.SaveToDatabaseAsync(bypassBatch: true))
                    throw new IOException(ResourceHelper.GetString("GamePage_ManualImportSaveFailed"));
                game.ProcessGame();
                GameManager.Instance.AddGame(game);
                importedGames.Add(game);
                added++;
            }
            catch (Exception err)
            {
                Logger.Error(err, $"Could not add manual game folder \"{installPath}\".");
                // A failed save must not trigger destructive cleanup of possibly existing state.
                failed.Add($"{installPath}: {err.Message}");
            }
        }

        await ManualLaunchSetup.OfferAsync(gameGridPage.XamlRoot, importedGames);
        var summary = new List<string>
        {
            ResourceHelper.GetFormattedResourceTemplate(
                "GamesPage_ManuallyAdding_BulkSummaryTemplate",
                added,
                alreadyPresent,
                failed.Count),
        };
        if (failed.Count > 0)
        {
            summary.Add(string.Empty);
            summary.Add(ResourceHelper.GetString("GamesPage_ManuallyAdding_BulkSummaryFailed"));
            summary.AddRange(failed.Select(item => $"• {item}"));
        }

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_BulkSummaryTitle"),
            CloseButtonText = ResourceHelper.GetString("General_Close"),
            DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = new TextBlock
                {
                    Text = string.Join(Environment.NewLine, summary),
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                },
            },
        };
        await dialog.ShowAsync();
    }

    static bool IsTopLevelDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);
        return string.IsNullOrWhiteSpace(rootPath) == false
            && string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }

    async Task ShowTopLevelDirectoryNotSupportedAsync()
    {
        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            DefaultButton = ContentDialogButton.Close,
            Title = ResourceHelper.GetString("General_Error"),
            Content = ResourceHelper.GetString("GamesPage_ManuallyAdding_TopLevelDirectoryNotSupported"),
        };
        await dialog.ShowAsync();
    }

    async Task ShowManualGameImportErrorAsync(Exception err)
    {
        Logger.Error(err, "Bulk manual game import failed.");
        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_ErrorTitle"),
            CloseButtonText = ResourceHelper.GetString("General_Close"),
            DefaultButton = ContentDialogButton.Close,
            Content = $"{ResourceHelper.GetString("GamesPage_ManuallyAdding_CouldntAddError")}\n\n{err.Message}",
        };
        await dialog.ShowAsync();
    }

    async Task AddGameManually()
    {
        var installPath = string.Empty;
        try
        {
            // Associate the HWND with the folder picker
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);


            var folder = FileSystemHelper.OpenFolder(hWnd, okButtonLabel: ResourceHelper.GetString("GamesPage_ManuallyAdding_SelectGameFolder"));

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            installPath = folder;

            // If top level directory throw error.
            if (IsTopLevelDirectory(installPath))
            {
                await ShowTopLevelDirectoryNotSupportedAsync();
                return;
            }


            var gameFolderAlreadyExists = GameManager.Instance.CheckIfGameIsAdded(installPath);
            if (gameFolderAlreadyExists == true)
            {
                var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_ErrorTitle"),
                    CloseButtonText = ResourceHelper.GetString("General_Close"),
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamesPage_ManuallyAdding_PathExistsTemplate", installPath),
                };
                await dialog.ShowAsync();
                return;
            }

            var manuallyAddGameControl = new ManuallyAddGameControl(installPath);
            var addGameDialog = new FakeContentDialog() //XamlRoot
            {
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                PrimaryButtonText = ResourceHelper.GetString("GamesPage_AddGame"),
                DefaultButton = ContentDialogButton.Primary,
                Content = manuallyAddGameControl,
            };
            addGameDialog.Resources["ContentDialogMinWidth"] = 700;
            addGameDialog.Resources["ContentDialogMaxWidth"] = 700;

            var addGameResult = await addGameDialog.ShowAsync();
            if (manuallyAddGameControl.DataContext is ManuallyAddGameModel manuallyAddGameModel)
            {
                if (addGameResult == ContentDialogResult.Primary)
                {
                    var game = manuallyAddGameModel.Game;
                    if (!await game.SaveToDatabaseAsync(bypassBatch: true))
                        throw new IOException(ResourceHelper.GetString("GamePage_ManualImportSaveFailed"));
                    game.ProcessGame();
                    GameManager.Instance.AddGame(game, true);
                    await ManualLaunchSetup.OfferAsync(gameGridPage.XamlRoot, new[] { game });
                }
                else
                {
                    // Cleanup if user is going back.
                    await manuallyAddGameModel.Game.DeleteAsync();
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Attempted to manually add game from path \"{installPath}\" but got an error.");
            var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_ErrorTitle"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Close,
                Content = $"{ResourceHelper.GetString("GamesPage_ManuallyAdding_CouldntAddError")}\n\n{ResourceHelper.GetString("General_ErrorMessage")}: {err.Message}",
            };
            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task DeepScanAsync()
    {
        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_DeepScan_Title"),
            PrimaryButtonText = ResourceHelper.GetString("GamesPage_DeepScan_Start"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            Content = ResourceHelper.GetString("GamesPage_DeepScan_Description"),
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        IsDLSSLoading = true;
        try
        {
            await LoadGamesWithProgressAsync(
                forceNeedsProcessing: true,
                exhaustiveScan: true);
            Settings.Instance.HasCompletedInitialDeepScan = true;
        }
        finally
        {
            PublishVisibleGameCount();
            IsDLSSLoading = false;
        }
    }

    [RelayCommand]
    async Task RefreshGamesButtonAsync()
    {
        IsDLSSLoading = true;
        try
        {
            await LoadGamesWithProgressAsync(true);
        }
        finally
        {
            PublishVisibleGameCount();
            IsDLSSLoading = false;
        }
    }

    [RelayCommand]
    async Task RestoreExcludedLauncherGamesAndRefreshAsync()
    {
        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_Refresh_RestoreExcluded_Title"),
            PrimaryButtonText = ResourceHelper.GetString("GamesPage_Refresh_RestoreExcluded_Primary"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            Content = ResourceHelper.GetString("GamesPage_Refresh_RestoreExcluded_Description"),
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        IsDLSSLoading = true;
        try
        {
            var enabledLibraries = GameManager.Instance.GetGameLibraries(true).ToHashSet();
            var excludedGames = GameManager.Instance.GetSynchronisedGamesListCopy()
                .Where(game => game.GameLibrary != Interfaces.GameLibrary.ManuallyAdded
                    && enabledLibraries.Contains(game.GameLibrary)
                    && game.IsHidden == true)
                .ToArray();

            var failed = new List<string>();
            foreach (var game in excludedGames)
            {
                var previous = game.IsHidden;
                game.IsHidden = null;
                if (!await game.SaveToDatabaseAsync(bypassBatch: true))
                {
                    game.IsHidden = previous;
                    failed.Add(game.Title);
                }
            }

            await LoadGamesWithProgressAsync(true);
            if (failed.Count > 0)
            {
                await new EasyContentDialog(gameGridPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Close"),
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamePage_RestoreVisibilityFailed", string.Join("\n", failed)),
                }.ShowAsync();
            }
        }
        finally
        {
            PublishVisibleGameCount();
            IsDLSSLoading = false;
        }
    }

    internal void ApplyGameFilter(GameFilterControlViewModel gameFilterControlViewModel)
    {
        Settings.Instance.HideNonDLSSGames = gameFilterControlViewModel.HideNonSwappableGames;
        GameManager.Instance.ShowHiddenGames = gameFilterControlViewModel.ShowHiddenGames;
        Settings.Instance.GroupGameLibrariesTogether = gameFilterControlViewModel.GroupGameLibrariesTogether;
        ApplyGameGroupFilter();
    }

    void ApplyGameGroupFilter()
    {
        // TODO: Remove weird hack which otherwise causes MainGridView_SelectionChanged to fire when changing MainGridView.ItemsSource.
        //gameGridPage.MainGridView.SelectionChanged -= MainGridView_SelectionChanged;

        //MainGridView.ItemsSource = null;
        CurrentCollectionView = null;
        CurrentCollectionView = GameManager.Instance.GetGameCollection(_activeSearch);
    }

    [RelayCommand]
    void ChangeGameGridView(GameGridViewType gameGridView)
    {
        if (gameGridView == this.GameGridViewType)
        {
            return;
        }

        GameGridViewType = gameGridView;
        gameGridPage.ReloadMainContentControl();
        Settings.Instance.GameGridViewType = gameGridView;
    }

    [RelayCommand]
    void ChangeGameSort(GameSortMode gameSortMode)
    {
        if (gameSortMode == GameSortMode)
        {
            return;
        }

        GameSortMode = gameSortMode;
        GameManager.Instance.ApplySort(gameSortMode);
        Settings.Instance.GameSortMode = gameSortMode;
    }
}
