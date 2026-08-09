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

    public ConfirmationDialog(string title, string message, string? warning = null)
        : this()
    {
        Title = title;
        var messageText = this.FindControl<TextBlock>("MessageText")
            ?? throw new InvalidOperationException("Confirmation message control is missing.");
        messageText.Text = message;
        if (warning is not null)
        {
            var warningText = this.FindControl<TextBlock>("WarningText")
                ?? throw new InvalidOperationException("Confirmation warning control is missing.");
            warningText.Text = warning;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Continue_Click(object? sender, RoutedEventArgs e) => Close(true);
}
