using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class DiagnosticsTests
{
    internal static void Run(Window owner, string root)
    {
        var english = new Translations("en-US");
        Check(english.Get("Linux_GuiRemainingResetWarning", "") == "SteamLibrary-adjacent artwork is preserved. This cannot be undone."
            && english.Get("Linux_GuiRemainingImportParent", "").Contains("nested folders are not searched")
            && english.Format("Linux_GuiRemainingStartupRetry", "", "fixture failure").Contains("fixture failure")
            && english.Format("Linux_GuiRemainingDirectoryMissing", "", "/fixture/game").Contains("/fixture/game"),
            "Remaining GUI resources omitted preservation, import scope or error/path details.");
        try
        {
            LanguageAppearance.Apply("en-US", new Dictionary<string, string>
            {
                ["Linux_Diagnostic1"] = "Fixture assembly",
                ["Linux_Diagnostic21"] = "Fixture library unavailable",
                ["Linux_Diagnostic27"] = "Fixture unavailable",
                ["Linux_Diagnostic28"] = "Fixture failure: ",
                ["Linux_GuiRemainingDirectoryMissing"] = "{0}: fixture missing directory",
                ["Linux_HistoryDllUpdated"] = "Fixture updated"
            });
            var translatedIdentity = DiagnosticsReport.BuildIdentity();
            Check(translatedIdentity.Contains("Fixture assembly: " + typeof(MainWindow).Assembly.ManifestModule.ModuleVersionId)
                && DiagnosticsReport.Capture(null, null).Contains("Fixture library unavailable"),
                "Diagnostic labels ignored translation or lost actual identity.");
            var translatedFailure = DiagnosticsReport.Collect([("Fixture", () => throw new IOException("original detail")), ("Empty", () => null)]);
            Check(translatedFailure.Contains("Fixture failure: original detail") && translatedFailure.Contains("Empty: Fixture unavailable"),
                "Translated diagnostic failure hid original details or missing values.");
            Check(LanguageAppearance.Format("Linux_GuiRemainingDirectoryMissing", "", "/fixture/game") == "/fixture/game: fixture missing directory",
                "Remaining GUI message did not permit translated argument ordering.");
            Check(GameHistoryWindow.EventLabel("DLL updated") == "Fixture updated"
                && GameHistoryWindow.EventLabel("User supplied event") == "User supplied event",
                "History display ignored translation or rewrote an unrecognized stored event.");
        }
        finally { LanguageAppearance.Apply("en-US"); }
        var partial = DiagnosticsReport.Collect([("Before", () => "one"), ("Broken", () => throw new IOException("fixture")),
            ("After", () => "two")]);
        Check(partial.Contains("Before: one") && partial.Contains("Broken: Unavailable") && partial.Contains("After: two"),
            "One failed field discarded other diagnostic details");
        Check(DiagnosticsReport.Capture(null, null).Contains("library initialization did not complete"),
            "Startup failure required a library to produce diagnostics");
        Check(DiagnosticsReport.Capture(null, null).Contains("Package format: "
            + (DiagnosticsReport.BuildMetadata("LlePackageFormat") ?? "Not recorded by this build")),
            "Diagnostics package format did not match build metadata");
        Check(DiagnosticsReport.BuildMetadata("MissingFixtureKey") is null, "Missing build metadata was invented");
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "diagnostics-state")));
        var build = DiagnosticsReport.BuildIdentity();
        Check(build.Contains(typeof(MainWindow).Assembly.ManifestModule.ModuleVersionId.ToString())
            && build.Contains(typeof(MainWindow).Assembly.GetName().Version!.ToString()), "Build identity omitted actual assembly data.");
        var identitySettings = new SettingsWindow(library);
        string? copiedIdentity = null;
        identitySettings.CopyBuildIdentityAsync(text => { copiedIdentity = text; return Task.CompletedTask; }).GetAwaiter().GetResult();
        Check(copiedIdentity == build, "Build identity copy changed the displayed metadata.");
        identitySettings.CopyBuildIdentityAsync(_ => throw new IOException("Fixture clipboard failure")).GetAwaiter().GetResult();
        Check(identitySettings.FindControl<TextBlock>("ValidationTextBlock")!.Text!.Contains("Fixture clipboard failure"),
            "Build identity copy failure was hidden.");
        identitySettings.Close();
        library.UpdateState(state => state.LibrarySelection = [new() { Id = "GOG", IsEnabled = false }]);
        var before = File.ReadAllBytes(Path.Combine(library.StateDirectory, "state.json"));
        var report = DiagnosticsReport.Capture(library, [new("Fixture", Path.Combine(root, "not-created"), "42")]);
        Check(report.Contains("Steam: Enabled") && report.Contains("Games: 1") && report.Contains("GOG: Disabled"),
            "Library status or current count missing");
        Check(File.ReadAllBytes(Path.Combine(library.StateDirectory, "state.json")).SequenceEqual(before),
            "Diagnostics changed saved data");
        string? copied = null;
        var fail = true;
        var dialog = new DiagnosticsWindow(report + string.Concat(Enumerable.Repeat("\nLong report line", 200)), text =>
        {
            if (fail) throw new IOException("Clipboard unavailable");
            copied = text; return Task.CompletedTask;
        });
        var closed = dialog.ShowDialog(owner);
        dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
        Dispatcher.UIThread.RunJobs();
        var copy = dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Copy report"));
        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(dialog.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("Could not copy report") == true),
            "Clipboard failure was reported as success");
        fail = false;
        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(copied == dialog.GetVisualDescendants().OfType<TextBox>().Single().Text, "Copy did not match displayed report");
        Dispatcher.UIThread.RunJobs();
        foreach (var button in dialog.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Content is string text && text is "Copy report" or "Close"))
        {
            var position = button.TranslatePoint(default, dialog)!.Value;
            Check(position.Y >= 0 && position.Y + button.Bounds.Height <= dialog.ClientSize.Height + 1
                && !button.GetVisualAncestors().OfType<ScrollViewer>().Any(), "Diagnostic action hidden by report scrolling");
        }
        dialog.Close(); Check(closed.IsCompleted, "Diagnostics did not close");
        var reportsRequested = 0;
        var settings = new SettingsWindow(library, () => { reportsRequested++; return report; });
        var settingsClosed = settings.ShowDialog<bool>(owner);
        try
        {
            Click(settings, "Diagnostics");
            Dispatcher.UIThread.RunJobs();
            var settingsReport = settings.OwnedWindows.OfType<DiagnosticsWindow>().Single();
            Check(reportsRequested == 1 && settingsReport.GetVisualDescendants().OfType<TextBox>().Single().Text == report,
                "Settings diagnostics did not use the requested current report");
            Click(settingsReport, "Close");
            Click(settings, "Cancel");
            Check(settingsClosed.IsCompleted && !settingsClosed.Result, "Diagnostics changed Settings cancel behavior");
        }
        finally { settings.Close(); }
        var discoveryCalls = 0;
        var startup = new MainWindow(new MainWindowServices(
            () => new(null, null, "Fixture state cannot be read"),
            _ => { discoveryCalls++; throw new Exception("Unexpected discovery"); },
            (_, _) => { discoveryCalls++; throw new Exception("Unexpected provider discovery"); },
            () => null));
        try
        {
            startup.Show();
            Dispatcher.UIThread.RunJobs();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var diagnosticsAction = startup.GetVisualDescendants().OfType<Button>()
                    .Single(button => Equals(button.Content, "Diagnostics"));
                Check(diagnosticsAction.IsVisible && diagnosticsAction.IsEnabled, "Startup diagnostics action is unavailable");
                diagnosticsAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                var startupReport = startup.OwnedWindows.OfType<DiagnosticsWindow>().Single();
                Check(startupReport.GetVisualDescendants().OfType<TextBox>().Single().Text!
                    .Contains("library initialization did not complete"), "Startup diagnostics lost the unavailable-library state");
                Click(startupReport, "Close");
            }
            Check(discoveryCalls == 0, "Diagnostics triggered game discovery during failed startup");
        }
        finally { startup.Close(); Dispatcher.UIThread.RunJobs(); }
        Check(File.ReadAllBytes(Path.Combine(library.StateDirectory, "state.json")).SequenceEqual(before),
            "Opening report routes changed saved state");
        Console.WriteLine("PASS diagnostics partial fields/library snapshot/exact copy/failure/footer (not native Linux acceptance)");
    }
    private static void Click(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
