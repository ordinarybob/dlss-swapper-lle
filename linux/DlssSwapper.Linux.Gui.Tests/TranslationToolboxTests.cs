using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class TranslationToolboxTests
{
    public static void Run(Window owner)
    {
        var window = new TranslationToolboxWindow("en-US");
        window.Show(owner); window.Width = window.MinWidth; window.Height = window.MinHeight;
        Dispatcher.UIThread.RunJobs();
        try
        {
            foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(button => button.Content is string))
            {
                var origin = button.TranslatePoint(default, window) ?? throw new Exception("Detached toolbox action.");
                if (origin.Y < 0 || origin.Y + button.Bounds.Height > window.ClientSize.Height + 1)
                    throw new Exception("Toolbox action clipped at minimum height.");
            }
            Click(window, "Load Existing");
            Dispatcher.UIThread.RunJobs();
            window.GetVisualDescendants().OfType<ListBox>().Single().SelectedItem = "General_Cancel";
            Dispatcher.UIThread.RunJobs();
            var editor = window.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "TranslationEditor");
            if (editor.Text != "Cancel") throw new Exception("Existing translation was not populated.");
            editor.Text = "Fixture cancel";
            Click(window, "Reload app");
            if (LanguageAppearance.Get("General_Cancel", "") != "Fixture cancel")
                throw new Exception("Translation edits were not previewed.");
            window.Close(); Dispatcher.UIThread.RunJobs();
            var confirm = window.OwnedWindows.OfType<ConfirmationDialog>().Single();
            confirm.Close(false); Dispatcher.UIThread.RunJobs();
            if (!window.IsVisible || editor.Text != "Fixture cancel") throw new Exception("Cancelled close lost draft.");
            window.Close(); Dispatcher.UIThread.RunJobs();
            window.OwnedWindows.OfType<ConfirmationDialog>().Single().Close(true);
            Dispatcher.UIThread.RunJobs();
            if (window.IsVisible || LanguageAppearance.Get("General_Cancel", "") != "Cancel")
                throw new Exception("Closing toolbox retained preview or failed to close.");
            Console.WriteLine("PASS translation toolbox load/edit/preview/discard and fixed actions (not native Linux acceptance)");
        }
        finally
        {
            foreach (var dialog in window.OwnedWindows.OfType<ConfirmationDialog>().ToArray()) dialog.Close(true);
            if (window.IsVisible)
            {
                window.Close(); Dispatcher.UIThread.RunJobs();
                foreach (var dialog in window.OwnedWindows.OfType<ConfirmationDialog>().ToArray()) dialog.Close(true);
            }
            LanguageAppearance.Apply("en-US");
        }
    }

    private static void Click(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
