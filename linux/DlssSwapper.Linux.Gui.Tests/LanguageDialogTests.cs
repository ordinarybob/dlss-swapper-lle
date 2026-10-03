using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class LanguageDialogTests
{
    public static void Run(Window owner, PersistentLibrary library)
    {
        var settings = new SettingsWindow(library);
        var report = new OperationReportWindow("Fixture", []);
        var proxy = new ProxySettingsWindow(library);
        try
        {
            settings.Show(owner); report.Show(owner); Dispatcher.UIThread.RunJobs();
            var close = report.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Close"));
            var cancel = settings.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Cancel"));
            var libraryPanel = settings.FindControl<StackPanel>("LibrarySelectionPanel")!;
            var up = libraryPanel.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "Move up"));
            var down = libraryPanel.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "Move down"));
            var handle = libraryPanel.GetVisualDescendants().OfType<TextBlock>().First(label => label.Text == "☰");
            var unsupported = libraryPanel.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(box => !box.IsEnabled);
            LanguageAppearance.Apply("en-US", new Dictionary<string, string>
            {
                ["Linux_SettingsWindow_Libraries_178"] = "Fixture up",
                ["Linux_SettingsWindow_Libraries_177"] = "Fixture down",
                ["Linux_LibraryReorderHint"] = "Fixture reorder",
                ["Linux_LibraryUnsupportedHint"] = "Fixture unsupported"
            });
            Dispatcher.UIThread.RunJobs();
            if (!Equals(up.Content, "Fixture up") || !Equals(down.Content, "Fixture down")
                || !Equals(ToolTip.GetTip(handle), "Fixture reorder")
                || (unsupported is not null && !Equals(ToolTip.GetTip(unsupported), "Fixture unsupported")))
                throw new Exception("Retained library controls ignored live translation.");
            LanguageAppearance.Apply("ar-SA"); Dispatcher.UIThread.RunJobs();
            var arabic = new Translations("ar-SA");
            var themes = settings.GetVisualDescendants().OfType<RadioButton>().Where(button => button.GroupName == "AppTheme").ToArray();
            if (themes.Length != 3 || themes.Any(button => !Equals(button.Content,
                arabic.Get(Equals(button.Tag, "System") ? "SettingsPage_ThemeSystemSettingDefault" : "SettingsPage_Theme" + button.Tag, "missing"))))
                throw new Exception("Theme choices did not update their translated labels.");
            var storedTheme = library.State.AppTheme;
            var selectedTheme = themes.Single(button => button.IsChecked == true);
            if (!Equals(selectedTheme.Tag, storedTheme ?? "Dark"))
                throw new Exception("Translating theme labels changed the selected theme identity.");
            var headings = new DlssSwapper.Linux.Gui.ViewModels.MainWindowViewModel { IsLoadingLibrary = false, GameCount = 12, SelectedCount = 3 };
            if (headings.GamesHeading != arabic.Get("GamesPage_Title", "Games") + " (12)"
                || headings.SelectionHeading != arabic.Format("GamesPage_SelectionMode_CountTemplate", "{0} selected", 3))
                throw new Exception("Game and selection headings ignored the selected language.");
            if (!Equals(close.Content, arabic.Get("General_Close", "")) || !Equals(cancel.Content, arabic.Get("General_Cancel", ""))
                || report.FlowDirection != FlowDirection.RightToLeft || settings.FlowDirection != FlowDirection.RightToLeft)
                throw new Exception("Open XAML/generated controls did not update language and RTL.");
            LanguageAppearance.Apply("he-IL"); Dispatcher.UIThread.RunJobs();
            var hebrew = new Translations("he-IL");
            if (hebrew.Language != "he-IL" || !hebrew.RightToLeft
                || !Equals(close.Content, hebrew.Get("General_Close", ""))
                || !Equals(cancel.Content, hebrew.Get("General_Cancel", ""))
                || report.FlowDirection != FlowDirection.RightToLeft || settings.FlowDirection != FlowDirection.RightToLeft)
                throw new Exception("Hebrew language selection, live controls or RTL layout failed.");
            CheckActions(settings, hebrew.Get("General_Cancel", "Cancel"));
            CheckActions(report, hebrew.Get("General_Close", "Close"));
            if (hebrew.Get("Linux_LibraryDownloadCancelled", "missing") != "Download cancelled"
                || hebrew.Get("ApplicationTitle", "missing") != "DLSS Swapper LLE")
                throw new Exception("Hebrew lost LLE branding or the English fallback.");
            LanguageAppearance.Apply("en-US"); Dispatcher.UIThread.RunJobs();
            if (!Equals(close.Content, "Close") || !Equals(cancel.Content, "Cancel") || settings.FlowDirection != FlowDirection.LeftToRight)
                throw new Exception("Returning to English did not restore open controls.");
            var english = new Translations("en-US");
            foreach (var name in DLSS_Swapper.Data.Streamline.StreamlineComponentSet.FileNames)
                if (StreamlineDisplay.Description(name.ToUpperInvariant()) != DLSS_Swapper.Data.Streamline.StreamlineComponentDescriptions.GetDescription(name)
                    || english.Get("Linux_StreamlineDescription_" + name[3..^4], "missing") != StreamlineDisplay.Description(name))
                    throw new Exception("Streamline description resource omitted or changed existing component information.");
            LanguageAppearance.Apply("en-US", new Dictionary<string, string> { ["Linux_StreamlineDescription_unknown"] = "Fixture unknown component" });
            if (StreamlineDisplay.Description("unrecognized.dll") != "Fixture unknown component")
                throw new Exception("Unknown Streamline description ignored translation.");
            LanguageAppearance.Apply("en-US");
            var streamlineLabels = english.Values.Where(pair => pair.Key.StartsWith("Linux_Streamline_", StringComparison.Ordinal)).ToArray();
            LanguageAppearance.Apply("en-US", streamlineLabels.ToDictionary(pair => pair.Key, pair => "Translated: " + pair.Value));
            if (streamlineLabels.Length != 17 || streamlineLabels.Any(pair => StreamlineDisplay.Text(pair.Value) != "Translated: " + pair.Value)
                || StreamlineDisplay.Text("2.12.0.0") != "2.12.0.0")
                throw new Exception("Streamline display labels ignored translation or changed a file version.");
            LanguageAppearance.Apply("en-US");
            foreach (var index in Enumerable.Range(1, 14))
                if (english.Get("Linux_GamesOperation" + index, "missing") == "missing")
                    throw new Exception("Games-operation resource is missing.");
            if (english.Format("Linux_GamesOperation9", "missing", 3, 1) != "Exact-version update finished: 3 succeeded, 1 failed.")
                throw new Exception("Exact-version summary lost its outcome counts.");
            if (english.Format("Linux_FastScanFinished", "missing", 1, "", 1.25) != "Fast Scan inspected 1 game in 1.25 seconds."
                || english.Format("Linux_ScanProgress", "missing", "Fast Scan", 1000, 2000) != "Fast Scan: 1,000 / 2,000 game roots processed…")
                throw new Exception("Scan resources lost count or duration formatting.");
            if (english.Format("Linux_ConfirmExactPrompt", "missing", "DLSS", "310.9", "12345678", 2, "s", 1, "")
                != "Apply DLSS 310.9 (12345678) to 2 detected DLL files in 1 selected game?"
                || !english.Get("Linux_RemovalNotice", "missing").EndsWith("Game files are not deleted."))
                throw new Exception("Confirmation resources lost version/count or removal-scope details.");
            foreach (var index in Enumerable.Range(1, 34))
                if (arabic.Get("Linux_GamesMessage" + index, "missing") != english.Get("Linux_GamesMessage" + index, "missing")
                    || english.Get("Linux_GamesMessage" + index, "missing") == "missing")
                    throw new Exception("Games status resource is missing its English fallback.");
            if (english.Format("Linux_GamesMessage11", "missing", "Fixture game", "fixture error") != "Could not launch Fixture game: fixture error")
                throw new Exception("Games launch error lost the game name or error detail.");
            if (english.Format("Linux_LaunchDispatched", "missing", "Fixture game", "Heroic") != "Sent a launch request for Fixture game to Heroic."
                || english.Format("Linux_RemoveCoverPrompt", "missing", "Fixture game") != "Remove the custom cover for Fixture game and return to ordinary artwork? Your source image will not be deleted.")
                throw new Exception("Games feedback lost its launcher or source-image preservation information.");
            if (english.Get("Linux_LibraryVerified", "missing") != "Downloaded and verified"
                || english.Format("Linux_LibraryTransferKnown", "missing", 512, 1024, 50) != "512 / 1,024 bytes (50%)"
                || arabic.Get("Linux_LibraryDownloadCancelled", "missing") != "Download cancelled")
                throw new Exception("Library transfer resources or English fallback are missing.");
            LanguageAppearance.Apply("en-US", new Dictionary<string, string>
            {
                ["Linux_LibraryDownloadFailed"] = "Failure detail: {0}",
                ["Linux_GameWriteWarning"] = "Fixture write warning",
                ["Linux_BatchConfirmCount"] = "Games {1}; files {0}"
            });
            if (LanguageAppearance.Format("Linux_LibraryDownloadFailed", "ignored", "fixture error") != "Failure detail: fixture error")
                throw new Exception("Translated Library failure omitted the actual error.");
            if (ConfirmationDialog.GameFileWriteWarning != "Fixture write warning"
                || LanguageAppearance.Format("Linux_BatchConfirmCount", "ignored", 4, 2) != "Games 2; files 4")
                throw new Exception("Batch confirmation ignored translated warning or reordered counts.");
            LanguageAppearance.Apply("en-US");
            proxy.Show(owner);
            proxy.Width = proxy.MinWidth; proxy.Height = proxy.MinHeight;
            var priorProxy = library.State.Proxy;
            LanguageAppearance.Apply("en-US", new Dictionary<string, string>
            {
                ["ProxySettings_Server"] = "Fixture server",
                ["ProxySettings_Username"] = "Fixture username",
                ["ProxySettings_Password"] = "Fixture password",
                ["Linux_ProxyUsernameRequired"] = "Fixture username required"
            });
            Dispatcher.UIThread.RunJobs();
            foreach (var label in new[] { "Fixture server", "Fixture username", "Fixture password" })
                if (!proxy.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label))
                    throw new Exception("Proxy field ignored translated label: " + label);
            var inputs = proxy.GetVisualDescendants().OfType<TextBox>().ToArray();
            inputs.Single(input => input.PlaceholderText == "http://proxy.example:8080").Text = "http://proxy.example:8080";
            inputs.Single(input => input.PasswordChar == '●').Text = "fixture-not-a-real-password";
            inputs.Single(input => input.PlaceholderText is null && input.PasswordChar != '●').Text = "";
            proxy.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            if (!proxy.IsVisible || library.State.Proxy != priorProxy
                || !proxy.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Fixture username required"))
                throw new Exception("Proxy validation ignored translation or changed saved settings.");
            CheckActions(proxy, "Save", "Clear proxy and password", "Cancel");
            proxy.Close();
            LanguageAppearance.Apply("en-US");
            CheckNetworkLabels(owner);
            Console.WriteLine("PASS live XAML/generated translations and RTL reset (not native layout acceptance)");
        }
        finally { LanguageAppearance.Apply("en-US"); proxy.Close(); settings.Close(); report.Close(); }
    }

    private static void CheckNetworkLabels(Window owner)
    {
        var labels = NetworkTests.All.ToDictionary(test => $"NetworkTesterPage_DiagnosticsTest{test.Number}Title", test => $"Fixture test {test.Number}");
        labels["Linux_NetworkBrowserInstructions"] = "Fixture browser instructions: {0}";
        labels["Linux_NetworkBrowserCancelled"] = "Fixture browser cancelled";
        labels["Linux_NetworkTestsWindow_130"] = "A translation must not change protocol data";
        LanguageAppearance.Apply("en-US", labels);
        var window = new NetworkTestsWindow();
        try
        {
            window.Show(owner); Dispatcher.UIThread.RunJobs();
            window.Width = window.MinWidth; window.Height = window.MinHeight;
            var selection = window.GetVisualDescendants().OfType<ComboBox>().Single();
            selection.SelectedIndex = 9; Dispatcher.UIThread.RunJobs();
            var agent = window.GetVisualDescendants().OfType<TextBox>().Single(input => input.Name == "UserAgentTextBox");
            if (agent.Text != "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/133.0.0.0 Safari/537.36")
                throw new Exception("Translation changed the diagnostic User-Agent.");
            agent.Text = "Fixture-Agent/1.0";
            if (agent.IsReadOnly || !agent.IsEnabled || agent.Text != "Fixture-Agent/1.0")
                throw new Exception("Custom diagnostic User-Agent is no longer editable.");
            if (!selection.Items.Cast<string>().SequenceEqual(NetworkTests.All.Select(test => $"{test.Number}. Fixture test {test.Number}")))
                throw new Exception("Network test labels changed ordering or ignored translation.");
            selection.SelectedIndex = 3; Dispatcher.UIThread.RunJobs();
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Run test"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var report = window.GetVisualDescendants().OfType<TextBox>().Single(input => input.IsReadOnly);
            if (report.Text?.Contains("Fixture browser instructions: " + NetworkTests.All[3].Target) != true)
                throw new Exception("Browser instructions lost translation or their target.");
            report.Text += string.Concat(Enumerable.Repeat("\nLong diagnostic output fixture. ", 1000));
            CheckActions(window, "Run test", "Cancel test", "Copy results", "Close",
                "Open browser", "Copy link", "Download worked", "Download failed");
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Cancel test"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (report.Text?.Contains("Fixture browser cancelled") != true)
                throw new Exception("Browser cancellation ignored translation.");
        }
        finally { window.Close(); LanguageAppearance.Apply("en-US"); }
    }

    private static void CheckActions(Window window, params string[] labels)
    {
        Dispatcher.UIThread.RunJobs();
        foreach (var label in labels)
        {
            var button = window.GetVisualDescendants().OfType<Button>().Single(item => Equals(item.Content, label));
            var point = button.TranslatePoint(default, window)!.Value;
            if (!button.IsEffectivelyVisible || point.X < 0 || point.Y < 0
                || point.X + button.Bounds.Width > window.ClientSize.Width + 1
                || point.Y + button.Bounds.Height > window.ClientSize.Height + 1
                || button.GetVisualAncestors().OfType<ScrollViewer>().Any())
                throw new Exception("Dialog action is clipped or inside scrolling: " + label);
        }
    }
}
