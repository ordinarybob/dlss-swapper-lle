using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Builders;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Messages;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Windows.System;

namespace DLSS_Swapper.Pages;

public enum GameGridViewType
{
    GridView,
    ListView,
}

public partial class GameGridPageModel : ObservableObject
{
    GameGridPage gameGridPage;

    [ObservableProperty]
    public partial Game? SelectedGame { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(CanUseHeaderControls))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(CanApplyBatchDll))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBatchDllCommand))]
    public partial bool IsGameListLoading { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(CanApplyBatchDll))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBatchDllCommand))]
    public partial bool IsDLSSLoading { get; set; } = true;

    public bool IsLoading => (IsGameListLoading || IsDLSSLoading);

    public bool CanUseHeaderControls => IsGameListLoading == false && IsSelectionMode == false;

    public bool CanRefresh => IsLoading == false && IsSelectionMode == false;

    [ObservableProperty]
    public partial ICollectionView? CurrentCollectionView { get; set; } = null;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridViewItemHeight))]
    public partial int GridViewItemWidth { get; set; } = Settings.Instance.GridViewItemWidth;

    public int GridViewItemHeight => (int)(GridViewItemWidth * 1.5);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameGridViewIcon))]
    public partial GameGridViewType GameGridViewType { get; set; } = Settings.Instance.GameGridViewType;

    public FontIcon GameGridViewIcon => GameGridViewType switch
    {
        GameGridViewType.GridView => new FontIcon() { Glyph = "\xF0E2" },
        GameGridViewType.ListView => new FontIcon() { Glyph = "\xE8FD" },
        _ => new FontIcon() { },
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseHeaderControls))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    public partial bool IsSelectionMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyBatchDll))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBatchDllCommand))]
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

    bool AreAllVisibleGamesSelected =>
        gameGridPage.GetVisibleItemCount() > 0
        && gameGridPage.GetVisibleSelectedCount() == gameGridPage.GetVisibleItemCount();

    public string SelectVisibleButtonText => AreAllVisibleGamesSelected
        ? ResourceHelper.GetString("GamesPage_SelectionMode_DeselectAll")
        : ResourceHelper.GetString("GamesPage_SelectionMode_SelectVisible");

    public GameGridPageModelTranslationProperties TranslationProperties { get; } = new GameGridPageModelTranslationProperties();

    public GameGridPageModel(GameGridPage gameGridPage)
    {
        WeakReferenceMessenger.Default.Register<GameLibrariesStateChangedMessage>(this, async (sender, message) =>
        {
            GameManager.Instance.RemoveAllGames();
            await InitialLoadAsync();
        });

        this.gameGridPage = gameGridPage;
        ApplyGameGroupFilter();
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
    void ToggleSelectVisible()
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

        var selections = picker.ViewModel.PlannedDllActions;
        if (selections.Count == 0)
        {
            return;
        }

        IsBatchUpdateRunning = true;
        try
        {
            var results = await DllUpdateWorkflow.ApplyAsync(games, selections);
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
            }
        }

        NotifySelectionChanged();
    }

    void ExitSelectionMode()
    {
        gameGridPage.ExitSelectionMode();
        SelectedGames.Clear();
        IsSelectionMode = false;
        NotifySelectionChanged();
    }

    void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedGamesCountText));
        OnPropertyChanged(nameof(SelectVisibleButtonText));
        OnPropertyChanged(nameof(CanApplyBatchDll));
        ApplyBatchDllCommand.NotifyCanExecuteChanged();
    }

    public async Task InitialLoadAsync()
    {
        IsGameListLoading = true;
        IsDLSSLoading = true;

        await GameManager.Instance.LoadGamesFromCacheAsync();

        IsGameListLoading = false;

        await GameManager.Instance.LoadGamesAsync(false);

        IsDLSSLoading = false;
    }

    public void SearchForGameEvent(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            throw new ArgumentException("Sender must be a TextBox");
        }

        if (string.IsNullOrEmpty(textBox.Text))
        {
            CurrentCollectionView = GameManager.Instance.GetGameCollection();
            return;
        }
        CurrentCollectionView = GameManager.Instance.GetGameCollection(textBox.Text);
    }

    [RelayCommand]
    async Task AddManualGameButtonAsync()
    {
        if (Settings.Instance.DontShowManuallyAddingGamesNotice == false)
        {
            var dontShowAgainCheckbox = new CheckBox()
            {
                Content = new TextBlock()
                {
                    Text = ResourceHelper.GetString("General_DontShowAgain"),
                },
            };

            var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_NoteTitle"),
                PrimaryButtonText = ResourceHelper.GetString("GamesPage_AddGame"),
                SecondaryButtonText = ResourceHelper.GetString("General_ReportIssue"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new StackPanel()
                {
                    Children = {
                        new TextBlock()
                        {
                            TextWrapping = TextWrapping.Wrap,
                            Text = ResourceHelper.GetString("GamesPage_ManuallyAdding_NoteMessage"),
                        },
                        dontShowAgainCheckbox,
                    },
                    Orientation = Orientation.Vertical,
                    Spacing = 16,
                },
            };

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
            else if (result == ContentDialogResult.Secondary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/issues"));
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
        if (Settings.Instance.HasShownAddMultipleGameFoldersMessage == false)
        {
            var explanation = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_MultipleFoldersNoteTitle"),
                PrimaryButtonText = ResourceHelper.GetString("GamesPage_ManuallyAdding_SelectGameFolders"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetString("GamesPage_ManuallyAdding_MultipleFoldersDescription"),
            };
            if (await explanation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            Settings.Instance.HasShownAddMultipleGameFoldersMessage = true;
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
                await game.SaveToDatabaseAsync();
                game.ProcessGame();
                GameManager.Instance.AddGame(game);
                added++;
            }
            catch (Exception err)
            {
                Logger.Error(err, $"Could not add manual game folder \"{installPath}\".");
                if (game is not null)
                {
                    await game.DeleteAsync();
                    GameManager.Instance.RemoveGame(game);
                }
                failed.Add($"{installPath}: {err.Message}");
            }
        }

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
        TextBlockBuilder textBlockBuilder = new TextBlockBuilder(ResourceHelper.GetString("GamesPage_ManuallyAdding_InfoHtml"));

        if (Settings.Instance.HasShownAddGameFolderMessage == false)
        {
            var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_AnotherNoteTitle"),
                PrimaryButtonText = ResourceHelper.GetString("GamesPage_AddGame"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Primary,
                Content = textBlockBuilder.Build()
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
            {
                return;
            }

            Settings.Instance.HasShownAddGameFolderMessage = true;
        }

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
                    await game.SaveToDatabaseAsync();
                    game.ProcessGame();
                    GameManager.Instance.AddGame(game, true);
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
                PrimaryButtonText = ResourceHelper.GetString("General_ReportIssue"),
                DefaultButton = ContentDialogButton.Primary,
                Content = $"{ResourceHelper.GetString("GamesPage_ManuallyAdding_CouldntAddError")}\n\n{ResourceHelper.GetString("General_ErrorMessage")}: {err.Message}",
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/issues"));
            }
        }
    }

    [RelayCommand]
    async Task RefreshGamesButtonAsync()
    {
        IsDLSSLoading = true;

        await GameManager.Instance.LoadGamesAsync(true);

        IsDLSSLoading = false;
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

            foreach (var game in excludedGames)
            {
                game.IsHidden = null;
                await game.SaveToDatabaseAsync();
            }

            await GameManager.Instance.LoadGamesAsync(true);
        }
        finally
        {
            IsDLSSLoading = false;
        }
    }

    [RelayCommand]
    async Task FilterGamesButtonAsync()
    {
        var gameFilterControl = new GameFilterControl();

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("General_Filter"),
            PrimaryButtonText = ResourceHelper.GetString("General_Apply"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = gameFilterControl,
        };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (gameFilterControl.DataContext is GameFilterControlViewModel gameFilterControlViewModel)
            {
                Settings.Instance.HideNonDLSSGames = gameFilterControlViewModel.HideNonSwappableGames;
                GameManager.Instance.ShowHiddenGames = gameFilterControlViewModel.ShowHiddenGames;
                Settings.Instance.GroupGameLibrariesTogether = gameFilterControlViewModel.GroupGameLibrariesTogether;
            }

            ApplyGameGroupFilter();
        }

    }

    void ApplyGameGroupFilter()
    {
        // TODO: Remove weird hack which otherwise causes MainGridView_SelectionChanged to fire when changing MainGridView.ItemsSource.
        //gameGridPage.MainGridView.SelectionChanged -= MainGridView_SelectionChanged;

        //MainGridView.ItemsSource = null;
        CurrentCollectionView = null;
        CurrentCollectionView = GameManager.Instance.GetGameCollection();
    }

    [RelayCommand]
    async Task UnknownAssetsFoundButtonAsync()
    {
        var newDllsControl = new NewDLLsControl();

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_NewDllsFound"),
            CloseButtonText = ResourceHelper.GetString("General_Close"),
            Content = newDllsControl,
        };
        dialog.Resources["ContentDialogMinWidth"] = 700;
        dialog.Resources["ContentDialogMaxWidth"] = 700;
        await dialog.ShowAsync();
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
}
