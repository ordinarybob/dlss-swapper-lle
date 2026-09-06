using Avalonia.Controls;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private void UpdateSelectionSummary()
    {
        _viewModel.SelectedCount = _viewModel.Games.Count(row => row.IsSelected);
        if (this.FindControl<Button>("SelectAllButton") is { } button)
        {
            button.Content = _viewModel.Games.Count > 0 && !GameViewPolicy.ShouldSelectAll(_viewModel.Games.Select(row => row.IsSelected))
                ? LanguageAppearance.Get("GamesPage_SelectionMode_DeselectAll", "Deselect all") : LanguageAppearance.Get("GamesPage_SelectionMode_SelectAll", "Select all");
            button.IsEnabled = _viewModel.Games.Count > 0;
        }
    }
}
