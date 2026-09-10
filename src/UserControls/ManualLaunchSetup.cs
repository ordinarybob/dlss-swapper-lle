using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            content.Children.Add(new TextBlock { Text = "Do you want a manifest for launching these games?\n\nThe app will suggest an executable for each game. Review them individually, or accept defaults for all games in one click. Suggestions are best-effort and may need correcting.\n\nLaunch details are saved in this app's library; no game files are changed. No skips setup. Remembering Yes opens setup automatically on future imports.", TextWrapping = TextWrapping.Wrap });
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
            var acceptDefaults = new Button { Content = "Accept default for all games", HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = games.Count > 1 ? Visibility.Visible : Visibility.Collapsed };
            content.Children.Add(acceptDefaults);
            if (games.Count > 1)
                content.Children.Add(new TextBlock { Text = "Uses the suggested executables for this setup. Keeps saved choices; skips games without a valid suggestion.", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(status);
            content.Children.Add(candidates);
            content.Children.Add(executable);
            content.Children.Add(browse);
            var advanced = new StackPanel { Spacing = 12 };
            advanced.Children.Add(arguments);
            advanced.Children.Add(working);
            content.Children.Add(new Expander { Header = "Launch options", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
            var dialog = new EasyContentDialog(root) { Title = $"Launch setup ({index + 1}/{games.Count}) — {game.Title}", PrimaryButtonText = index + 1 == games.Count ? "Save" : "Save and next", SecondaryButtonText = "Skip game", CloseButtonText = "Finish later", DefaultButton = ContentDialogButton.Primary, Content = new ScrollViewer { MaxHeight = 440, Content = content } };
            var useDefaults = false;
            var saving = false;
            acceptDefaults.Click += (_, _) =>
            {
                if (saving) return;
                useDefaults = true;
                dialog.Hide();
            };
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
                var found = await FindCandidatesAsync(game);
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
                saving = true;
                acceptDefaults.IsEnabled = false;
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
                finally
                {
                    saving = false;
                    acceptDefaults.IsEnabled = true;
                    deferral.Complete();
                }
            };
            var result = await dialog.ShowAsync();
            if (useDefaults)
            {
                await AcceptDefaultsAsync(root, games);
                return;
            }
            if (result == ContentDialogResult.None) return;
        }
    }

    static Task<List<ManualLaunchManifest.Candidate>> FindCandidatesAsync(ManuallyAddedGame game) =>
        Task.Run(() => ManualLaunchManifest.FindCandidates(game.InstallPath, game.Title,
            Data.GameAssetCandidatePathIndex.EnumerateCandidateDirectories(game.InstallPath),
            Data.Steam.SteamArtworkLookup.NormalizeTitle));

    static async Task AcceptDefaultsAsync(XamlRoot root, IReadOnlyList<ManuallyAddedGame> games)
    {
        using var cancellation = new CancellationTokenSource();
        var progress = new ProgressBar { Minimum = 0, Maximum = games.Count };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(status);
        content.Children.Add(progress);
        var dialog = new EasyContentDialog(root) { Title = "Saving launch defaults", Content = content, CloseButtonText = "Cancel" };
        dialog.CloseButtonClick += (_, _) => cancellation.Cancel();
        var shown = dialog.ShowAsync();
        BulkLaunchResult result;
        try
        {
            result = await SaveDefaultsAsync(games, FindCandidatesAsync, (index, title) =>
            {
                progress.Value = index;
                status.Text = $"{index + 1}/{games.Count} — {title}";
            }, cancellation.Token);
            progress.Value = games.Count;
        }
        finally
        {
            dialog.Hide();
            await shown;
        }
        var text = $"Saved: {result.Saved}\nKept existing choices: {result.Kept}\nSkipped: {result.Skipped.Count}";
        if (result.Cancelled) text += "\nStopped. Remaining games were not changed.";
        if (result.Skipped.Count > 0) text += "\n\n" + string.Join("\n", result.Skipped);
        await new EasyContentDialog(root)
        {
            Title = "Launch setup", CloseButtonText = "Okay",
            Content = new ScrollViewer { MaxHeight = 440, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } },
        }.ShowAsync();
    }
}
