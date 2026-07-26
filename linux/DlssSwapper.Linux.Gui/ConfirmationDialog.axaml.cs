using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DlssSwapper.Linux.Gui;

public sealed partial class ConfirmationDialog : Window
{
    public ConfirmationDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public ConfirmationDialog(string title, string message)
        : this()
    {
        Title = title;
        var messageText = this.FindControl<TextBlock>("MessageText")
            ?? throw new InvalidOperationException("Confirmation message control is missing.");
        messageText.Text = message;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Continue_Click(object? sender, RoutedEventArgs e) => Close(true);
}
