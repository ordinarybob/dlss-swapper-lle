using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DLSS_Swapper.Data.ManuallyAdded;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class ManualLaunchSetupWindow : Window
{
    private readonly TextBox _executable = new(), _working = new(), _runner = new(), _prefix = new();
    private readonly TextBox _arguments = new() { AcceptsReturn = true, MinHeight = 70 };
    private readonly ComboBox _method = new() { ItemsSource = new[] { LanguageAppearance.Get("Linux_LaunchNative", "Native Linux executable"), LanguageAppearance.Get("Linux_LaunchThroughWine", "Windows executable through Wine") }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _candidates = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _status = Text(LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_123", "Looking for executables…"));
    private bool _closed;

    public static async Task OfferAsync(Window owner, PersistentLibrary library, IReadOnlyList<ManualGameState> games)
    {
        if (games.Count == 0) return;
        var setup = library.State.SetupManualLaunchOnImport;
        if (!library.State.DontShowManualLaunchPrompt)
        {
            var prompt = new Window { Title = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_122", "Set up game launching?"), Width = 600, Height = 370, MinWidth = 460, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var grid = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(Text(LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_121", "Do you want a manifest for launching these games?\n\nThe app will make a best-effort selection of the game's executable. Verify every suggestion before saving. For Windows games, choose your Wine executable and optional prefix. Saving changes only this app's library and does not launch the game.\n\nNo skips setup. Remembering Yes opens setup automatically on future imports.")));
            var remember = new CheckBox { Content = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_120", "Don't show this again") }; content.Children.Add(remember);
            var error = Text("");
            content.Children.Add(error);
            grid.Children.Add(new ScrollViewer { Content = content });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            var yes = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Yes"), MinWidth = 100 }; yes.Classes.Add("primary");
            var no = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_No"), MinWidth = 100 };
            void CompleteOffer(bool proceed)
            {
                try
                {
                    if (remember.IsChecked == true) ManualLaunchSetupWorkflow.RememberOffer(library, proceed);
                    prompt.Close(new ImportNoticeResult(proceed, remember.IsChecked == true));
                }
                catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); error.Text = LanguageAppearance.Format("Linux_ManualLaunchSetupWindow_119", "Could not save your preference: {0}. Try again, or clear Don't show this again to continue without saving it.", ex.Message); }
            }
            yes.Click += (_, _) => CompleteOffer(true);
            no.Click += (_, _) => CompleteOffer(false);
            actions.Children.Add(yes); actions.Children.Add(no); Grid.SetRow(actions, 1); grid.Children.Add(actions); prompt.Content = grid;
            var result = await prompt.ShowDialog<ImportNoticeResult?>(owner);
            if (result is null) return;
            setup = result.Proceed;
        }
        if (setup) await ConfigureAsync(owner, library, games);
    }

    public static async Task ConfigureAsync(Window owner, PersistentLibrary library, IReadOnlyList<ManualGameState> games)
    {
        for (var index = 0; index < games.Count; index++)
            if (!await new ManualLaunchSetupWindow(library, games[index], index, games.Count).ShowDialog<bool>(owner)) break;
    }

    private ManualLaunchSetupWindow(PersistentLibrary library, ManualGameState game, int index, int count)
    {
        Title = LanguageAppearance.Format("Linux_ManualLaunchSetupWindow_118", "Launch setup ({0}/{1}) — {2}", index + 1, count, game.Name);
        Width = 720; Height = 700; MinWidth = 520; MinHeight = 440; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
        layout.Children.Add(Text(LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_117", "Verify the suggested executable—not an installer, uninstaller or crash reporter. Saving does not launch it.")));
        var fields = new StackPanel { Spacing = 8 };
        AddField(fields, LanguageAppearance.Get("Linux_LaunchSuggestions", "Executable suggestions"), _candidates);
        AddField(fields, LanguageAppearance.Get("Linux_LaunchExecutable", "Executable"), _executable);
        var advanced = new StackPanel { Spacing = 8 };
        var options = new Expander { Header = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_116", "Launch options"), Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch };
        fields.Children.Add(options);
        AddField(advanced, LanguageAppearance.Get("Linux_LaunchMethod", "Launch method"), _method);
        AddField(advanced, LanguageAppearance.Get("Linux_LaunchWine", "Wine executable"), _runner);
        AddField(advanced, LanguageAppearance.Get("Linux_LaunchPrefix", "Wine prefix (optional)"), _prefix);
        AddField(advanced, LanguageAppearance.Get("Linux_LaunchWorkingFolder", "Working folder (optional)"), _working);
        AddField(advanced, LanguageAppearance.Get("Linux_LaunchArguments", "Arguments (one argument per line; do not add shell quoting)"), _arguments);
        var scroll = new ScrollViewer { Content = fields, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8 };
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 100 });
        var browse = new Button { Content = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_115", "Browse for executable…") };
        browse.Click += async (_, _) =>
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_114", "Choose the game executable"), AllowMultiple = false });
                if (!_closed && files.Count > 0) _executable.Text = files[0].Path.LocalPath;
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); if (!_closed) _status.Text = ex.Message; }
        };
        var browsing = new WrapPanel();
        browsing.Children.Add(browse);
        var browseWine = new Button { Content = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_113", "Browse for Wine…"), Margin = new Thickness(8, 0, 0, 0) };
        browseWine.Click += async (_, _) =>
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_112", "Choose the Wine executable"), AllowMultiple = false });
                if (!_closed && files.Count > 0) _runner.Text = files[0].Path.LocalPath;
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); if (!_closed) _status.Text = ex.Message; }
        };
        browsing.Children.Add(browseWine); footer.Children.Add(browsing);
        var actions = new WrapPanel();
        var save = new Button { Content = index + 1 == count ? LanguageAppearance.Get("General_Save", "Save") : LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_111", "Save and next"), Margin = new Thickness(0, 0, 8, 0) }; save.Classes.Add("primary");
        var skip = new Button { Content = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_110", "Skip game"), Margin = new Thickness(0, 0, 8, 0) };
        var finish = new Button { Content = LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_109", "Finish later") };
        save.Click += (_, _) =>
        {
            try
            {
                var launch = new ManualGameLaunch(_executable.Text ?? "", _working.Text ?? "",
                    string.IsNullOrEmpty(_arguments.Text) ? [] : _arguments.Text.Replace("\r\n", "\n").Split('\n'),
                    _method.SelectedIndex == 0 ? ManualLaunchKind.Native : ManualLaunchKind.Wine, _runner.Text, _prefix.Text).Validate();
                ManualLaunchSetupWorkflow.Save(library, game.RootPath, launch);
                Close(true);
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); options.IsExpanded = true; _status.Text = LanguageAppearance.Format("Linux_ManualLaunchSetupWindow_108", "Could not save: {0}", ex.Message); }
        };
        skip.Click += (_, _) => Close(true); finish.Click += (_, _) => Close(false);
        actions.Children.Add(save); actions.Children.Add(skip); actions.Children.Add(finish); footer.Children.Add(actions);
        Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;
        _executable.Text = game.Launch?.Executable ?? ""; _working.Text = game.Launch?.WorkingDirectory ?? "";
        _arguments.Text = string.Join('\n', game.Launch?.Arguments ?? []); _runner.Text = game.Launch?.Runner ?? ""; _prefix.Text = game.Launch?.WinePrefix ?? "";
        _method.SelectedIndex = game.Launch?.Kind == ManualLaunchKind.Native ? 0 : 1;
        _method.SelectionChanged += (_, _) => { _runner.IsEnabled = _prefix.IsEnabled = _method.SelectedIndex == 1; };
        _runner.IsEnabled = _prefix.IsEnabled = _method.SelectedIndex == 1;
        _candidates.SelectionChanged += (_, _) =>
        {
            if (_candidates.SelectedItem is Suggestion item)
            {
                _executable.Text = item.Path;
                _method.SelectedIndex = item.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
        };
        Closed += (_, _) => _closed = true;
        Opened += async (_, _) =>
        {
            try
            {
                var patterns = library.State.CustomScanPatterns.ToArray();
                var suggestions = await Task.Run(() => (Candidates: LaunchSuggestions.Find(game.RootPath, game.Name, patterns), Wine: LaunchSuggestions.FindWine(Environment.GetEnvironmentVariable("PATH"))));
                if (_closed) return;
                var candidates = suggestions.Candidates;
                if (string.IsNullOrWhiteSpace(_runner.Text)) _runner.Text = suggestions.Wine ?? "";
                _candidates.ItemsSource = candidates.Select(item => new Suggestion(item.Path, item.Label)).ToArray();
                if (string.IsNullOrWhiteSpace(_executable.Text) && candidates.Count > 0) _candidates.SelectedIndex = 0;
                _status.Text = candidates.Count == 0 ? LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_107", "No executable suggestion found. Browse for the executable you use.") : LanguageAppearance.Get("Linux_ManualLaunchSetupWindow_106", "Best-effort suggestions are ready. Verify the executable, Wine choice and launch method before saving.");
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); if (!_closed) _status.Text = LanguageAppearance.Format("Linux_ManualLaunchSetupWindow_105", "Could not scan: {0}. Browse for the executable.", ex.Message); }
        };
    }
    private sealed record Suggestion(string Path, string Label) { public override string ToString() => Label; }
    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static void AddField(StackPanel panel, string label, Control input) { panel.Children.Add(Text(label)); panel.Children.Add(input); }
}
