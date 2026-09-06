using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class DllRecordDetailsWindow : Window
{
    public DllRecordDetailsWindow(DllCatalogEntry entry)
    {
        Title = LanguageAppearance.Format("Linux_DllRecordDetailsWindow_57", "DLL information — {0}", entry.Version);
        Width = 680; Height = 520; MinWidth = 440; MinHeight = 300; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var grid = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        grid.Children.Add(new TextBox { Text = DllRecordDetails.Describe(entry, LanguageAppearance.Current), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap });
        var close = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Close") }; close.Click += (_, _) => Close(); Grid.SetRow(close, 1); grid.Children.Add(close); Content = grid;
    }
}
