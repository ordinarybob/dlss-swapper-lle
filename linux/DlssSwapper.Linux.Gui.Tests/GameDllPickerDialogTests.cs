using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class GameDllPickerDialogTests
{
    internal static void Run(Window owner, string fixtureRoot)
    {
        var root = Path.Combine(fixtureRoot, "picker-game"); Directory.CreateDirectory(root);
        var target = Path.Combine(root, DllTypes.Get(DllType.Fsr31Dx12).FileName);
        var original = StreamlineMutationDialogTests.DllBytes("picker original");
        var updated = StreamlineMutationDialogTests.DllBytes("picker updated");
        File.WriteAllBytes(target, original);
        var hash = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(updated)).ToLowerInvariant();
        var entry = new DllCatalogEntry(DllType.Fsr31Dx12, "1.0.1.41314", 1, hash, "", null, updated.Length, 0, true, false, InternalName: "3.1.4", IsImported: true);
        var catalog = DllCatalog.Empty(); catalog.AddImported(entry);
        // Populate the case-sensitive cache with the same canonical record the picker uses.
        entry = catalog.GetEntries(DllType.Fsr31Dx12).Single();
        var details = new DllRecordDetailsWindow(entry with { FileDescription = new string('x', 20000) });
        try
        {
            details.Show(owner); details.Width = details.MinWidth; details.Height = details.MinHeight;
            Dispatcher.UIThread.RunJobs();
            var closeDetails = details.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Close"));
            var point = closeDetails.TranslatePoint(default, details)!.Value;
            Check(point.Y >= 0 && point.Y + closeDetails.Bounds.Height <= details.ClientSize.Height + 1
                && point.X >= 0 && point.X + closeDetails.Bounds.Width <= details.ClientSize.Width + 1,
                "Long DLL metadata clipped Close at minimum size");
            Check(!closeDetails.GetVisualAncestors().OfType<ScrollViewer>().Any(), "DLL details Close is inside scrolling");
        }
        finally { details.Close(); }
        catalog.AddImported(entry with { Md5 = new string('b', 32), InternalName = "3.1.3" });
        var cacheRoot = Path.Combine(fixtureRoot, "picker-cache");
        using (var cache = new DownloadCache(cacheRoot: cacheRoot))
        { var path = cache.GetCachedPath(entry); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, updated); }
        var game = new SelectedGame("Picker fixture", root, "preserved-id") { WinePrefix = "/fixture-prefix" };
        var window = new GameDllPickerWindow(game, catalog, cacheRoot, downloadedOnly: true); var closed = window.ShowDialog(owner);
        Button Action(Window dialog, string name) => dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
        void Click(Window dialog, string name) => Action(dialog, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Until(Func<bool> condition)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            Check(condition(), "DLL picker timed out");
        }
        ConfirmationDialog Confirm()
        {
            Until(() => window.OwnedWindows.OfType<ConfirmationDialog>().Any());
            return window.OwnedWindows.OfType<ConfirmationDialog>().Single();
        }
        Until(() => Action(window, "Close").IsEnabled);
        window.Width = window.MinWidth; window.Height = window.MinHeight; Dispatcher.UIThread.RunJobs();
        CheckActions(window);
        var releases = window.GetVisualDescendants().OfType<ListBox>().Single();
        Check(releases.Items.Count == 1 && releases.Items[0]?.ToString() == "v3.1.4 (v1.0.1.41314)", "Picker lacks readable release/build version");
        var filter = window.GetVisualDescendants().OfType<CheckBox>().Single();
        Check(filter.IsChecked == true, "Picker ignored downloaded-only preference");
        filter.IsChecked = false; Check(releases.Items.Count == 2, "Local filter did not expose uncached release");
        filter.IsChecked = true; Check(releases.Items.Count == 1, "Downloaded-only filter retained uncached release");
        releases.SelectedIndex = 0;
        LanguageAppearance.Apply("en-US", new Dictionary<string, string>
        {
            ["Linux_ApplyDllTitle"] = "Fixture DLL confirmation",
            ["Linux_ApplyDllPrompt"] = "Game {2}; version {0}; family {1}"
        });
        Click(window, "Apply"); var confirmation = Confirm();
        Check(confirmation.FindControl<TextBlock>("MessageText")?.Text?.Contains("v3.1.4 (v1.0.1.41314)") == true, "Confirmation loses release name");
        Check(confirmation.Title == "Fixture DLL confirmation"
            && confirmation.FindControl<TextBlock>("MessageText")!.Text!.StartsWith("Game Picker fixture; version "), "DLL confirmation ignored translated title or reordered fields");
        Click(confirmation, "Cancel"); Until(() => Action(window, "Apply").IsEnabled);
        LanguageAppearance.Apply("en-US");
        Check(File.ReadAllBytes(target).SequenceEqual(original) && !File.Exists(target + ".dlsss"), "Cancelled picker changed files");
        Click(window, "Apply"); Click(Confirm(), "Continue"); Until(() => Action(window, "Restore this family").IsEnabled);
        Check(File.ReadAllBytes(target).SequenceEqual(updated) && File.ReadAllBytes(target + ".dlsss").SequenceEqual(original), "Picker apply or backup failed");
        Check(!Action(window, "Apply").IsEnabled && releases.SelectedItem?.ToString()?.EndsWith(" — installed") == true, "Picker did not recognize updated file");
        Dispatcher.UIThread.RunJobs(); CheckActions(window);
        var emptyCacheRoot = Path.Combine(fixtureRoot, "picker-empty-cache");
        var uncached = new GameDllPickerWindow(game, catalog, emptyCacheRoot, downloadedOnly: true);
        var uncachedClosed = uncached.ShowDialog(owner);
        Until(() => Action(uncached, "Close").IsEnabled);
        var uncachedReleases = uncached.GetVisualDescendants().OfType<ListBox>().Single();
        Check(uncachedReleases.Items.Count == 1 && uncachedReleases.SelectedItem?.ToString()?.EndsWith(" — installed") == true,
            "Downloaded-only hid the installed release when its package was absent");
        Check(!Action(uncached, "Apply").IsEnabled && Action(uncached, "DLL information").IsEnabled,
            "Uncached installed release did not retain read-only information/current status");
        Check(!Directory.Exists(emptyCacheRoot) || !Directory.EnumerateFiles(emptyCacheRoot, "*", SearchOption.AllDirectories).Any(),
            "Inspecting the installed release unexpectedly acquired a package");
        Click(uncached, "Close"); Check(uncachedClosed.IsCompleted, "Uncached picker failed to close");
        Click(window, "Restore this family"); Click(Confirm(), "Continue");
        Until(() => Action(window, "Close").IsEnabled && window.Results.Count == 2);
        Check(File.ReadAllBytes(target).SequenceEqual(original) && !File.Exists(target + ".dlsss"), "Picker restore failed");
        Check(window.Results.All(result => result.Success && result.Game == game), "Picker lost operation identity or failed");
        Click(window, "Close"); Check(closed.IsCompleted, "Picker failed to close");
        Console.WriteLine("PASS headless DLL picker labels/cancel/apply/current/restore/identity (not native Linux acceptance)");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void CheckActions(Window window)
    {
        foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(button => button.IsVisible && button.Content is string text
            && new[] { "Apply", "Restore this family", "DLL information", "Open containing folder", "Close" }.Contains(text)))
        {
            var point = button.TranslatePoint(default, window)!.Value;
            Check(point.Y >= 0 && point.Y + button.Bounds.Height <= window.ClientSize.Height + 1
                && point.X >= 0 && point.X + button.Bounds.Width <= window.ClientSize.Width + 1, "DLL picker action clipped: " + button.Content);
            Check(!button.GetVisualAncestors().OfType<ScrollViewer>().Any(), "DLL picker action is inside scrolling content");
        }
    }
}
