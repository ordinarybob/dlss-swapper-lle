using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class BatchDialogTests
{
    internal static void Run(Window owner, string root)
    {
        var manifest = Path.Combine(root, "batch-manifest.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(DllTypes.All.ToDictionary(family => family.ManifestKey, _ => Array.Empty<object>())));
        var catalog = DllCatalog.Load(manifest);
        var entry = new DllCatalogEntry(DllType.Fsr31Dx12, "1.0.1.41314", 1, new string('a', 32), "", null, 1, 0, true, false, InternalName: "3.1.4", IsImported: true);
        catalog.AddImported(entry);
        var game = new SelectedGame("Fixture", root, null);
        var scan = new ScanResult(game, [new(DllType.Fsr31Dx12, Path.Combine(root, "fixture.dll"), "fixture.dll", "", "1")], [])
        { StreamlineFiles = [Path.Combine(root, "sl.common.dll"), Path.Combine(root, "sl.reflex.dll")] };
        var network = new Offline();
        LanguageAppearance.Apply("en-US", new Dictionary<string, string> { ["Linux_StreamlineDescription_common"] = "Fixture common services" });
        var window = new BatchUpdateWindow(catalog, [scan], network);
        var closed = window.ShowDialog(owner);
        window.Width = window.MinWidth; window.Height = window.MinHeight;
        var include = window.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Content?.ToString()?.StartsWith("Include Streamline") == true);
        include.IsChecked = true; Dispatcher.UIThread.RunJobs();
        var components = Components(window);
        Check(components.Length == 2 && components.All(box => box.IsChecked == true), "Batch lists absent components or omits detected components");
        Check(Equals(ToolTip.GetTip(components.Single(box => Equals(box.Content, "sl.common.dll"))), "Fixture common services"),
            "Batch component tooltip ignored translation");
        var picker = window.GetVisualDescendants().OfType<ComboBox>().Single(box => box.PlaceholderText != "Streamline SDK version");
        Check(picker.Items.Cast<object>().Any(item => item.ToString() == "v3.1.4 (v1.0.1.41314)"), "Batch omitted public FSR version");
        foreach (var name in new[] { "Apply", "Cancel", "View / save report", "Components…" })
        {
            var button = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
            var point = button.TranslatePoint(default, window)!.Value;
            Check(point.Y >= 0 && point.Y + button.Bounds.Height <= window.ClientSize.Height + 1
                && point.X >= 0 && point.X + button.Bounds.Width <= window.ClientSize.Width + 1, "Batch action clipped: " + name);
            Check(!button.GetVisualAncestors().OfType<ScrollViewer>().Any(), "Batch action inside scroll: " + name);
        }
        include.IsChecked = false; include.IsChecked = true;
        Check(components.All(box => box.IsChecked == true), "Toggling Streamline lost selection");
        window.Close(); Check(closed.IsCompleted, "Batch failed to close");
        LanguageAppearance.Apply("en-US");
        window = new BatchUpdateWindow(catalog, [scan with { StreamlineFiles = [] }], new Offline(), dllCacheRoot: Path.Combine(root, "empty-cache"), downloadedOnly: true);
        closed = window.ShowDialog(owner);
        Check(!window.GetVisualDescendants().OfType<CheckBox>().Single().IsEnabled, "Empty batch enables Streamline");
        Check(window.GetVisualDescendants().OfType<ComboBox>().Single(box => box.PlaceholderText != "Streamline SDK version").Items.Count == 1, "Downloaded-only batch retained an uncached release");
        window.Close(); Check(closed.IsCompleted, "Empty batch failed to close");
        Console.WriteLine("PASS headless batch detected components/version labels/footer (not native Linux acceptance)");
        VerifyExecution(owner, root, catalog);
    }
    private static void VerifyExecution(Window owner, string root, DllCatalog catalog)
    {
        var dllBytes = StreamlineMutationDialogTests.DllBytes("new:DLSS");
        var dllHash = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(dllBytes)).ToLowerInvariant();
        var dllEntry = new DllCatalogEntry(DllType.Dlss, "2.0", 2, dllHash, "", null, dllBytes.Length, 0, true, false, IsImported: true);
        catalog.AddImported(dllEntry);
        var dllCacheRoot = Path.Combine(root, "batch-dll-cache");
        using (var cache = new DownloadCache(cacheRoot: dllCacheRoot))
        {
            var payload = cache.GetCachedPath(dllEntry);
            Directory.CreateDirectory(Path.GetDirectoryName(payload)!); File.WriteAllBytes(payload, dllBytes);
        }
        var scans = Enumerable.Range(0, 2).Select(index =>
        {
            var folder = Path.Combine(root, "batch-game-" + index); Directory.CreateDirectory(folder);
            var paths = new[] { "sl.common.dll", "sl.reflex.dll" }.Select(name => Path.Combine(folder, name)).ToArray();
            foreach (var path in paths) File.WriteAllBytes(path, StreamlineMutationDialogTests.DllBytes("old:" + Path.GetFileName(path)));
            var dll = Path.Combine(folder, "nvngx_dlss.dll");
            File.WriteAllBytes(dll, StreamlineMutationDialogTests.DllBytes("old:DLSS"));
            return new ScanResult(new SelectedGame("Batch fixture " + index, folder, null),
                [new(DllType.Dlss, dll, "nvngx_dlss.dll", DllScanner.ComputeMd5(dll), "2.0")], []) { StreamlineFiles = paths };
        }).ToArray();
        var network = new StreamlineMutationDialogTests.PackageHandler();
        var window = new BatchUpdateWindow(catalog, scans, network, Path.Combine(root, "batch-sdk"), dllCacheRoot);
        var closed = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        Button Action(Window dialog, string name) => dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
        void Click(Window dialog, string name) => Action(dialog, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Until(Func<bool> condition)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            Check(condition(), "Timed out waiting for batch operation");
        }
        ConfirmationDialog Confirm()
        {
            Until(() => window.OwnedWindows.OfType<ConfirmationDialog>().Any());
            return window.OwnedWindows.OfType<ConfirmationDialog>().Single();
        }
        window.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Content?.ToString()?.StartsWith("Include Streamline") == true).IsChecked = true;
        Components(window).Single(box => Equals(box.Content, "sl.reflex.dll")).IsChecked = false;
        window.GetVisualDescendants().OfType<ComboBox>().Single(box => box.PlaceholderText != "Streamline SDK version").SelectedIndex = 1;
        Click(window, "Apply"); var confirmation = Confirm();
        Check(network.Packages == 1, "Batch did not acquire exactly one SDK");
        Check(confirmation.FindControl<TextBlock>("WarningText")?.Text?.Contains("not recommended") == true, "Batch omitted partial-set warning");
        Click(confirmation, "Cancel"); Until(() => Action(window, "Apply").IsEnabled);
        Check(window.Results.Count == 0 && scans.SelectMany(scan => scan.StreamlineFiles).All(path => File.ReadAllBytes(path).SequenceEqual(StreamlineMutationDialogTests.DllBytes("old:" + Path.GetFileName(path)))), "Cancelled batch reported or changed files");
        Check(scans.All(scan => File.ReadAllBytes(Path.Combine(scan.Game.RootPath, "nvngx_dlss.dll")).SequenceEqual(StreamlineMutationDialogTests.DllBytes("old:DLSS"))
            && Directory.GetFiles(scan.Game.RootPath).Length == 3), "Cancelled mixed batch changed ordinary DLLs or created backups");
        Click(window, "Apply"); confirmation = Confirm(); Click(confirmation, "Continue");
        Until(() => Action(window, "View / save report").IsEnabled);
        Check(network.Packages == 1, "Batch reacquired cached SDK");
        Check(window.Results.Count == 4 && window.Results.All(result => result.Success)
            && scans.All(scan => window.Results.Count(result => result.Game.RootPath == scan.Game.RootPath) == 2)
            && window.Results.Count(result => result.Family == "Streamline") == 2
            && window.Results.Count(result => result.Family == "DLSS") == 2, "Batch results do not match both families in both games: " + string.Join("; ", window.Results.Select(result => result.Family + ": " + result.Message)));
        foreach (var scan in scans)
        {
            var common = Path.Combine(scan.Game.RootPath, "sl.common.dll");
            var reflex = Path.Combine(scan.Game.RootPath, "sl.reflex.dll");
            var dll = Path.Combine(scan.Game.RootPath, "nvngx_dlss.dll");
            Check(File.ReadAllBytes(dll).SequenceEqual(dllBytes), "Mixed batch omitted ordinary DLL update");
            Check(File.ReadAllBytes(dll + ".dlsss").SequenceEqual(StreamlineMutationDialogTests.DllBytes("old:DLSS")), "Mixed batch lost ordinary DLL original");
            Check(File.ReadAllBytes(common).SequenceEqual(StreamlineMutationDialogTests.DllBytes("new:sl.common.dll")), "Batch did not replace selected component");
            Check(File.ReadAllBytes(common + ".dlsss").SequenceEqual(StreamlineMutationDialogTests.DllBytes("old:sl.common.dll")), "Batch lost original");
            Check(File.ReadAllBytes(reflex).SequenceEqual(StreamlineMutationDialogTests.DllBytes("old:sl.reflex.dll")) && !File.Exists(reflex + ".dlsss"), "Batch touched an unselected component");
        }
        Click(window, "Close"); Check(closed.IsCompleted, "Completed batch failed to close");
        var unchanged = scans.SelectMany(scan => Directory.GetFiles(scan.Game.RootPath)).ToDictionary(path => path,
            path => (Bytes: File.ReadAllBytes(path), Modified: File.GetLastWriteTimeUtc(path)));
        var cachedNetwork = new StreamlineMutationDialogTests.PackageHandler();
        window = new BatchUpdateWindow(catalog, scans, cachedNetwork, Path.Combine(root, "batch-sdk"), dllCacheRoot, downloadedOnly: true);
        closed = window.ShowDialog(owner);
        window.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Content?.ToString()?.StartsWith("Include Streamline") == true).IsChecked = true;
        Components(window).Single(box => Equals(box.Content, "sl.reflex.dll")).IsChecked = false;
        Click(window, "Apply");
        Until(() => Action(window, "Apply").IsEnabled);
        var summary = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text)
            .SingleOrDefault(text => text?.StartsWith("No files were changed.") == true);
        Check(summary is not null && scans.All(scan => summary.Contains(scan.Game.Name)) && summary.Contains("sl.common.dll")
            && summary.Contains("Identical — no change"), "Already-current batch omitted component/game comparison");
        Check(cachedNetwork.Packages == 0 && window.Results.Count == 0 && window.OwnedWindows.Count == 0, "Already-current batch downloaded, confirmed or reported updates");
        Check(unchanged.All(item => File.ReadAllBytes(item.Key).SequenceEqual(item.Value.Bytes)
            && File.GetLastWriteTimeUtc(item.Key) == item.Value.Modified), "Already-current batch rewrote files or backups");
        Click(window, "Cancel"); Check(closed.IsCompleted, "Already-current batch failed to close");
        var missingNetwork = new StreamlineMutationDialogTests.PackageHandler();
        window = new BatchUpdateWindow(catalog, scans, missingNetwork, Path.Combine(root, "missing-sdk"), dllCacheRoot, downloadedOnly: true);
        closed = window.ShowDialog(owner);
        window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
        Click(window, "Apply"); Until(() => Action(window, "Apply").IsEnabled);
        Check(missingNetwork.Packages == 0 && window.Results.Count == 0
            && window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("No cached Streamline SDK") == true), "Downloaded-only mode acquired SDK or omitted missing-cache guidance");
        Click(window, "Cancel"); Check(closed.IsCompleted, "Missing-cache batch failed to close");
        Console.WriteLine("PASS headless batch SDK acquisition/confirmation/cancel/selected execution/results (not native Linux acceptance)");
    }
    private static CheckBox[] Components(Window window)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Components…"));
        Check(button.IsEnabled, "Component picker unavailable");
        var flyout = (Flyout)button.Flyout!;
        flyout.ShowAt(button); Dispatcher.UIThread.RunJobs();
        var boxes = ((Control)flyout.Content!).GetVisualDescendants().OfType<CheckBox>().ToArray();
        Check(boxes.Length > 0, "Component popup is empty");
        flyout.Hide();
        return boxes;
    }
    private static void Check(bool value, string message) { if (!value) { Console.Error.WriteLine(message); throw new Exception(message); } }
    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException(string.Concat(Enumerable.Repeat("Metadata server is unavailable. ", 100))));
    }
}
