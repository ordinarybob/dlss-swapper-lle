using System.IO.Compression;
using System.Net;
using System.Text.Json;
using DLSS_Swapper.Data.Streamline;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineAcquisitionTests
{
    internal static async Task RunAsync()
    {
        TestReleaseSelection();
        var root = Path.Combine(Path.GetTempPath(), "lle-sdk-fixture-" + Guid.NewGuid().ToString("N"));
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in StreamlineComponentSet.FileNames)
            {
                using var output = zip.CreateEntry("bin/x64/" + name).Open();
                output.Write(StreamlineSafetyTests.DllBytes(name));
            }
        }
        var zipBytes = archive.ToArray();
        var metadata = JsonSerializer.Serialize(new
        {
            tag_name = "v2.12.0",
            assets = new[] { new { name = "streamline-sdk-v2.12.0.zip", browser_download_url = "https://example.invalid/sdk.zip" } },
        });
        using var handler = new Handler(metadata, zipBytes);
        using var http = new HttpClient(handler);
        var service = new StreamlineLibraryService(http, root);
        try
        {
            var latest = await service.FetchLatestAsync(CancellationToken.None);
            Check(latest.Tag == "v2.12.0" && handler.ArchiveRequests == 0 && !Directory.Exists(root), "Metadata lookup acquired/wrote package data.");
            var progress = new List<(long Received, long? Total)>();
            var first = await service.PrepareLatestAsync(CancellationToken.None,
                (received, total) => progress.Add((received, total)));
            Check(first.WasDownloaded && handler.ArchiveRequests == 1, "Cold package not acquired.");
            Check(progress.Count >= 2 && progress[0].Received == 0
                && progress[^1].Received == zipBytes.Length
                && progress.All(item => item.Total == zipBytes.Length), "SDK transfer progress differs from archive bytes.");
            StreamlineComponentSet.ValidatePackage(first.DirectoryPath);
            progress.Clear();
            var second = await service.PrepareLatestAsync(CancellationToken.None,
                (received, total) => progress.Add((received, total)));
            Check(!second.WasDownloaded && handler.ArchiveRequests == 1, "Cached package downloaded again.");
            Check(progress.Count == 0, "Cached SDK reported transfer progress.");
            Check(service.FindNewestCached() == first.DirectoryPath, "Finalized cache lookup failed.");
            Check(!Directory.EnumerateFiles(Path.Combine(root, "temporary")).Any(), "Temporary archive was retained.");
            var summary = LibraryDownloadWorkflow.Describe([], [$"Streamline SDK {first.Tag}"]);
            Check(summary.Contains(first.Tag) && !summary.Contains("No new files"), "SDK-only summary contradicts download.");
            using var invalid = JsonDocument.Parse(metadata.Replace("v2.12.0", "../escape"));
            var rejected = false;
            try { StreamlineSdkAcquisition.ParseRelease(invalid.RootElement); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Invalid release tag accepted.");
            using var unknownHandler = new Handler(metadata, zipBytes, unknownLength: true);
            using var unknownHttp = new HttpClient(unknownHandler);
            var unknownService = new StreamlineLibraryService(unknownHttp, Path.Combine(root, "unknown-length"));
            using var cancel = new CancellationTokenSource();
            var cancelled = false;
            try
            {
                await unknownService.PrepareLatestAsync(cancel.Token, (received, total) =>
                {
                    Check(total is null, "Unknown HTTP length was invented.");
                    if (received > 0) cancel.Cancel();
                });
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { cancelled = true; }
            Check(cancelled && unknownService.FindNewestCached() is null, "Cancelled SDK was finalized.");
            Check(!Directory.EnumerateFiles(Path.Combine(unknownService.CacheRoot, "temporary")).Any(),
                "Cancelled SDK left its temporary archive.");
            progress.Clear();
            var retry = await unknownService.PrepareLatestAsync(CancellationToken.None,
                (received, total) => progress.Add((received, total)));
            Check(retry.WasDownloaded && unknownHandler.ArchiveRequests == 2
                && progress[0].Received == 0 && progress[^1].Received == zipBytes.Length
                && progress.All(item => item.Total is null), "Unknown-length retry progress is incorrect.");
            StreamlineComponentSet.ValidatePackage(retry.DirectoryPath);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static void TestReleaseSelection()
    {
        const string tag = "v2.14.1";
        const string x64 = "streamline-sdk-v2.14.1.zip";
        const string arm = "streamline-sdk-v2.14.1-aarch64.zip";
        const string armEc = "streamline-sdk-v2.14.1-arm64ec.zip";
        StreamlineSdkRelease Parse(string releaseTag, params string[] names)
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                tag_name = releaseTag,
                assets = names.Select(name => new
                {
                    name,
                    browser_download_url = $"https://github.com/NVIDIA-RTX/Streamline/releases/download/{releaseTag}/{name}",
                }),
            }));
            return StreamlineSdkAcquisition.ParseRelease(document.RootElement);
        }
        foreach (var names in new[] { new[] { arm, armEc, x64 }, new[] { x64, armEc, arm }, new[] { x64 } })
        {
            var selected = Parse(tag, names);
            Check(selected.Tag == tag && selected.DownloadUrl.EndsWith("/" + x64, StringComparison.Ordinal),
                "The current multi-architecture release did not select the x64 SDK.");
        }
        Check(Parse("v2.12.0", "streamline-sdk-v2.12.0.zip").Tag == "v2.12.0",
            "The older single-package release was rejected.");
        foreach (var names in new[]
        {
            new[] { arm, armEc },
            new[] { "streamline-sdk-v2.12.0.zip" },
            new[] { "streamline-sdk-v2.14.1-debug.zip", "source.zip" },
            Array.Empty<string>(),
            new[] { x64, x64 },
        })
        {
            var rejected = false;
            try { Parse(tag, names); }
            catch (InvalidDataException ex)
            {
                rejected = true;
                Check(ex.Message.Contains(x64, StringComparison.Ordinal), "Package selection failure did not identify the required file.");
            }
            Check(rejected, "Missing, wrong-architecture, wrong-version or duplicate SDK package was accepted.");
        }
        foreach (var url in new[] { "http://example.invalid/sdk.zip", "https://user@example.invalid/sdk.zip", "not-a-url" })
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                tag_name = tag,
                assets = new[] { new { name = x64, browser_download_url = url } },
            }));
            var rejected = false;
            try { StreamlineSdkAcquisition.ParseRelease(document.RootElement); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Invalid SDK URL was accepted.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class UnknownLengthContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class Handler(string metadata, byte[] archive, bool unknownLength = false) : HttpMessageHandler
    {
        internal int ArchiveRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            HttpContent content;
            if (request.RequestUri!.AbsoluteUri == StreamlineSdkAcquisition.LatestReleaseApi)
                content = new StringContent(metadata);
            else { ArchiveRequests++; content = unknownLength ? new UnknownLengthContent(archive) : new ByteArrayContent(archive); }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = content });
        }
    }
}
