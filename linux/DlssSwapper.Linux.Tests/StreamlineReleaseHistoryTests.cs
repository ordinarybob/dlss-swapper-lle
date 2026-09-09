using System.IO.Compression;
using System.Net;
using System.Text.Json;
using DLSS_Swapper.Data.Streamline;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineReleaseHistoryTests
{
    internal static async Task RunAsync()
    {
        object Release(string tag, bool draft = false) => new
        {
            tag_name = tag, draft, prerelease = false,
            assets = new[] { new { name = $"streamline-sdk-{tag}.zip", browser_download_url = $"https://fixture.invalid/{tag}.zip" } },
        };
        var calls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            var page = request.RequestUri!.Query.Contains("page=2")
                ? new[] { Release("v2.14.1"), Release("v2.12.0"), Release("v9.0.0", true) }
                : new[] { Release("v2.7.32"), Release("v2.12.0") }.Concat(Enumerable.Repeat(Release("v8.0.0", true), 98)).ToArray();
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(page)) };
        }));
        var releases = await StreamlineSdkAcquisition.FetchReleasesAsync(http);
        Check(calls == 2 && releases.Select(release => release.Tag).SequenceEqual(new[] { "v2.14.1", "v2.12.0", "v2.7.32" }),
            "History pagination, stable filtering, numeric order or duplicate removal failed.");

        var root = Directory.CreateTempSubdirectory("lle-sdk-history-");
        try
        {
            var legacyNames = new[] { "sl.common.dll", "sl.interposer.dll", "sl.reflex.dll" };
            using var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
                foreach (var name in legacyNames)
                {
                    using var output = zip.CreateEntry("bin/x64/" + name).Open();
                    output.Write(StreamlineSafetyTests.DllBytes("historical"));
                }
            var requests = new List<string>();
            Task Download(string url, Stream output, CancellationToken token)
            { requests.Add(url); return output.WriteAsync(archive.ToArray(), token).AsTask(); }
            var selected = releases.Last();
            var cache = Path.Combine(root.FullName, "cache");
            var temp = Path.Combine(root.FullName, "temp");
            var package = await StreamlineSdkAcquisition.PrepareAsync(selected, cache, temp, Download);
            Check(package.Tag == "v2.7.32" && requests.Single() == selected.DownloadUrl,
                "Selected historical release was replaced with latest.");
            Check(StreamlineSdkAcquisition.HasCompletePackage(package.DirectoryPath), "Valid older SDK rejected for lacking newer components.");
            Check(!(await StreamlineSdkAcquisition.PrepareAsync(selected, cache, temp, Download)).WasDownloaded && requests.Count == 1,
                "Historical SDK was downloaded twice.");
            var service = new StreamlineLibraryService(http, cache);
            Check(service.FindCached(selected.Tag) == package.DirectoryPath && service.FindCached(releases[0].Tag) is null,
                "Exact cache lookup substituted another version.");
            var game = Directory.CreateDirectory(Path.Combine(root.FullName, "game")).FullName;
            var common = Path.Combine(game, "sl.common.dll");
            var absent = Path.Combine(game, "sl.directsr.dll");
            StreamlineSafetyTests.WriteDll(common, "installed");
            StreamlineSafetyTests.WriteDll(absent, "untouched");
            var failed = StreamlineComponentSet.UpdateExisting(package.DirectoryPath, [common, absent]);
            Check(!failed.Success && StreamlineSafetyTests.ReadLabel(common) == "installed" && StreamlineSafetyTests.ReadLabel(absent) == "untouched",
                "Missing historical component permitted partial mutation.");
            var applied = StreamlineComponentSet.UpdateExisting(package.DirectoryPath, [common]);
            Check(applied.Success && StreamlineSafetyTests.ReadLabel(common) == "historical"
                && StreamlineSafetyTests.ReadLabel(common + ".dlsss") == "installed", "Selected historical bytes or originals lost.");
            File.Delete(Path.Combine(package.DirectoryPath, "sl.reflex.dll"));
            Check(!StreamlineSdkAcquisition.HasCompletePackage(package.DirectoryPath), "Incomplete historical cache accepted.");
        }
        finally { root.Delete(true); }
        Console.WriteLine("PASS: historical SDK pagination, exact acquisition/cache, older component inventories and safe missing-component rejection.");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
}
