using Avalonia.Interactivity;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private async void GameTitle_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row) || !TryGetLibrary(out var library)) return;
        if (!library.State.ManualGames.Any(game => PathComparer.Equals(game.RootPath, row.RootPath)))
        {
            _viewModel.StatusText = LanguageAppearance.Get("Linux_GamesMessage29", "Titles from launchers cannot be edited. Only manually added games can be renamed.");
            return;
        }
        var title = await new GameTitleDialog(row.Name,
            draft => ManualGameTitle.Save(library, row.RootPath, draft)).ShowDialog<string?>(this);
        if (title is null) return;
        row.SetTitle(title);
        ApplyGameView();
        _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage30", "Saved title: {0}.", title);
    }
}
