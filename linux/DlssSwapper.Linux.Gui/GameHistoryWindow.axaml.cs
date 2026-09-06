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
        _headingText.Text = LanguageAppearance.Format("Linux_GameHistoryWindow_85", "History — {0}", gameName);
        _historyItems.ItemsSource = history.Select(item => new
            {
                EventTime = item.EventTimeUtc.ToLocalTime()
                    .ToString("g", CultureInfo.GetCultureInfo(LanguageAppearance.Current?.Language ?? "en-US")),
                EventType = EventLabel(item.EventType),
                AssetType = item.AssetType == "Artwork" ? LanguageAppearance.Get("Linux_GuiRemainingArtwork", "Artwork") : item.AssetType,
                item.Version,
                item.Detail,
            }).ToArray();
    }

    internal static string EventLabel(string value) => value switch
    {
        "DLL updated" => LanguageAppearance.Get("Linux_HistoryDllUpdated", value),
        "DLL detected" => LanguageAppearance.Get("Linux_HistoryDllDetected", value),
        "DLL operation" => LanguageAppearance.Get("Linux_HistoryDllOperation", value),
        "DLL restore result" => LanguageAppearance.Get("Linux_HistoryDllRestore", value),
        "Batch update result" => LanguageAppearance.Get("Linux_HistoryBatchUpdate", value),
        "Cover changed" => LanguageAppearance.Get("Linux_HistoryCoverChanged", value),
        "Streamline update" => LanguageAppearance.Get("Linux_HistoryStreamlineUpdate", value),
        "Streamline restore" => LanguageAppearance.Get("Linux_HistoryStreamlineRestore", value),
        "Streamline recover" => LanguageAppearance.Get("Linux_HistoryStreamlineRecover", value),
        _ => value
    };

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
