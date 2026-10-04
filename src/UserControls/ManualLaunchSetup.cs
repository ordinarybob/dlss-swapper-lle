using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

internal static partial class ManualLaunchSetup
{
    internal static async Task OfferAsync(XamlRoot root, IReadOnlyList<ManuallyAddedGame> games)
    {
        if (games.Count == 0) return;
        var setup = Settings.Instance.SetupManualLaunchOnImport;
        if (!Settings.Instance.DontShowManualLaunchPrompt)
        {
            var remember = new CheckBox { Content = "Don't show this again" };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock { Text = "Do you want a manifest for launching these games?\n\nThe app will scan all added games, then show their suggested executables together for review.\n\nLaunch details are saved in this app's library; no game files are changed. No skips setup. Remembering Yes opens setup automatically on future imports.", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(remember);
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
            content.Children.Add(error);
            var prompt = new EasyContentDialog(root) { Title = "Set up game launching?", PrimaryButtonText = "Yes", CloseButtonText = "No", DefaultButton = ContentDialogButton.Close, Content = content };
            bool SaveChoice(bool choice)
            {
                if (TryRememberChoice(choice, remember.IsChecked == true)) return true;
                error.Text = ResourceHelper.GetString("GamePage_LaunchChoiceSaveFailed");
                return false;
            }
            prompt.PrimaryButtonClick += (_, e) => e.Cancel = !SaveChoice(true);
            prompt.CloseButtonClick += (_, e) => e.Cancel = !SaveChoice(false);
            setup = await prompt.ShowAsync() == ContentDialogResult.Primary;
        }
        if (setup) await ConfigureAsync(root, games);
    }

    internal static async Task ConfigureAsync(XamlRoot root, IReadOnlyList<ManuallyAddedGame> games)
    {
        if (games.Count == 0) return;
        var scans = await ScanAllAsync(root, games);
        if (scans is null) return;
        await new ManualLaunchSetupDialog(root, scans).ShowAsync();
    }

    static Task<List<ManualLaunchManifest.Candidate>> FindCandidatesAsync(ManuallyAddedGame game) =>
        Task.Run(() => ManualLaunchManifest.FindCandidates(game.InstallPath, game.Title,
            Data.GameAssetCandidatePathIndex.EnumerateCandidateDirectories(game.InstallPath),
            Data.Steam.SteamArtworkLookup.NormalizeTitle));

    static async Task<IReadOnlyList<LaunchScanEntry>?> ScanAllAsync(XamlRoot root, IReadOnlyList<ManuallyAddedGame> games)
    {
        using var cancellation = new CancellationTokenSource();
        var bar = new ProgressBar { Minimum = 0, Maximum = games.Count };
        var status = new TextBlock { Text = $"0/{games.Count} games scanned", TextWrapping = TextWrapping.Wrap };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(status);
        content.Children.Add(bar);
        var dialog = new EasyContentDialog(root) { Title = "Scanning game executables", Content = content, CloseButtonText = "Cancel" };
        dialog.CloseButtonClick += (_, _) => cancellation.Cancel();
        var shown = dialog.ShowAsync();
        var progress = new Progress<int>(completed =>
        {
            // Parallel completions can be posted out of order.
            bar.Value = Math.Max(bar.Value, completed);
            status.Text = $"{bar.Value:0}/{games.Count} games scanned";
        });
        try
        {
            var scans = await ScanAllDefaultsAsync(games, FindCandidatesAsync, progress, cancellation.Token);
            return cancellation.IsCancellationRequested ? null : scans;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return null; }
        finally
        {
            dialog.Hide();
            await shown;
        }
    }

}
