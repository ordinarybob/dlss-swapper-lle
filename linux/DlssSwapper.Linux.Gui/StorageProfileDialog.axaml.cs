using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DlssSwapper.Linux.Gui;

public sealed partial class StorageProfileDialog : Window
{
    private readonly CheckBox _hddModeCheckBox;

    public StorageProfileDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _hddModeCheckBox = this.FindControl<CheckBox>("HddModeCheckBox")
            ?? throw new InvalidOperationException("Required HDD-mode control is missing.");
    }

    private void Apply_Click(object? sender, RoutedEventArgs e) =>
        Close(_hddModeCheckBox.IsChecked == true);
}
