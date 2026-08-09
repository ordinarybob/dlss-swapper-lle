using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DlssSwapper.Linux.Gui;

public sealed partial class ConfirmationDialog : Window
{
    internal const string GameFileWriteWarning =
        "This operation writes game files. Close the game before continuing.";

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
            var warningBorder = this.FindControl<Border>("WarningBorder")
                ?? throw new InvalidOperationException("Confirmation warning border is missing.");
            var warningText = this.FindControl<TextBlock>("WarningText")
                ?? throw new InvalidOperationException("Confirmation warning control is missing.");
            warningText.Text = warning;
            warningBorder.IsVisible = true;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Continue_Click(object? sender, RoutedEventArgs e) => Close(true);
}
