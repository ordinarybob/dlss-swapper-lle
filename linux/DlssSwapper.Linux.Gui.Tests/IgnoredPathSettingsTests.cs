using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class IgnoredPathSettingsTests
{
    public static void Run(Window owner, string root)
    {
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "ignored-settings")));
        var path = Path.Combine(root, "excluded-games");
        var window = new SettingsWindow(library); window.Show(owner);
        window.FindControl<TextBox>("IgnoredPathsTextBox")!.Text = path;
        Click(window, "Cancel"); Dispatcher.UIThread.RunJobs();
        if (library.State.IgnoredPaths.Count != 0) throw new Exception("Cancel persisted ignored paths.");
        window = new SettingsWindow(library); window.Show(owner);
        foreach (var (controlName, key) in new[]
        {
            ("MediaWikiEndpointTextBox", "Linux_SettingsApiInvalid"),
            ("MediaWikiHostTextBox", "Linux_SettingsImageHostInvalid"),
            ("HeroicDirectoriesTextBox", "Linux_SettingsHeroicFoldersInvalid"),
            ("HeroicExecutableTextBox", "Linux_SettingsHeroicExeInvalid"),
            ("ProviderPrefixesTextBox", "Linux_SettingsProviderFoldersInvalid"),
            ("LegendaryDirectoriesTextBox", "Linux_SettingsProviderFoldersInvalid")
        })
        {
            var field = window.FindControl<TextBox>(controlName)!;
            var prior = field.Text;
            LanguageAppearance.Apply("en-US", new Dictionary<string, string> { [key] = "Fixture invalid setting" });
            field.Text = "invalid/path";
            Click(window, "Save"); Dispatcher.UIThread.RunJobs();
            if (!window.IsVisible || !window.GetVisualDescendants().OfType<TextBlock>()
                .Any(label => label.Text?.Contains("Fixture invalid setting") == true))
                throw new Exception("Invalid setting was saved or ignored translation: " + controlName);
            field.Text = prior;
        }
        LanguageAppearance.Apply("en-US");
        window.FindControl<TextBox>("IgnoredPathsTextBox")!.Text = "relative-path";
        Click(window, "Save"); Dispatcher.UIThread.RunJobs();
        if (!window.IsVisible || library.State.IgnoredPaths.Count != 0) throw new Exception("Invalid ignored path was saved.");
        window.FindControl<TextBox>("IgnoredPathsTextBox")!.Text = path;
        Click(window, "Save"); Dispatcher.UIThread.RunJobs();
        if (window.IsVisible || new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.IgnoredPaths.Single() != path)
            throw new Exception("Ignored path did not persist.");
        window = new SettingsWindow(library); window.Show(owner);
        if (window.FindControl<TextBox>("IgnoredPathsTextBox")!.Text != path) throw new Exception("Ignored path not shown on reopen.");
        window.FindControl<TextBox>("IgnoredPathsTextBox")!.Text = "";
        Click(window, "Save"); Dispatcher.UIThread.RunJobs();
        if (library.State.IgnoredPaths.Count != 0) throw new Exception("Removing ignored path failed.");
        Console.WriteLine("PASS ignored-path settings validation/save/cancel/reopen/removal (not native picker acceptance)");
    }
    private static void Click(Window window, string label) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
