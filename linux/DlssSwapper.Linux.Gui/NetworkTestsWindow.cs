using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class NetworkTestsWindow : Window
{
    public NetworkTestsWindow()
    {
        Title = LanguageAppearance.Get("Linux_NetworkTestsWindow_131", "Network tests"); Width = 850; Height = 650; MinWidth = 550; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        CancellationTokenSource? cancel = null;
        var closeRequested = false;
        var layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto,Auto"), Margin = new Thickness(18), RowSpacing = 10 };
        var selection = new ComboBox { ItemsSource = NetworkTests.All.Select(test => $"{test.Number}. {LanguageAppearance.Get($"NetworkTesterPage_DiagnosticsTest{test.Number}Title", test.Name)}").ToArray(), SelectedIndex = 0,
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        layout.Children.Add(selection);
        var agent = new TextBox { Name = "UserAgentTextBox", Text = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/133.0.0.0 Safari/537.36", IsVisible = false };
        ToolTip.SetTip(agent, LanguageAppearance.Get("Linux_NetworkUserAgentHint", "Custom User-Agent for test 10 only")); Grid.SetRow(agent, 1); layout.Children.Add(agent);
        var results = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(results, 2); layout.Children.Add(results);
        var browser = new WrapPanel { IsVisible = false };
        var open = new Button { Content = LanguageAppearance.Get("Linux_NetworkTestsWindow_129", "Open browser") }; var copyLink = new Button { Content = LanguageAppearance.Get("Linux_NetworkTestsWindow_128", "Copy link") };
        var passed = new Button { Content = LanguageAppearance.Get("Linux_NetworkTestsWindow_127", "Download worked") }; var failed = new Button { Content = LanguageAppearance.Get("Linux_NetworkTestsWindow_126", "Download failed") };
        foreach (var button in new[] { open, copyLink, passed, failed }) { button.Margin = new Thickness(0, 0, 8, 8); browser.Children.Add(button); }
        Grid.SetRow(browser, 3); layout.Children.Add(browser);
        var actions = new WrapPanel(); var run = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("NetworkTesterPage_RunTest") }; var stop = new Button { Content = LanguageAppearance.Get("Linux_NetworkTestsWindow_125", "Cancel test"), IsEnabled = false };
        var copy = new Button { Content = LanguageAppearance.Get("Linux_NetworkTestsWindow_124", "Copy results") }; var close = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Close") };
        foreach (var button in new[] { run, stop, copy, close }) { button.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(button); }
        Grid.SetRow(actions, 4); layout.Children.Add(actions); Content = layout;
        selection.SelectionChanged += (_, _) => { agent.IsVisible = selection.SelectedIndex == 9; browser.IsVisible = false; stop.IsEnabled = false; };
        void Append(string message) => results.Text += $"{DateTimeOffset.Now:O} {message}\n";
        run.Click += async (_, _) =>
        {
            if (cancel is not null || selection.SelectedIndex < 0) return;
            var test = NetworkTests.All[selection.SelectedIndex];
            if (test.Number == 4)
            {
                browser.IsVisible = true;
                stop.IsEnabled = true;
                Append(LanguageAppearance.Format("Linux_NetworkBrowserInstructions", "Test 4: Open or copy {0}, then report whether the browser downloaded it. Opening the browser is not a passed test.", test.Target));
                return;
            }
            cancel = new(); run.IsEnabled = selection.IsEnabled = agent.IsEnabled = false; stop.IsEnabled = true;
            try
            {
                await NetworkTests.RunAsync(test, http, agent.Text ?? "", message => Dispatcher.UIThread.Post(() => Append(message)), cancel.Token, LanguageAppearance.Current);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            }
            finally
            {
                cancel.Dispose(); cancel = null; run.IsEnabled = selection.IsEnabled = agent.IsEnabled = true; stop.IsEnabled = false;
                if (closeRequested) Close();
            }
        };
        stop.Click += (_, _) => { cancel?.Cancel(); if (browser.IsVisible) { Append(LanguageAppearance.Get("Linux_NetworkBrowserCancelled", "Test 4: Cancelled")); browser.IsVisible = false; stop.IsEnabled = false; } };
        open.Click += async (_, _) =>
        {
            try
            {
                var launched = await Launcher.LaunchUriAsync(new Uri(NetworkTests.All[3].Target));
                Append(launched ? LanguageAppearance.Get("Linux_NetworkBrowserRequested", "Browser launch requested; awaiting your download result.")
                    : LanguageAppearance.Get("Linux_NetworkBrowserFailed", "Browser launch failed."));
            }
            catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); Append(LanguageAppearance.Format("Linux_NetworkBrowserError", "Browser launch failed: {0}", error.Message)); }
        };
        async Task CopyAsync(string text)
        {
            try { if (Clipboard is null) throw new IOException(LanguageAppearance.Get("Linux_NetworkClipboardUnavailable", "Clipboard is unavailable.")); await Clipboard.SetTextAsync(text); }
            catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); Append(LanguageAppearance.Format("Linux_NetworkCopyFailed", "Copy failed: {0}", error.Message)); }
        }
        copyLink.Click += async (_, _) => await CopyAsync(NetworkTests.All[3].Target);
        copy.Click += async (_, _) => await CopyAsync(results.Text ?? "");
        passed.Click += (_, _) => { Append(LanguageAppearance.Get("Linux_NetworkBrowserWorked", "Test 4: User reported the browser download worked.")); browser.IsVisible = false; stop.IsEnabled = false; };
        failed.Click += (_, _) => { Append(LanguageAppearance.Get("Linux_NetworkBrowserDidNotWork", "Test 4: User reported the browser download failed.")); browser.IsVisible = false; stop.IsEnabled = false; };
        close.Click += (_, _) => Close();
        Closing += (_, e) => { closeRequested = true; cancel?.Cancel(); e.Cancel = cancel is not null; };
        Closed += (_, _) => http.Dispose();
    }
}
