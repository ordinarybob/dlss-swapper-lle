using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;
using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class StreamlineLibraryHistoryTests
{
    internal static void Run(string root)
    {
        var network = new Packages();
        var sdkCache = Path.Combine(root, "library-sdk-history");
        var page = new LibraryPage(DllCatalog.Empty(), cache: new DownloadCache(cacheRoot: Path.Combine(root, "library-dlls")),
            sdkHttp: new HttpClient(network), sdkCacheRoot: sdkCache);
        var host = new Window { Content = page, Width = 850, Height = 750 };
        host.Show(); page.Start();
        try
        {
            var families = page.FindControl<ListBox>("FamilyComboBox")!;
            families.SelectedIndex = families.ItemCount - 1;
            var rows = page.FindControl<StackPanel>("StreamlineReleaseRows")!;
            Until(() => rows.Children.Count == 2);
            Button ButtonFor(string tag) => rows.Children.OfType<StackPanel>().SelectMany(row => row.Children).OfType<Button>()
                .Single(button => button.Tag is StreamlineSdkRelease release && release.Tag == tag);
            ButtonFor("v2.7.32").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => !ButtonFor("v2.7.32").IsEnabled && page.FindControl<Button>("DownloadLatestButton")!.IsEnabled);
            Check(network.Downloads.SequenceEqual(new[] { "v2.7.32.zip" }) && !Directory.Exists(Path.Combine(sdkCache, "v2.12.0")),
                "Historical Library download acquired the latest SDK instead.");
            page.FindControl<Button>("DownloadLatestButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => !ButtonFor("v2.12.0").IsEnabled && page.FindControl<Button>("DownloadLatestButton")!.IsEnabled);
            Check(network.Downloads.SequenceEqual(new[] { "v2.7.32.zip", "v2.12.0.zip" }), "Download latest ignored or duplicated SDK acquisition.");
            page.FindControl<Button>("DownloadLatestButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => page.FindControl<Button>("DownloadLatestButton")!.IsEnabled);
            Check(network.Downloads.Count == 2 && rows.Children.Count == 2, "Cached SDK was downloaded again or history disappeared.");
            Console.WriteLine("PASS headless Library SDK history, exact historical download, latest download and cache state.");
        }
        finally
        {
            var stopped = page.StopAsync(); Until(() => stopped.IsCompleted); stopped.GetAwaiter().GetResult(); host.Close();
        }
    }
    static void Until(Func<bool> condition)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(10)) { TestUi.Flush(); Thread.Sleep(1); }
        Check(condition(), "Library SDK history state transition timed out.");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Packages : HttpMessageHandler
    {
        internal List<string> Downloads { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            HttpContent content;
            if (request.RequestUri!.Host == "api.github.com")
            {
                var releases = new[] { "v2.12.0", "v2.7.32" }.Select(tag => new { tag_name = tag,
                    assets = new[] { new { name = $"streamline-sdk-{tag}.zip", browser_download_url = $"https://fixture.invalid/{tag}.zip" } } }).ToArray();
                content = new StringContent(request.RequestUri.AbsolutePath.EndsWith("/latest")
                    ? JsonSerializer.Serialize(releases[0]) : JsonSerializer.Serialize(releases));
            }
            else
            {
                Downloads.Add(Path.GetFileName(request.RequestUri.AbsolutePath));
                using var stream = new MemoryStream();
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                    foreach (var name in StreamlineComponentSet.FileNames)
                    { using var entry = zip.CreateEntry("bin/x64/" + name).Open(); entry.Write(StreamlineMutationDialogTests.DllBytes("sdk")); }
                content = new ByteArrayContent(stream.ToArray());
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = content });
        }
    }
}
