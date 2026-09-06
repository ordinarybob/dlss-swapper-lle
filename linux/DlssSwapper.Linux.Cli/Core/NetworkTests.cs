using System.Diagnostics;
using System.Net;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record NetworkTest(int Number, string Name, string Target);

public static class NetworkTests
{
    private const string Download = "https://dlss-swapper-downloads.beeradmoore.com/dlss/nvngx_dlss_v1.0.0.0.zip";
    public static IReadOnlyList<NetworkTest> All { get; } = new NetworkTest[]
    {
        new(1, "Google HTTP", "https://google.com"),
        new(2, "Bing HTTP", "https://bing.com"),
        new(3, "DLSS Swapper download", Download),
        new(4, "Compare download in browser", Download),
        new(5, "Steam cover", "https://steamcdn-a.akamaihd.net/steam/apps/870780/library_600x900_2x.jpg"),
        new(6, "Epic cover", "https://cdn1.epicgames.com/item/calluna/Control_Portrait_Storefront_1200X1600_1200x1600-456c920cae7a0aa9b36670cd5e1237a1?w=600&h=900&resize=1"),
        new(7, "DLSS Swapper cover", "https://dlss-swapper-downloads.beeradmoore.com/test/library_600x900_2x.jpg"),
        new(8, "Alternative cover server", "https://files.beeradmoore.com/dlss-swapper/test/library_600x900_2x.jpg"),
        new(9, "Download-server DNS", "dlss-swapper-downloads.beeradmoore.com"),
        new(10, "Download with custom User-Agent", Download),
        new(11, "UploadThing download", "https://hb4kzlkh4u.ufs.sh/f/isdnLt22yljeRWLOje0oeKXyth5OC7M6sI02T3YfL8GPbvpd"),
    };

    public static async Task RunAsync(NetworkTest test, HttpClient http, string userAgent, Action<string> report, CancellationToken token,
        Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        if (test.Number == 4) throw new ArgumentException(T("Linux_NetworkBrowserResultRequired", "The browser test requires the user's reported result."));
        var elapsed = Stopwatch.StartNew();
        report(T("Linux_NetworkTestHeader", "Test {0}: {1} — {2}", test.Number,
            translations?.Get($"NetworkTesterPage_DiagnosticsTest{test.Number}Title", test.Name) ?? test.Name, test.Target));
        try
        {
            if (test.Number == 9)
            {
                var addresses = await Dns.GetHostAddressesAsync(test.Target, token).ConfigureAwait(false);
                if (addresses.Length == 0) throw new IOException(T("Linux_NetworkDnsEmpty", "DNS returned no addresses."));
                foreach (var address in addresses) report(T("Linux_NetworkAddress", "Address: {0}", address));
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, test.Target);
                request.Headers.UserAgent.ParseAdd(test.Number == 10 ? userAgent : "DLSS-Swapper-LLE-Linux");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                report($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var buffer = new byte[81920]; long bytes = 0; long lastReport = 0; int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    bytes += count;
                    if (elapsed.ElapsedMilliseconds - lastReport >= 1000)
                    { report(T("Linux_NetworkReceivedProgress", "Received {0:N0} / {1} bytes", bytes, response.Content.Headers.ContentLength?.ToString() ?? T("Linux_NetworkUnknownSize", "unknown"))); lastReport = elapsed.ElapsedMilliseconds; }
                }
                if (response.Content.Headers.ContentLength is { } expected && bytes != expected) throw new IOException(T("Linux_NetworkIncomplete", "Incomplete response body."));
                report(T("Linux_NetworkReceived", "Received {0:N0} bytes", bytes));
            }
            report(T("Linux_NetworkPassed", "Passed"));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { report(T("Linux_NetworkCancelled", "Cancelled")); }
        catch (Exception error) { report(T("Linux_NetworkFailed", "Failed: {0}", error.Message)); }
        finally { report(T("Linux_NetworkDuration", "Duration: {0:0.00} seconds", elapsed.Elapsed.TotalSeconds)); }
    }
}
