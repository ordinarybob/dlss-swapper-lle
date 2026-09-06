using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DlssSwapper.Linux.Gui;

public sealed partial class GameNotesDialog : Window
{
    private readonly TextBox _notesTextBox;
    private readonly Action<string>? _saveNotes;

    public GameNotesDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _notesTextBox = this.FindControl<TextBox>("NotesTextBox")
            ?? throw new InvalidOperationException("Notes control is missing.");
    }

    public GameNotesDialog(string gameName, string? notes, Action<string>? saveNotes = null)
        : this()
    {
        (this.FindControl<TextBlock>("GameNameText")
            ?? throw new InvalidOperationException(LanguageAppearance.Get("Linux_GameNotesDialog_87", "Game-name control is missing."))).Text = gameName;
        _notesTextBox.Text = notes;
        _saveNotes = saveNotes;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var notes = _notesTextBox.Text ?? string.Empty;
        try
        {
            _saveNotes?.Invoke(notes);
            Close(notes);
        }
        catch (Exception ex)
        { AppLog.Write(ApplicationLogLevel.Error, ex.Message);
            this.FindControl<TextBlock>("SaveError")!.Text = LanguageAppearance.Format("Linux_GameNotesDialog_86", "Could not save notes: {0} Your text is still here; retry or cancel.", ex.Message);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
