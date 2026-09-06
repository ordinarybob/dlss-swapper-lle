using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class AppLogTests
{
    public static void Run(Window owner, string root)
    {
        var stateRoot = Path.Combine(root, "application-log");
        AppLog.Configure(stateRoot, "Off");
        AppLog.Write(ApplicationLogLevel.Error, "must not appear");
        if (Directory.Exists(Path.Combine(stateRoot, "logs"))) throw new Exception("Off created log files.");
        AppLog.ChangeLevel("Warning");
        AppLog.Write(ApplicationLogLevel.Info, "filtered message");
        AppLog.Write(ApplicationLogLevel.Error, "recorded message");
        var text = File.ReadAllText(AppLog.CurrentPath!);
        if (!text.Contains("recorded message") || text.Contains("filtered message")) throw new Exception("Log threshold failed.");
        for (var index = 1; index <= 9; index++)
            File.WriteAllText(Path.Combine(stateRoot, "logs", $"dlss_swapper_{DateTime.Now.AddDays(-index):yyyyMMdd}.log"), "fixture");
        var unrelated = Path.Combine(stateRoot, "logs", "dlss_swapper_notes.log");
        File.WriteAllText(unrelated, "preserve");
        AppLog.Write(ApplicationLogLevel.Warning, "retention");
        if (Directory.GetFiles(Path.Combine(stateRoot, "logs"), "*.log").Length != 8 || File.ReadAllText(unrelated) != "preserve")
            throw new Exception("Retention did not retain seven dated logs and unrelated files.");
        var library = new PersistentLibrary(new LibraryStateStore(stateRoot));
        var window = new SettingsWindow(library); window.Show(owner);
        window.FindControl<ComboBox>("ApplicationLogLevelComboBox")!.SelectedItem = "Debug";
        Click(window, "Cancel");
        if (library.State.ApplicationLoggingLevel != "Error") throw new Exception("Cancelled log setting persisted.");
        window = new SettingsWindow(library); window.Show(owner);
        var logLevel = window.FindControl<ComboBox>("ApplicationLogLevelComboBox")!;
        LanguageAppearance.Apply("en-US", Enum.GetNames<ApplicationLogLevel>().ToDictionary(
            level => "SettingsPage_Logging_" + level, level => "Fixture " + level));
        foreach (var level in Enum.GetNames<ApplicationLogLevel>())
        {
            logLevel.SelectedItem = level; Dispatcher.UIThread.RunJobs();
            if (!logLevel.GetVisualDescendants().OfType<TextBlock>().Any(label => label.Text == "Fixture " + level))
                throw new Exception("Selected logging level ignored translation: " + level);
        }
        logLevel.SelectedItem = "Off";
        Click(window, "Save");
        LanguageAppearance.Apply("en-US");
        if (new PersistentLibrary(new LibraryStateStore(stateRoot)).State.ApplicationLoggingLevel != "Off"
            || AppLog.Level != ApplicationLogLevel.Off) throw new Exception("Saved log level not applied/persisted.");
        var prior = File.ReadAllText(AppLog.CurrentPath!);
        AppLog.Write(ApplicationLogLevel.Error, "disabled");
        if (File.ReadAllText(AppLog.CurrentPath!) != prior) throw new Exception("Off continued writing.");
        Console.WriteLine("PASS application logging threshold/off/retention/settings (not native log-opening acceptance)");
    }
    private static void Click(Window window, string label)
    {
        window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
