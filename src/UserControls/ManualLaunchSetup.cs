using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

internal static class ManualLaunchSetup
{
    internal static async Task OfferAsync(XamlRoot root, IReadOnlyList<ManuallyAddedGame> games)
    {
        if (games.Count == 0) return;
        var setup = Settings.Instance.SetupManualLaunchOnImport;
        if (!Settings.Instance.DontShowManualLaunchPrompt)
        {
            var remember = new CheckBox { Content = "Don't show this again" };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock { Text = "Do you want a manifest for launching these games?\n\nThe app will scan each game folder and make a best-effort selection of the correct executable. You must verify each suggestion before saving; the app cannot guarantee it is the right one. You can change the selection or browse for another file.\n\nLaunch details are saved in this app's library; no game files are changed. No skips setup. Remembering Yes opens setup automatically on future imports.", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(remember);
            var prompt = new EasyContentDialog(root) { Title = "Set up game launching?", PrimaryButtonText = "Yes", CloseButtonText = "No", DefaultButton = ContentDialogButton.Close, Content = content };
            setup = await prompt.ShowAsync() == ContentDialogResult.Primary;
            if (remember.IsChecked == true)
            {
                Settings.Instance.DontShowManualLaunchPrompt = true;
                Settings.Instance.SetupManualLaunchOnImport = setup;
                Settings.Instance.SaveJson();
            }
        }
        if (setup) await ConfigureAsync(root, games);
    }

    internal static async Task ConfigureAsync(XamlRoot root, IReadOnlyList<ManuallyAddedGame> games)
    {
        for (var index = 0; index < games.Count; index++)
        {
            var game = games[index];
            var content = new StackPanel { Spacing = 12 };
            var status = new TextBlock { Text = "Looking for executables…", TextWrapping = TextWrapping.Wrap };
            var candidates = new ComboBox { Header = "Executables in this game folder", HorizontalAlignment = HorizontalAlignment.Stretch, DisplayMemberPath = "Label" };
            var executable = new TextBox { Header = "Executable", Text = game.LaunchExecutable ?? "", PlaceholderText = "Choose a suggestion or browse for the game's .exe" };
            if (ManualLaunchManifest.IsExcluded(executable.Text)) executable.Text = "";
            var arguments = new TextBox { Header = "Launch arguments (optional)", Text = game.LaunchArguments ?? "" };
            var working = new TextBox { Header = "Working folder (optional)", Text = game.LaunchWorkingDirectory ?? "", PlaceholderText = "Defaults to the executable's folder" };
            var browse = new Button { Content = "Browse…" };
            content.Children.Add(new TextBlock { Text = "Confirm the executable used to start this game—not an installer, uninstaller or crash reporter. Saving does not launch it.", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(status);
            content.Children.Add(candidates);
            content.Children.Add(executable);
            content.Children.Add(browse);
            var advanced = new StackPanel { Spacing = 12 };
            advanced.Children.Add(arguments);
            advanced.Children.Add(working);
            content.Children.Add(new Expander { Header = "Launch options", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
            var dialog = new EasyContentDialog(root) { Title = $"Launch setup ({index + 1}/{games.Count}) — {game.Title}", PrimaryButtonText = index + 1 == games.Count ? "Save" : "Save and next", SecondaryButtonText = "Skip game", CloseButtonText = "Finish later", DefaultButton = ContentDialogButton.Primary, Content = new ScrollViewer { MaxHeight = 440, Content = content } };
            candidates.SelectionChanged += (_, _) => { if (candidates.SelectedItem is ManualLaunchManifest.Candidate selected) executable.Text = selected.Path; };
            browse.Click += (_, _) =>
            {
                try
                {
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);
                    var path = FileSystemHelper.OpenFile(handle, new[] { new FileSystemHelper.FileFilter("Game executable", "*.exe") }, defaultPath: game.InstallPath);
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (ManualLaunchManifest.IsExcluded(path)) status.Text = "That executable is excluded from game launching. Choose another.";
                        else executable.Text = path;
                    }
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            // Populate before displaying this step; never replace a saved choice.
            try
            {
                var found = await Task.Run(() => ManualLaunchManifest.FindCandidates(
                    game.InstallPath, game.Title,
                    Data.GameAssetCandidatePathIndex.EnumerateCandidateDirectories(game.InstallPath),
                    Data.Steam.SteamArtworkLookup.NormalizeTitle));
                candidates.ItemsSource = found;
                var saved = found.FindIndex(item => string.Equals(item.Path, executable.Text, StringComparison.OrdinalIgnoreCase));
                if (saved >= 0) candidates.SelectedIndex = saved;
                else if (string.IsNullOrWhiteSpace(executable.Text) && found.Count > 0) candidates.SelectedIndex = 0;
                status.Text = !string.IsNullOrWhiteSpace(game.LaunchExecutable) && !ManualLaunchManifest.IsExcluded(game.LaunchExecutable)
                    ? "Your saved executable is selected. Verify it before saving changes."
                    : found.Count == 0 ? "No suggested executable found. Use Browse to choose one."
                    : "Best-effort suggestion selected. Verify it is the correct game executable before saving.";
            }
            catch (Exception ex) { status.Text = $"Could not scan this folder. Use Browse. {ex.Message}"; }
            dialog.PrimaryButtonClick += async (_, e) =>
            {
                var deferral = e.GetDeferral();
                var old = (game.LaunchExecutable, game.LaunchArguments, game.LaunchWorkingDirectory);
                try
                {
                    var manifest = ManualLaunchManifest.Validate(executable.Text, arguments.Text, working.Text);
                    game.LaunchExecutable = manifest.Executable;
                    game.LaunchArguments = manifest.Arguments;
                    game.LaunchWorkingDirectory = manifest.WorkingDirectory;
                    if (!await game.SaveToDatabaseAsync(bypassBatch: true))
                        throw new IOException("Launch details could not be saved. Please try again.");
                }
                catch (Exception ex)
                {
                    (game.LaunchExecutable, game.LaunchArguments, game.LaunchWorkingDirectory) = old;
                    status.Text = ex.Message;
                    e.Cancel = true;
                }
                finally { deferral.Complete(); }
            };
            if (await dialog.ShowAsync() == ContentDialogResult.None) return;
        }
    }

}
