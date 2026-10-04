using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private bool _loadingFilter;

    private void UpdateHeaderLayout()
    {
        var narrow = ClientSize.Width < 720;
        var toolbar = this.FindControl<StackPanel>("GameToolbar")!;
        Grid.SetRow(toolbar, narrow ? 2 : 0);
        Grid.SetRowSpan(toolbar, narrow ? 1 : 2);
        Grid.SetColumn(toolbar, narrow ? 0 : 1);
        Grid.SetColumnSpan(toolbar, narrow ? 2 : 1);
        Grid.SetColumnSpan(this.FindControl<Grid>("GameSearchHost")!, narrow ? 2 : 1);
        var strip = this.FindControl<Grid>("SelectionStrip")!;
        strip.RowDefinitions = new RowDefinitions("Auto,Auto");
        var actions = this.FindControl<Border>("SelectionCommandsHost")!;
        Grid.SetRow(actions, ClientSize.Width < 850 ? 1 : 0);
        Grid.SetColumn(actions, ClientSize.Width < 850 ? 0 : 1);
        Grid.SetColumnSpan(actions, ClientSize.Width < 850 ? 2 : 1);
    }

    private void FilterFlyout_Opening(object? sender, EventArgs e)
    {
        _loadingFilter = true;
        try
        {
            this.FindControl<CheckBox>("HideNonSwappableCheckBox")!.IsChecked = _library?.State.HideNonSwappableGames ?? true;
            this.FindControl<CheckBox>("ShowHiddenCheckBox")!.IsChecked = _includeHidden;
            this.FindControl<CheckBox>("GroupLibrariesCheckBox")!.IsChecked = _library?.State.GroupGameLibrariesTogether ?? true;
        }
        finally { _loadingFilter = false; }
    }

    private void FilterOptions_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loadingFilter || _library is null || !_opened) return;
        try
        {
            _library.UpdateState(state =>
            {
                state.HideNonSwappableGames = this.FindControl<CheckBox>("HideNonSwappableCheckBox")!.IsChecked == true;
                state.ShowHiddenGames = this.FindControl<CheckBox>("ShowHiddenCheckBox")!.IsChecked == true;
                state.GroupGameLibrariesTogether = this.FindControl<CheckBox>("GroupLibrariesCheckBox")!.IsChecked == true;
            });
            _includeHidden = _library.State.ShowHiddenGames;
            ApplyGameView();
        }
        catch (Exception error) { _viewModel.StatusText = error.Message; FilterFlyout_Opening(null, EventArgs.Empty); }
    }

    private async void RestoreAllExcluded_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy || !TryGetLibrary(out var library)) return;
        if (!await new ConfirmationDialog(
            LanguageAppearance.Get("GamesPage_Refresh_RestoreExcluded_Title", "Restore excluded launcher games?"),
            LanguageAppearance.Get("GamesPage_Refresh_RestoreExcluded_Description", "Restore previously excluded launcher games and refresh the library. Manually removed games must be added again."),
            "").ShowDialog<bool>(this)) return;
        try
        {
            library.UpdateState(state => { state.ExcludedSteamAppIds.Clear(); state.ExcludedProviderGames.Clear(); });
            await RefreshLibraryAsync(runInitialDeepScan: false);
        }
        catch (Exception error) { _viewModel.StatusText = error.Message; }
    }
}
