using System.Net;
using System.Text.Json;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class VersionTransitionTests
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("lle-version-transition-");
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(root.FullName));
            var catalog = JsonSerializer.Serialize(DllTypes.All.ToDictionary(type => type.ManifestKey, _ => Array.Empty<object>()));
            var calls = 0; var response = catalog;
            using var http = new HttpClient(new Handler(() => { calls++; return response; }));
            if (await LibraryStartup.RefreshForVersionAsync(http, library, "1.0") is not null || calls != 0)
                throw new Exception("Fresh install did not retain the bundled catalog.");
            var path = Path.Combine(root.FullName, "manifest.json");
            var prior = catalog + new string(' ', 100);
            await File.WriteAllTextAsync(path, prior);
            response = "invalid JSON";
            try { await LibraryStartup.RefreshForVersionAsync(http, library, "2.0"); throw new Exception("Invalid catalog accepted."); }
            catch (JsonException) { }
            if (await File.ReadAllTextAsync(path) != prior || library.State.LastLaunchVersion != "1.0")
                throw new Exception("Failed version refresh lost catalog or advanced version.");
            response = catalog;
            if (await LibraryStartup.RefreshForVersionAsync(http, library, "2.0") is null
                || await File.ReadAllTextAsync(path) != catalog)
                throw new Exception("Valid shorter catalog was not accepted.");
            var reopened = new PersistentLibrary(new LibraryStateStore(root.FullName));
            var before = calls;
            if (reopened.State.LastLaunchVersion != "2.0"
                || await LibraryStartup.RefreshForVersionAsync(http, reopened, "2.0") is not null || calls != before)
                throw new Exception("Same-version launch repeated refresh.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await LibraryStartup.RefreshForVersionAsync(http, reopened, "3.0", cancelled.Token); throw new Exception("Cancelled version refresh succeeded."); }
            catch (OperationCanceledException) { }
            if (reopened.State.LastLaunchVersion != "2.0" || await File.ReadAllTextAsync(path) != catalog)
                throw new Exception("Cancellation changed saved version or catalog.");
        }
        finally { root.Delete(true); }
    }

    private sealed class Handler(Func<string> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request.RequestUri != DllCatalogRefresh.Source) throw new Exception("Unexpected catalog source.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content()) });
        }
    }
}
