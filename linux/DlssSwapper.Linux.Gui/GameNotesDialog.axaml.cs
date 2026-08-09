using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DlssSwapper.Linux.Gui;

public sealed partial class GameNotesDialog : Window
{
    private readonly TextBox _notesTextBox;

    public GameNotesDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _notesTextBox = this.FindControl<TextBox>("NotesTextBox")
            ?? throw new InvalidOperationException("Notes control is missing.");
    }

    public GameNotesDialog(string gameName, string? notes)
        : this()
    {
        (this.FindControl<TextBlock>("GameNameText")
            ?? throw new InvalidOperationException("Game-name control is missing.")).Text = gameName;
        _notesTextBox.Text = notes;
    }

    private void Save_Click(object? sender, RoutedEventArgs e) =>
        Close(_notesTextBox.Text?.Trim());

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
