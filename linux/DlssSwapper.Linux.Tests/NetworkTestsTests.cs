using System.Net;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class NetworkTestsTests
{
    public static async Task RunAsync()
    {
        var requests = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add(request.Headers.UserAgent.ToString());
            return new(HttpStatusCode.OK) { Content = new StringContent("fixture") };
        }));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("unchanged/1.0");
        if (NetworkTests.All.Count != 11 || !NetworkTests.All.Select(test => test.Number).SequenceEqual(Enumerable.Range(1, 11)))
            throw new Exception("Missing Windows network test.");
        foreach (var test in NetworkTests.All.Where(test => test.Number is not (4 or 9)))
        {
            var lines = new List<string>();
            await NetworkTests.RunAsync(test, http, "fixture/2.0", lines.Add, default);
            if (!lines.Contains("Passed") || !lines.Contains("Received 7 bytes")) throw new Exception("Successful transfer not reported.");
        }
        if (requests.Count != 9 || requests.Count(agent => agent == "fixture/2.0") != 1
            || http.DefaultRequestHeaders.UserAgent.ToString() != "unchanged/1.0") throw new Exception("User-Agent leaked across tests.");
        using var failure = new HttpClient(new Handler(_ => new(HttpStatusCode.BadGateway)));
        var errors = new List<string>();
        await NetworkTests.RunAsync(NetworkTests.All[0], failure, "", errors.Add, default);
        if (errors.Contains("Passed") || !errors.Any(line => line.StartsWith("Failed:"))) throw new Exception("HTTP failure reported success.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        foreach (var test in new[] { NetworkTests.All[0], NetworkTests.All[8] })
        {
            var lines = new List<string>();
            await NetworkTests.RunAsync(test, http, "", lines.Add, cancelled.Token);
            if (!lines.Contains("Cancelled") || lines.Contains("Passed")) throw new Exception("HTTP/DNS cancellation lost.");
        }
        var translations = new Translations("en-US");
        var values = (Dictionary<string, string>)translations.Values;
        values["NetworkTesterPage_DiagnosticsTest1Title"] = "Fixture connectivity";
        values["Linux_NetworkTestHeader"] = "{2} | {1} | {0}";
        values["Linux_NetworkReceived"] = "Fixture bytes {0:N0}";
        values["Linux_NetworkPassed"] = "Fixture passed";
        values["Linux_NetworkFailed"] = "Fixture failure: {0}";
        values["Linux_NetworkCancelled"] = "Fixture cancelled";
        values["Linux_NetworkDuration"] = "Fixture duration {0:0.00}";
        var localized = new List<string>();
        await NetworkTests.RunAsync(NetworkTests.All[0], http, "", localized.Add, default, translations);
        if (localized[0] != "https://google.com | Fixture connectivity | 1"
            || !localized.Contains("Fixture bytes 7") || !localized.Contains("Fixture passed")
            || !localized[^1].StartsWith("Fixture duration "))
            throw new Exception("Translated network result lost its title, target, byte count or duration.");
        localized.Clear();
        await NetworkTests.RunAsync(NetworkTests.All[0], failure, "", localized.Add, default, translations);
        if (!localized.Any(line => line.StartsWith("Fixture failure: ") && line.Contains("502"))
            || localized.Contains("Fixture passed"))
            throw new Exception("Translated network failure lost error detail or reported success.");
        localized.Clear();
        await NetworkTests.RunAsync(NetworkTests.All[0], http, "", localized.Add, cancelled.Token, translations);
        if (!localized.Contains("Fixture cancelled") || localized.Contains("Fixture passed"))
            throw new Exception("Translated network cancellation reported the wrong outcome.");
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
}
