using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public partial class GameControlModel
{
    [RelayCommand]
    async Task ReloadGameAsync()
    {
        if (TryGetActionHost(out var actionHost))
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void ScanStateChanged(object? sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName == nameof(Game.Processing) && !Game.Processing)
                    completion.TrySetResult(!Game.NeedsProcessing);
            }
            var dialogStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var dialog = new EasyContentDialog(actionHost.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ReloadingGame"),
                Content = new ProgressRing()
                {
                    IsIndeterminate = true,
                },
                PrimaryButtonText = ResourceHelper.GetString("General_Close")
            };
            Game.PropertyChanged += ScanStateChanged;
            try
            {
                Game.NeedsProcessing = true;
                Game.ProcessGame(forceNeedsProcessing: true);
                // ProcessGame sets Processing synchronously on this UI thread;
                // missing/unavailable install paths return without starting.
                if (!Game.Processing) completion.TrySetResult(false);
                var dialogTask = dialog.ShowAsync().AsTask();
                await Task.WhenAny(dialogTask, completion.Task);
                if (dialogTask.IsCompleted)
                {
                    // Closing this dialog does not cancel the background scan.
                    Close();
                    return;
                }
                if (!await completion.Task)
                {
                    dialog.Hide();
                    await ShowPersistenceErrorAsync("GamePage_ReloadFailed");
                    return;
                }
                var loadingDuration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - dialogStart;

                if (loadingDuration < 1000)
                {
                    // Force loading dialog to exist for at least 1 second
                    await Task.Delay(1000 - (int)loadingDuration);
                }

                var reopenGameControl = TryGetGameControl(out _);
                Close();
                if (dialogTask.IsCompleted) return;
                dialog.Hide();
                if (reopenGameControl)
                {
                    var gameControl = new GameControl(Game);
                    _ = gameControl.ShowAsync();
                }
            }
            catch (Exception error)
            {
                Logger.Error(error);
                dialog.Hide();
                await ShowPersistenceErrorAsync("GamePage_ReloadFailed");
            }
            finally { Game.PropertyChanged -= ScanStateChanged; }
        }
    }
}

