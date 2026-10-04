using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private GameDetailsWindow? _gameDetails;
    private DllType? _detailsFamily;
    private Window GameDialogOwner => _gameDetails ?? (Window)this;

    private async void GameItem_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || e.Handled) return;
        if (e.Source is Control source && (source is ToggleButton || source.GetVisualAncestors().Any(control => control is ToggleButton))) return;
        if (sender is Control { DataContext: GameRowViewModel row })
        {
            e.Handled = true;
            await ActivateGameAsync(row);
        }
    }

    private async void GameItem_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key is not (Key.Enter or Key.Space) || e.Source is ToggleButton) return;
        if (sender is Control { DataContext: GameRowViewModel row })
        {
            e.Handled = true;
            await ActivateGameAsync(row);
        }
    }

    internal async Task ActivateGameAsync(GameRowViewModel row)
    {
        if (!_viewModel.CanInteract || _gameDetails is not null || !TryGetLibrary(out var library)) return;
        if (_viewModel.IsBatchMode) { row.IsSelected = !row.IsSelected; return; }
        var sender = new MenuItem { Tag = row };
        var manual = library.State.ManualGames.Any(game => PathComparer.Equals(game.RootPath, row.RootPath));
        _gameDetails = new GameDetailsWindow(row, manual, title =>
        {
            row.SetTitle(ManualGameTitle.Save(library, row.RootPath, title));
            ApplyGameView();
        }, async action =>
        {
            switch (action)
            {
                case GameDetailsAction.Launch: await GameLaunchAsync(sender); break;
                case GameDetailsAction.LaunchSetup: await GameLaunchSetupAsync(sender); break;
                case GameDetailsAction.Notes: await GameNotesAsync(sender); break;
                case GameDetailsAction.History: await GameHistoryAsync(sender); break;
                case GameDetailsAction.Favorite: GameFavorite_Click(sender, new RoutedEventArgs()); break;
                case GameDetailsAction.Reload: await GameReloadAsync(sender); break;
                case GameDetailsAction.Hide: GameHide_Click(sender, new RoutedEventArgs()); break;
                case GameDetailsAction.Cover: await GameCustomCoverAsync(sender); break;
                case GameDetailsAction.UpdateLatest: await GameUpdateLatestAsync(sender); break;
                case GameDetailsAction.Streamline: await OpenStreamlineAsync(sender); break;
                case GameDetailsAction.Restore: await GameRestoreDllsAsync(sender); break;
                case GameDetailsAction.ScanDetails: await new OperationReportWindow(row.Name, row.ScanDetail, true).ShowDialog(GameDialogOwner); break;
                case GameDetailsAction.Remove: await GameRemoveAsync(sender); break;
            }
        }, async family =>
        {
            _detailsFamily = family;
            try { await GameDllVersionsAsync(sender); }
            finally { _detailsFamily = null; }
        });
        try { await _gameDetails.ShowDialog(this); }
        finally { _gameDetails = null; }
    }
}
