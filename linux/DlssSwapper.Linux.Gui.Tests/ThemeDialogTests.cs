using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class ThemeDialogTests
{
    internal static void Run(Window owner, string root)
    {
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "theme-state")));
        library.UpdateState(state => state.CardSize = 7);
        var settings = new SettingsWindow(library); settings.Show(owner);
        RadioButton Choice(string name) => settings.GetVisualDescendants().OfType<RadioButton>().Single(button => Equals(button.Tag, name));
        Check(Choice("Dark").IsChecked == true && library.State.AppTheme is null, "Initialization saved a default preference");
        var other = new ConfirmationDialog("Theme fixture", "Read this before continuing.", "The warning must remain readable."); other.Show(owner);
        var warning = other.FindControl<Border>("WarningBorder")!;
        var message = other.FindControl<TextBlock>("MessageText")!;
        var cancel = other.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Cancel"));
        var proceed = other.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Continue"));
        try
        {
            var dark = ((ISolidColorBrush)settings.Background!).Color;
            var darkText = ((ISolidColorBrush)message.Foreground!).Color;
            var darkWarning = ((ISolidColorBrush)warning.Background!).Color;
            var darkButton = ((ISolidColorBrush)cancel.Background!).Color;
            var darkDanger = ((ISolidColorBrush)proceed.Background!).Color;
            var primary = settings.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save"));
            var darkAccent = ((ISolidColorBrush)primary.Background!).Color;
            Choice("Light").IsChecked = true; Dispatcher.UIThread.RunJobs();
            Check(Application.Current!.RequestedThemeVariant == ThemeVariant.Light, "Light choice not applied");
            Check(((ISolidColorBrush)settings.Background!).Color != dark && ((ISolidColorBrush)other.Background!).Color != dark, "Open windows retained dark colors");
            Check(((ISolidColorBrush)message.Foreground!).Color != darkText, "Dialog text retained dark-theme color");
            Check(((ISolidColorBrush)warning.Background!).Color != darkWarning, "Warning surface retained dark-theme color");
            Check(((ISolidColorBrush)cancel.Background!).Color != darkButton, "Ordinary action retained dark-theme color");
            Check(((ISolidColorBrush)primary.Background!).Color != darkAccent, "Primary action retained dark-theme color");
            Check(((ISolidColorBrush)proceed.Background!).Color == darkDanger && ((ISolidColorBrush)proceed.Foreground!).Color == Colors.White, "Danger action lost its fixed contrasting text and fill");
            Check(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.AppTheme == "Light", "Theme not persisted");
            using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Choice("Dark").IsChecked = true; Dispatcher.UIThread.RunJobs();
                Check(Choice("Light").IsChecked == true && Choice("Dark").IsChecked == false, "Failed save did not restore radio selection");
                Check(Application.Current.RequestedThemeVariant == ThemeVariant.Light && library.State.AppTheme == "Light", "Failed save changed theme");
                Check(settings.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Could not save theme:") == true), "Theme failure not displayed");
            }
            Choice("System").IsChecked = true; Dispatcher.UIThread.RunJobs();
            Check(Application.Current.RequestedThemeVariant == ThemeVariant.Default && library.State.AppTheme == "System", "System selection not retained");
            settings.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var reloaded = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
            Check(reloaded.State.AppTheme == "System" && reloaded.State.CardSize == 7, "Cancel undid immediate theme or altered unrelated state");
            settings = new SettingsWindow(reloaded); settings.Show(owner);
            Check(Choice("System").IsChecked == true, "Reopened settings lost System selection");
            Choice("Dark").IsChecked = true; Dispatcher.UIThread.RunJobs();
            Check(((ISolidColorBrush)settings.Background!).Color == dark && ((ISolidColorBrush)other.Background!).Color == dark, "Dark colors not restored");
            Check(((ISolidColorBrush)message.Foreground!).Color == darkText && ((ISolidColorBrush)warning.Background!).Color == darkWarning
                && ((ISolidColorBrush)cancel.Background!).Color == darkButton && ((ISolidColorBrush)proceed.Background!).Color == darkDanger, "Dialog colors did not return to dark values");
        }
        finally { settings.Close(); other.Close(); Application.Current!.RequestedThemeVariant = ThemeVariant.Dark; }
        Console.WriteLine("PASS headless theme live colors/persistence/failure rollback/reopen (not native System-theme acceptance)");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
