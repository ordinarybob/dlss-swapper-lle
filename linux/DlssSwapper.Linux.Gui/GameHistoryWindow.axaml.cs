using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DlssSwapper.Linux.Cli.Core;
using System.Globalization;

namespace DlssSwapper.Linux.Gui;

public sealed partial class GameHistoryWindow : Window
{
    private readonly TextBlock _headingText;
    private readonly ItemsControl _historyItems;

    public GameHistoryWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _headingText = this.FindControl<TextBlock>("HeadingText")
            ?? throw new InvalidOperationException("History heading is missing.");
        _historyItems = this.FindControl<ItemsControl>("HistoryItems")
            ?? throw new InvalidOperationException("History list is missing.");
    }

    public GameHistoryWindow(string gameName, IReadOnlyList<GameHistoryState> history)
        : this()
    {
        _headingText.Text = $"History — {gameName}";
        _historyItems.ItemsSource = history.Select(item => new
            {
                EventTime = item.EventTimeUtc.ToLocalTime()
                    .ToString("g", CultureInfo.CurrentCulture),
                item.EventType,
                item.AssetType,
                item.Version,
                item.Detail,
            }).ToArray();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
