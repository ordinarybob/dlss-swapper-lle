using Avalonia.Interactivity;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private async void GameLaunchSetup_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy || !TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library)) return;
        var game = library.State.ManualGames.FirstOrDefault(item => PathComparer.Equals(item.RootPath, row.RootPath));
        try
        {
            if (game is not null) { await ManualLaunchSetupWindow.ConfigureAsync(this, library, [game]); return; }
            var choices = row.Game.ProviderLaunchChoices.Where(launch => launch.Launcher == ProviderLauncher.Wine).ToArray();
            if (choices.Length == 0)
            { _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage26", "This game's launch settings are managed by its launcher."); return; }
            var launch = choices.Length == 1 ? choices[0]
                : await new ProviderLaunchDialog(row.Name, choices, forSetup: true).ShowDialog<ProviderLaunch?>(this);
            if (launch is null) return;
            var runner = await new ProviderWineLaunchDialog(row.Name, launch,
                library.State.ProviderWineRunners.GetValueOrDefault(launch.ConfigurationDirectory), launchOnSave: false).ShowDialog<string?>(this);
            if (runner is null) return;
            library.UpdateState(state => state.ProviderWineRunners[launch.ConfigurationDirectory] = runner);
            _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage27", "Saved the Wine runner for this prefix. Nothing was launched.");
        }
        catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage28", "Could not configure launching: {0}", ex.Message); }
    }
}
