using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml.Controls;

internal static class ReloadTests
{
    public static async Task RunAsync()
    {
        void Check(bool value) { if (!value) throw new Exception("Reload lifecycle contract failed."); }
        async Task Run(bool success, bool dismiss)
        {
            var model = new GameControlModel();
            var dismissed = new TaskCompletionSource<ContentDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EasyContentDialog? progress = null;
            EasyContentDialog.Interact = dialog => { progress = dialog; return dismissed.Task; };
            GameControl.Opened = 0;
            var operation = model.ReloadGameCommand.ExecuteAsync(null);
            Check(model.Game.Processing && model.Game.ScanSubscribers == 1);
            Check(!operation.IsCompleted && progress is not null && !progress.Hidden && GameControl.Opened == 0);
            if (dismiss) dismissed.SetResult(ContentDialogResult.Primary);
            else
            {
                model.Game.NeedsProcessing = !success;
                model.Game.SetProcessing(false);
            }
            await operation.WaitAsync(TimeSpan.FromSeconds(5));
            Check(model.Game.ScanSubscribers == 0);
            if (dismiss)
                Check(model.Game.Processing && model.Control.Hidden && model.Errors.Count == 0 && GameControl.Opened == 0);
            else if (success)
                Check(model.Control.Hidden && progress!.Hidden && GameControl.Opened == 1 && model.Errors.Count == 0);
            else
                Check(!model.Control.Hidden && progress!.Hidden && GameControl.Opened == 0 && model.Errors.SequenceEqual(["GamePage_ReloadFailed"]));
        }
        try
        {
            await Run(success: true, dismiss: false);
            await Run(success: false, dismiss: false);
            await Run(success: true, dismiss: true);
            foreach (var throws in new[] { false, true })
            {
                var model = new GameControlModel();
                model.Game.StartScan = _ => { if (throws) throw new IOException("fixture start failure"); };
                EasyContentDialog.Interact = _ => new TaskCompletionSource<ContentDialogResult>().Task;
                await model.ReloadGameCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));
                Check(model.Game.ScanSubscribers == 0 && model.Errors.SequenceEqual(["GamePage_ReloadFailed"]));
            }
        }
        finally { EasyContentDialog.Interact = null; }
        Console.WriteLine("Reload command: waits for completion, handles failure/dismissal/no-start/exception and removes subscriptions (scan/dialog doubles).");
    }
}

// The native dialog returns IAsyncOperation; the fixture returns its Task directly.
namespace DLSS_Swapper.Helpers
{
    internal static class DialogTaskAdapter
    {
        internal static Task<T> AsTask<T>(this Task<T> task) => task;
    }
}
