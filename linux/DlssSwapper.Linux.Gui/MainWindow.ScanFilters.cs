using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private bool _includeHidden;
    private void GroupLibraries_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetLibrary(out var library)) return;
        try
        {
            library.UpdateState(state => state.GroupGameLibrariesTogether = !state.GroupGameLibrariesTogether);
            ApplyGameView();
        }
        catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage31", "Could not save grouping: {0}", ex.Message); }
    }
    private void IncludeHidden_Click(object? sender, RoutedEventArgs e)
    {
        _includeHidden = !_includeHidden;
        if (sender is MenuItem item) item.Header = _includeHidden ? LanguageAppearance.Get("Linux_MainWindow_ScanFilters_99", "Exclude hidden games") : LanguageAppearance.Get("Linux_MainWindow_ScanFilters_98", "Include hidden games");
        ApplyGameView();
    }

    private void SwappableFilter_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetLibrary(out var library)) return;
        try
        {
            library.UpdateState(state => state.HideNonSwappableGames = !state.HideNonSwappableGames);
            ApplyGameView();
        }
        catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage32", "Could not save filter: {0}", ex.Message); }
    }

    private async void ScanDetails_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetMenuGame(sender, out var row)) return;
        var window = new Window { Title = LanguageAppearance.Format("Linux_MainWindow_ScanFilters_97", "Scan details — {0}", row.Name), Width = 640, Height = 420,
            MinWidth = 340, MinHeight = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var layout = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        layout.Children.Add(new TextBox { Text = $"{row.Name}\n{row.RootPath}\n\n{row.ScanSummary}\n\n{row.ScanDetail}",
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap });
        var close = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Close"), IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => window.Close();
        Grid.SetRow(close, 1); layout.Children.Add(close); window.Content = layout;
        await window.ShowDialog(this);
    }
}
