using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public partial class GameControlModel
{
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
            var saveError = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

            var dialog = new EasyContentDialog(actionHost.XamlRoot)
            {
                Title = $"{ResourceHelper.GetString("GamePage_Notes")} - {Game.Title}",
                PrimaryButtonText = ResourceHelper.GetString("General_Save"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new StackPanel { Spacing = 8, Children = { textBox, saveError } },
            };
            dialog.Resources["ContentDialogMinWidth"] = 700;
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                var previous = Game.Notes;
                try
                {
                    Game.Notes = textBox.Text ?? string.Empty;
                    if (!await Game.SaveToDatabaseAsync(bypassBatch: true))
                    {
                        Game.Notes = previous;
                        args.Cancel = true;
                        saveError.Text = ResourceHelper.GetString("GamePage_MetadataSaveFailed");
                        saveError.Visibility = Visibility.Visible;
                    }
                }
                finally { deferral.Complete(); }
            };
            await dialog.ShowAsync();
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
                    if (!await Game.DeleteAsync())
                    {
                        await ShowPersistenceErrorAsync("GamePage_RemoveFailed");
                        return;
                    }
                    GameManager.Instance.RemoveGame(Game);
                }
                else
                {
                    var previous = Game.IsHidden;
                    Game.IsHidden = true;
                    if (!await Game.SaveToDatabaseAsync(bypassBatch: true))
                    {
                        Game.IsHidden = previous;
                        await ShowPersistenceErrorAsync("GamePage_MetadataSaveFailed");
                        return;
                    }
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
        var previous = Game.IsFavourite;
        Game.IsFavourite = !Game.IsFavourite;
        if (!await Game.SaveToDatabaseAsync(bypassBatch: true))
        {
            Game.IsFavourite = previous;
            await ShowPersistenceErrorAsync("GamePage_MetadataSaveFailed");
        }
    }

    [RelayCommand]
    async Task SaveTitleAsync()
    {
        var previous = Game.Title;
        Game.Title = GameTitle;
        if (!await Game.SaveToDatabaseAsync(bypassBatch: true))
        {
            Game.Title = previous;
            await ShowPersistenceErrorAsync("GamePage_MetadataSaveFailed");
        }
        OnPropertyChanged(nameof(GameTitleHasChanged));
    }

    [RelayCommand]
    async Task ShowHideGameAsync()
    {
        var previous = Game.IsHidden;
        if (Game.IsHidden is null)
        {
            Game.IsHidden = true;
        }
        else
        {
            Game.IsHidden = !Game.IsHidden;
        }
        if (!await Game.SaveToDatabaseAsync(bypassBatch: true))
        {
            Game.IsHidden = previous;
            await ShowPersistenceErrorAsync("GamePage_MetadataSaveFailed");
        }
    }
}
