using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.IO;
using Windows.System;
using DLSS_Swapper.Helpers;
using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Data.DLSS;
using System.ComponentModel;

namespace DLSS_Swapper.UserControls;

public partial class GameControlModel : ObservableObject
{
    readonly WeakReference<Control> actionHostWeakReference;
    readonly WeakReference<GameControl>? gameControlWeakReference;

    public Game Game { get; init; }

    public bool IsManuallyAdded => Game.GameLibrary == Interfaces.GameLibrary.ManuallyAdded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateDetectedDllsToLatestCommand))]
    public partial bool IsUpdatingDetectedDlls { get; set; }

    bool CanUpdateDetectedDllsToLatest => IsUpdatingDetectedDlls == false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameTitleHasChanged))]
    public partial string GameTitle { get; set; }

    [ObservableProperty]
    public partial PresetOption? SelectedDlssPreset { get; set; }

    [ObservableProperty]
    public partial PresetOption? SelectedDlssDPreset { get; set; }

    [ObservableProperty]
    public partial PresetOption? SelectedDlssGPreset { get; set; }

    public bool CanSelectDlssPreset { get; private set; }

    public bool CanSelectDlssDPreset { get; private set; }

    public bool CanSelectDlssGPreset { get; private set; }

    public List<PresetOption> DlssPresetOptions { get; } = new List<PresetOption>();

    public List<PresetOption> DlssDPresetOptions { get; } = new List<PresetOption>();

    public List<PresetOption> DlssGPresetOptions { get; } = new List<PresetOption>();

    PresetOption? _previousDlssPreset;
    PresetOption? _previousDlssDPreset;
    PresetOption? _previousDlssGPreset;

    public bool GameTitleHasChanged
    {
        get
        {
            if (IsManuallyAdded == false)
            {
                return false;
            }

            if (string.IsNullOrEmpty(GameTitle))
            {
                return false;
            }

            return GameTitle.Equals(Game.Title) == false;
        }
    }

    public GameControlModelTranslationProperties TranslationProperties { get; } = new GameControlModelTranslationProperties();

    public GameControlModel(GameControl gameControl, Game game)
        : this((Control)gameControl, game)
    {
    }

    public GameControlModel(Control actionHost, Game game) : base()
    {
        ArgumentNullException.ThrowIfNull(actionHost);
        ArgumentNullException.ThrowIfNull(game);
        actionHostWeakReference = new WeakReference<Control>(actionHost);
        if (actionHost is GameControl gameControl)
        {
            gameControlWeakReference = new WeakReference<GameControl>(gameControl);
        }
        Game = game;
        GameTitle = game.Title;


        // Make sure NVAPIHelper is supported and the game has DLSS.
        if (NVAPIHelper.Instance.IsSupported && game.CurrentDLSS is not null)
        {
            // Try load the DriverSettingProfile for the given game. If it is not found the game is not supported.

            var gameProfile = NVAPIHelper.Instance.FindGameProfile(game);
            if (gameProfile is not null)
            {
                var gameDLSSPresetResult = NVAPIHelper.Instance.GetGameDLSSPreset(game);
                if (gameDLSSPresetResult.Success)
                {
                    CanSelectDlssPreset = true;
                    game.DlssPreset = gameDLSSPresetResult.Result;
                    DlssPresetOptions.AddRange(NVAPIHelper.Instance.DlssPresetOptions);
                    if (game.DlssPreset is null)
                    {
                        // If it was never set, ensure it goes to default.
                        SelectedDlssPreset = DlssPresetOptions.FirstOrDefault(x => x.Value == 0);
                    }
                    else
                    {
                        SelectedDlssPreset = DlssPresetOptions.FirstOrDefault(x => x.Value == game.DlssPreset);
                    }


                    if (Game.CurrentDLSS_D is not null)
                    {
                        var gameDLSSDPresetResult = NVAPIHelper.Instance.GetGameDLSSDPreset(game);
                        if (gameDLSSDPresetResult.Success)
                        {
                            CanSelectDlssDPreset = true;

                            game.DlssDPreset = gameDLSSDPresetResult.Result;
                            DlssDPresetOptions.AddRange(NVAPIHelper.Instance.DlssDPresetOptions);
                            if (game.DlssDPreset is null)
                            {
                                // If it was never set, ensure it goes to default.
                                SelectedDlssDPreset = DlssDPresetOptions.FirstOrDefault(x => x.Value == 0);
                            }
                            else
                            {
                                SelectedDlssDPreset = DlssDPresetOptions.FirstOrDefault(x => x.Value == game.DlssDPreset);
                            }
                        }
                    }


                    if (Game.CurrentDLSS_G is not null)
                    {
                        var gameDLSSGPresetResult = NVAPIHelper.Instance.GetGameDLSSGPreset(game);
                        if (gameDLSSGPresetResult.Success)
                        {
                            CanSelectDlssGPreset = true;

                            game.DlssGPreset = gameDLSSGPresetResult.Result;
                            DlssGPresetOptions.AddRange(NVAPIHelper.Instance.DlssGPresetOptions);
                            if (game.DlssGPreset is null)
                            {
                                // If it was never set, ensure it goes to default.
                                SelectedDlssGPreset = DlssGPresetOptions.FirstOrDefault(x => x.Value == 0);
                            }
                            else
                            {
                                SelectedDlssGPreset = DlssGPresetOptions.FirstOrDefault(x => x.Value == game.DlssGPreset);
                            }
                        }
                    }
                }
            }
        }

        if (CanSelectDlssPreset == false)
        {
            var disabledPresetOption = new PresetOption(ResourceHelper.GetString("General_NotSupported"), 0);
            DlssPresetOptions.Add(disabledPresetOption);
            SelectedDlssPreset = disabledPresetOption;
        }

        if (CanSelectDlssDPreset == false)
        {
            var disabledPresetOption = new PresetOption(ResourceHelper.GetString("General_NotSupported"), 0);
            DlssDPresetOptions.Add(disabledPresetOption);
            SelectedDlssDPreset = disabledPresetOption;
        }

        if (CanSelectDlssGPreset == false)
        {
            var disabledPresetOption = new PresetOption(ResourceHelper.GetString("General_NotSupported"), 0);
            DlssGPresetOptions.Add(disabledPresetOption);
            SelectedDlssGPreset = disabledPresetOption;
        }
    }

    bool TryGetActionHost(out Control actionHost)
    {
        if (actionHostWeakReference.TryGetTarget(out var target))
        {
            actionHost = target;
            return true;
        }

        actionHost = null!;
        return false;
    }

    bool TryGetGameControl(out GameControl gameControl)
    {
        if (gameControlWeakReference is not null
            && gameControlWeakReference.TryGetTarget(out var target))
        {
            gameControl = target;
            return true;
        }

        gameControl = null!;
        return false;
    }

    partial void OnSelectedDlssPresetChanging(PresetOption? value)
    {
        _previousDlssPreset = SelectedDlssPreset;
    }

    partial void OnSelectedDlssDPresetChanging(PresetOption? value)
    {
        _previousDlssDPreset = SelectedDlssDPreset;
    }

    partial void OnSelectedDlssGPresetChanging(PresetOption? value)
    {
        _previousDlssGPreset = SelectedDlssGPreset;
    }


    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(SelectedDlssPreset))
        {
            if (CanSelectDlssPreset == true && SelectedDlssPreset is not null && SelectedDlssPreset.Value != Game.DlssPreset)
            {
                var result = NVAPIHelper.Instance.SetGameDLSSPreset(Game, SelectedDlssPreset.Value);
                if (result.Success == false)
                {
                    if (TryGetActionHost(out var actionHost))
                    {
                        actionHost.DispatcherQueue.TryEnqueue(() =>
                        {
                            SelectedDlssPreset = _previousDlssPreset;
                        });
                        _ = NVAPIHelper.Instance.DisplayNVAPIErrorAsync(actionHost.XamlRoot);
                    }
                }
            }
        }
        else if (e.PropertyName == nameof(SelectedDlssDPreset))
        {
            if (CanSelectDlssDPreset == true && SelectedDlssDPreset is not null && SelectedDlssDPreset.Value != Game.DlssDPreset)
            {
                var result = NVAPIHelper.Instance.SetGameDLSSDPreset(Game, SelectedDlssDPreset.Value);
                if (result.Success == false)
                {
                    if (TryGetActionHost(out var actionHost))
                    {
                        actionHost.DispatcherQueue.TryEnqueue(() =>
                        {
                            SelectedDlssDPreset = _previousDlssDPreset;
                        });
                        _ = NVAPIHelper.Instance.DisplayNVAPIErrorAsync(actionHost.XamlRoot);
                    }
                }
            }
        }
        else if (e.PropertyName == nameof(SelectedDlssGPreset))
        {
            if (CanSelectDlssGPreset == true && SelectedDlssGPreset is not null && SelectedDlssGPreset.Value != Game.DlssGPreset)
            {
                var result = NVAPIHelper.Instance.SetGameDLSSGPreset(Game, SelectedDlssGPreset.Value);
                if (result.Success == false)
                {
                    if (TryGetActionHost(out var actionHost))
                    {
                        actionHost.DispatcherQueue.TryEnqueue(() =>
                        {
                            SelectedDlssGPreset = _previousDlssGPreset;
                        });
                        _ = NVAPIHelper.Instance.DisplayNVAPIErrorAsync(actionHost.XamlRoot);
                    }
                }
            }
        }
    }

    [RelayCommand]
    async Task OpenInstallPathAsync()
    {
        try
        {
            if (Directory.Exists(Game.InstallPath))
            {
                FileSystemHelper.OpenFolderInExplorer(Game.InstallPath);
            }
            else
            {
                throw new Exception(ResourceHelper.GetFormattedResourceTemplate("GamePage_CouldNotFindGameInstallPathTemplate", Game.InstallPath));
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);

            if (TryGetActionHost(out var actionHost))
            {
                var dialog = new EasyContentDialog(actionHost.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    Content = err.Message,
                };
                await dialog.ShowAsync();
            }
        }
    }

    [RelayCommand]
    async Task LaunchAsync()
    {
        if (GameManager.Instance.CanLaunchGame(Game))
        {
            await GameManager.Instance.LaunchGameAsync(Game);
        }
        else
        {
            if (TryGetActionHost(out var actionHost))
            {
                var dialog = new EasyContentDialog(actionHost.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamePage_CantLaunchFromLibraryTemplate", Game.GameLibrary),
                };
                await dialog.ShowAsync();
            }
        }
    }

    [RelayCommand]
    async Task EditNotesAsync()
    {
        if (TryGetActionHost(out var actionHost))
        {
            var textBox = new TextBox()
            {
                MinHeight = 400,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
            };
            // This needs to be set after AcceptsReturn otherwise it will strip out the \r
            textBox.Text = Game.Notes;

            var dialog = new EasyContentDialog(actionHost.XamlRoot)
            {
                Title = $"{ResourceHelper.GetString("GamePage_Notes")} - {Game.Title}",
                PrimaryButtonText = ResourceHelper.GetString("General_Save"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = textBox,
            };
            dialog.Resources["ContentDialogMinWidth"] = 700;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Game.Notes = textBox.Text ?? string.Empty;
                await Game.SaveToDatabaseAsync();
            }
        }
    }

    [RelayCommand]
    async Task ViewHistoryAsync()
    {
        if (TryGetActionHost(out var actionHost))
        {
            using var historyControl = new GameHistoryControl(Game);
            await historyControl.LoadAsync();
            var dialog = new EasyContentDialog(actionHost.XamlRoot)
            {
                Title = $"{ResourceHelper.GetFormattedResourceTemplate("GamePage_History")} - {Game.Title}",
                PrimaryButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Primary,
                Content = historyControl,
            };
            dialog.Resources["ContentDialogMinWidth"] = 0d;
            dialog.Resources["ContentDialogMaxWidth"] = 800d;

            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task AddCoverImageAsync()
    {
        if (Game.CoverImage == Game.ExpectedCustomCoverImage)
        {
            await Game.PromptToRemoveCustomCover();
            return;
        }

        Game.PromptToBrowseCustomCover();
    }

    [RelayCommand(CanExecute = nameof(CanUpdateDetectedDllsToLatest))]
    async Task UpdateDetectedDllsToLatestAsync()
    {
        if (TryGetActionHost(out var actionHost) == false)
        {
            return;
        }

        var selections = DllUpdateWorkflow.GetLatestSelections([Game]);
        if (selections.Count == 0)
        {
            var noActionsDialog = new EasyContentDialog(actionHost.XamlRoot)
            {
                Title = TranslationProperties.UpdateDetectedDllsText,
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetString("GamePage_UpdateDetectedDlls_NoEligibleActions"),
            };
            await noActionsDialog.ShowAsync();
            return;
        }

        var confirmationContent = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = ResourceHelper.GetFormattedResourceTemplate(
                        "GamePage_UpdateDetectedDlls_ConfirmTemplate",
                        selections.Count,
                        Game.Title),
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = ResourceHelper.GetString("GamePage_UpdateDetectedDlls_PresetsUnchanged"),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        var confirmationDialog = new EasyContentDialog(actionHost.XamlRoot)
        {
            Title = TranslationProperties.UpdateDetectedDllsText,
            PrimaryButtonText = ResourceHelper.GetString("General_Update"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = confirmationContent,
        };
        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        List<BatchSwapResult> results;
        IsUpdatingDetectedDlls = true;
        actionHost.IsEnabled = false;
        try
        {
            results = await DllUpdateWorkflow.ApplyAsync([Game], selections);
        }
        finally
        {
            actionHost.IsEnabled = true;
            IsUpdatingDetectedDlls = false;
        }

        var summaryDialog = new EasyContentDialog(actionHost.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_Batch_Summary_Title"),
            CloseButtonText = ResourceHelper.GetString("General_Close"),
            DefaultButton = ContentDialogButton.Close,
            Content = new BatchSwapSummaryControl(results),
        };
        await summaryDialog.ShowAsync();
    }

    [RelayCommand]
    void Close()
    {
        if (TryGetGameControl(out var gameControl))
        {
            gameControl.Hide();
        }
    }

    [RelayCommand]
    async Task RemoveAsync()
    {
        if (TryGetActionHost(out var actionHost))
        {
            // This needs to be set after AcceptsReturn otherwise it will strip out the \r
            var dialog = new EasyContentDialog(actionHost.XamlRoot)
            {
                Title = $"{ResourceHelper.GetString("General_Remove")} {Game.Title}?",
                PrimaryButtonText = ResourceHelper.GetString("General_Remove"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetFormattedResourceTemplate(
                    IsManuallyAdded
                        ? "GamePage_ManuallyAdded_RemoveGameTemplate"
                        : "GamePage_Discovered_RemoveGameTemplate",
                    Game.Title),
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (IsManuallyAdded)
                {
                    await Game.DeleteAsync();
                    GameManager.Instance.RemoveGame(Game);
                }
                else
                {
                    Game.IsHidden = true;
                    await Game.SaveToDatabaseAsync();
                }
                if (TryGetGameControl(out var gameControl))
                {
                    gameControl.Hide();
                }
            }
        }
    }

    [RelayCommand]
    async Task FavouriteAsync()
    {
        Game.IsFavourite = !Game.IsFavourite;
        await Game.SaveToDatabaseAsync();
    }

    [RelayCommand]
    async Task ChangeRecordAsync(GameAssetType gameAssetType)
    {
        if (TryGetGameControl(out var gameControl))
        {
            var currentAssets = Game.GameAssets
                .Where(asset => asset.AssetType == gameAssetType)
                .ToList();
            await Game.EnsureAssetHashesAsync(currentAssets);

            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = ResourceHelper.GetFormattedResourceTemplate("GamePage_SelectDllTemplateTitle", DLLManager.Instance.GetAssetTypeName(gameAssetType)),
                PrimaryButtonText = ResourceHelper.GetString("General_Swap"),
                IsPrimaryButtonEnabled = false,
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };

            var dllPickerControl = new DLLPickerControl(gameControl, dialog, Game, gameAssetType);
            dialog.Content = dllPickerControl;
            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task SaveTitleAsync()
    {
        Game.Title = GameTitle;
        await Game.SaveToDatabaseAsync();
        OnPropertyChanged(nameof(GameTitleHasChanged));
    }

    [RelayCommand]
    async Task MultipleDLLsFoundAsync(GameAssetType gameAssetType)
    {
        if (TryGetGameControl(out var gameControl))
        {
            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = ResourceHelper.GetFormattedResourceTemplate("GamePage_MultipleDllsFoundTemplate", DLLManager.Instance.GetAssetTypeName(gameAssetType)),
                PrimaryButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new MultipleDLLsFoundControl(Game, gameAssetType),
            };

            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task ReadyToPlayStateMoreInformationAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/wiki/Troubleshooting#game-is-not-in-a-ready-to-play-state"));
    }

    [RelayCommand]
    async Task DLSSPresetInfoAsync()
    {
        if (TryGetGameControl(out var gameControl))
        {
            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamePage_DLSSPresetInfo_Title"),
                PrimaryButtonText = ResourceHelper.GetString("General_Okay"),
                SecondaryButtonText = ResourceHelper.GetString("GamePage_DLSSPresetInfo_OnScreenIndicator"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetString("GamePage_DLSSPresetInfo_Message"),
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/wiki/DLSS-Developer-Options#on-screen-indicator"));
            }
        }
    }

    TaskCompletionSource? _reloadGameTaskCompletionSource;

    [RelayCommand]
    async Task ReloadGameAsync()
    {
        if (_reloadGameTaskCompletionSource is not null)
        {
            _reloadGameTaskCompletionSource.SetCanceled();
        }

        if (TryGetActionHost(out _))
        {
            _reloadGameTaskCompletionSource = new TaskCompletionSource();

            Game.PropertyChanged += Game_PropertyChanged;
            Game.NeedsProcessing = true;
            Game.ProcessGame(forceNeedsProcessing: true);

            var dialogStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var dialog = new EasyContentDialog(App.CurrentApp.MainWindow.Content.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ReloadingGame"),
                Content = new ProgressRing()
                {
                    IsIndeterminate = true,
                },
                PrimaryButtonText = ResourceHelper.GetString("General_Cancel")
            };
            var dialogTask = dialog.ShowAsync().AsTask();

            await Task.WhenAny(dialogTask, _reloadGameTaskCompletionSource.Task);

            Game.PropertyChanged -= Game_PropertyChanged;

            var reopenGameControl = TryGetGameControl(out _);

            if (dialogTask.IsCompleted)
            {
                // User clicked cancel, close the current dialog.
                Close();
            }
            else
            {
                var loadingDuration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - dialogStart;

                if (loadingDuration < 1000)
                {
                    // Force loading dialog to exist for at least 1 second
                    await Task.Delay(1000 - (int)loadingDuration);
                }

                Close();

                if (dialogTask.IsCompleted == true)
                {
                    return;
                }

                // Game finished reloading so re-launch the GameControl.
                _reloadGameTaskCompletionSource = null;
                dialog.Hide();
                if (reopenGameControl)
                {
                    var gameControl = new GameControl(Game);
                    _ = gameControl.ShowAsync();
                }
            }
        }
    }

    private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game.Processing))
        {
            if (Game.Processing == true)
            {
                _reloadGameTaskCompletionSource?.SetResult();
            }
        }
    }

    [RelayCommand]
    async Task ShowHideGameAsync()
    {
        if (Game.IsHidden is null)
        {
            Game.IsHidden = true;
        }
        else
        {
            Game.IsHidden = !Game.IsHidden;
        }
        await Game.SaveToDatabaseAsync();
    }

    [RelayCommand]
    async Task NVAPIErrorAsync()
    {
        if (TryGetActionHost(out var actionHost))
        {
            await NVAPIHelper.Instance.DisplayNVAPIErrorAsync(actionHost.XamlRoot);
        }
    }
}
