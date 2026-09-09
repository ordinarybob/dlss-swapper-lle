using DLSS_Swapper;
using DLSS_Swapper.Helpers;
using System.Net;
using System.Net.Http;

internal static class DownloaderHeaderTests
{
    public static async Task RunAsync()
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        App.CurrentApp.HttpClient = client;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Swapper/1.0 (normal) Library/2.0");
        var defaults = client.DefaultRequestHeaders.UserAgent.ToString();
        async Task Download(string? userAgent)
        {
            using var output = new MemoryStream();
            await new FileDownloader("https://example.invalid/file", 0)
                .DownloadFileToStreamAsync(output, userAgent: userAgent);
            if (output.Length != 3) throw new Exception("Download payload changed.");
        }
        await Task.WhenAll(Download("Custom/3.0 (test)"), Download(null), Download(""));
        if (!handler.Headers.Contains("Custom/3.0 (test)") || !handler.Headers.Contains(defaults)
            || !handler.Headers.Contains("") || client.DefaultRequestHeaders.UserAgent.ToString() != defaults)
            throw new Exception("Request-local user agent isolation failed.");
        try { await Download("invalid\r\nheader"); throw new Exception("Invalid header was accepted."); }
        catch (FormatException) { }
        if (client.DefaultRequestHeaders.UserAgent.ToString() != defaults)
            throw new Exception("Failed custom header changed client defaults.");
        Console.WriteLine("Downloader: custom/default/empty user agents remain isolated; invalid header preserves shared defaults.");
    }

    sealed class RecordingHandler : HttpMessageHandler
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Headers { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Headers.Add(request.Headers.UserAgent.ToString());
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        }
    }
}

namespace DLSS_Swapper
{
    internal enum LoggingLevel { Normal, Verbose }
    internal sealed class App
    {
        public static App CurrentApp { get; } = new();
        public HttpClient HttpClient { get; set; } = null!;
        public bool RunOnUIThread(Action action) { action(); return true; }
    }
}
