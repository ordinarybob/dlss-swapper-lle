using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Text.Json;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class StreamlineDialogTests
{
    internal static void Run(Window owner, string fixtureRoot)
    {
        var root = Path.Combine(fixtureRoot, "streamline-game"); Directory.CreateDirectory(root);
        foreach (var name in new[] { "sl.common.dll", "sl.dlss_g.dll", "sl.interposer.dll", "sl.reflex.dll" })
            File.WriteAllBytes(Path.Combine(root, name), [1,2,3]);
        var network = new OfflineMetadata();
        LanguageAppearance.Apply("en-US", new Dictionary<string, string>
        {
            ["Linux_Streamline_Unknown"] = "Fixture unknown version",
            ["Linux_Streamline_NoBackup"] = "Fixture no backup",
            ["Linux_StreamlineDescription_common"] = "Fixture common services"
        });
        var window = new StreamlineGameWindow(root, "Fixture", null, network, Path.Combine(fixtureRoot, "sdk-cache"));
        var closed = window.ShowDialog(owner);
        Button Action(string name) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while ((!Action("Close").IsEnabled || network.Requests == 0) && timeout.Elapsed < TimeSpan.FromSeconds(5))
        { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        Check(Action("Close").IsEnabled && !Action("Apply all").IsEnabled, "Offline dialog must not offer an unspecified SDK update");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Fixture common services"),
            "Streamline component description ignored translation");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text?.Contains("Installed: Fixture unknown version") == true) == 4
            && window.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text?.StartsWith("Restore: Fixture no backup") == true) == 4,
            "Streamline component rows ignored translated version/backup labels");
        var all = window.GetVisualDescendants().OfType<CheckBox>().Single(box => Equals(box.Content, "Select all components"));
        Check(!Action("Apply selected").IsEnabled, "Empty selection enabled Apply selected");
        all.IsChecked = true;
        var components = window.GetVisualDescendants().OfType<CheckBox>().Where(box => !ReferenceEquals(box, all)).ToArray();
        Check(components.Length == 4 && components.All(box => box.IsChecked == true) && !Action("Apply selected").IsEnabled, "Selection was lost or an unspecified SDK update was enabled");
        components[0].IsChecked = false;
        Check(all.IsChecked is null, "Partial selection not shown in select-all box");
        all.IsChecked = false; // Clicking an indeterminate header selects the remaining items.
        Check(all.IsChecked == true, "Partial select-all did not complete selection");
        all.IsChecked = false;
        Check(!Action("Apply selected").IsEnabled, "Clear all retained selected operation");
        Check(!Action("Restore selected").IsEnabled && !Action("Restore all originals").IsEnabled, "Restore enabled without backups");
        window.Width = window.MinWidth; window.Height = window.MinHeight; Dispatcher.UIThread.RunJobs();
        foreach (var name in new[] { "Download selected package", "Use local package", "Restore all originals", "Restore selected", "Recover interrupted operation", "Apply selected", "Apply all", "Close" })
        {
            var button = Action(name); var point = button.TranslatePoint(default, window)!.Value;
            Check(point.Y >= 0 && point.Y + button.Bounds.Height <= window.ClientSize.Height + 1
                && point.X >= 0 && point.X + button.Bounds.Width <= window.ClientSize.Width + 1, "Clipped Streamline action: " + name);
            Check(!button.GetVisualAncestors().OfType<ScrollViewer>().Any(), "Streamline action inside scroll: " + name);
        }
        Action("Close").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(closed.IsCompleted, "Close did not finish");
        LanguageAppearance.Apply("en-US");
        Check(network.Requests == 1 && Directory.GetFiles(root).Length == 4
            && Directory.GetFiles(root).All(file => File.ReadAllBytes(file).SequenceEqual(new byte[] {1,2,3})), "Selection changed files or acquired a package");
        Console.WriteLine("PASS headless Streamline selection/footer (not native Linux acceptance)");
        VerifyVersionAndFailedApply(owner, root, fixtureRoot);
        StreamlineMutationDialogTests.Run(owner, fixtureRoot);
        StreamlineDownloadProgressTests.Run(owner, fixtureRoot);
    }
    private static void VerifyVersionAndFailedApply(Window owner, string root, string fixtureRoot)
    {
        var network = new VersionMetadata();
        var cache = Path.Combine(fixtureRoot, "sdk-failure-cache");
        var window = new StreamlineGameWindow(root, "Version fixture", null, network, cache);
        var closed = window.ShowDialog(owner);
        Button Action(string name) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
        void Until(Func<bool> condition)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(5)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            Check(condition(), "Streamline dialog did not finish the expected state transition");
        }
        Until(() => Action("Apply all").IsEnabled && window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("Latest SDK: v2.12.0") == true));
        Check(network.Packages == 0, "Opening dialog downloaded the SDK");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text?.Contains("Available: v2.12.0 SDK") == true) == 4, "Available rows did not update after version lookup");
        var versions = window.GetVisualDescendants().OfType<ComboBox>().Single();
        Check(versions.Items.Count == 2, "Historical SDK version missing from game picker");
        versions.SelectedIndex = 1;
        Until(() => Action("Apply all").IsEnabled && window.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text?.Contains("Available: v2.7.32 SDK") == true) == 4);
        window.GetVisualDescendants().OfType<CheckBox>().Single(box => Equals(box.Content, "Select all components")).IsChecked = true;
        Action("Apply selected").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(() => network.Packages == 1 && Action("Apply selected").IsEnabled);
        Check(network.LastPackage?.EndsWith("v2.7.32.zip") == true && versions.SelectedItem?.ToString() == "v2.7.32", "Apply or failure reset the selected historical SDK");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("503") == true), "Failed acquisition not reported");
        Check(window.GetVisualDescendants().OfType<CheckBox>().All(box => box.IsChecked == true), "Failed acquisition lost selected components");
        Check(Directory.GetFiles(root).Length == 4 && Directory.GetFiles(root).All(file => File.ReadAllBytes(file).SequenceEqual(new byte[] {1,2,3})), "Failed acquisition changed game files");
        Check(!Directory.Exists(cache) || Directory.GetFiles(cache, "*", SearchOption.AllDirectories).Length == 0, "Failed acquisition retained partial package");
        Action("Close").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(closed.IsCompleted, "Failed operation prevented closing");
        Console.WriteLine("PASS headless Streamline metadata and failed apply (not native Linux acceptance)");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class OfflineMetadata : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            return Task.FromException<HttpResponseMessage>(new HttpRequestException(string.Concat(Enumerable.Repeat("Metadata server is unavailable. ", 100))));
        }
    }
    private sealed class VersionMetadata : HttpMessageHandler
    {
        internal int Packages;
        internal string? LastPackage;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "api.github.com")
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(JsonSerializer.Serialize(new[] { "v2.12.0", "v2.7.32" }.Select(tag => new { tag_name = tag, assets = new[]
                    { new { name = $"streamline-sdk-{tag}.zip", browser_download_url = $"https://fixture.invalid/{tag}.zip" } } })))
                });
            Interlocked.Increment(ref Packages);
            LastPackage = request.RequestUri?.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable) { RequestMessage = request });
        }
    }
}
