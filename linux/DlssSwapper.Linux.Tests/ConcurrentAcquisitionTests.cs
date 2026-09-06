using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class ConcurrentAcquisitionTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "lle-concurrent-cache-" + Guid.NewGuid().ToString("N"));
        var payload = "concurrent fixture payload"u8.ToArray();
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        { using var file = zip.CreateEntry("nvngx_dlss.dll").Open(); file.Write(payload); }
        var archive = buffer.ToArray();
        var first = new DllCatalogEntry(DllType.Dlss, "1.0", 1, Convert.ToHexString(MD5.HashData(payload)),
            Convert.ToHexString(MD5.HashData(archive)), new Uri("https://example.invalid/first.zip"),
            payload.Length, archive.Length, true, false);
        var second = first with { Version = "2.0", VersionNumber = 2, DownloadUri = new Uri("https://example.invalid/second.zip") };
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancelFirst = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        using var handler = new PausedHandler(archive);
        using var http = new HttpClient(handler);
        using var cache = new DownloadCache(http, root);
        var a = cache.AcquireAsync(first, cancelFirst.Token);
        var b = cache.AcquireAsync(second, lifetime.Token);
        var pending = new List<Task<CacheAcquisition>> { a, b };
        try
        {
            await handler.BothStarted.Task.WaitAsync(lifetime.Token);
            using var cancelWaiter = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var waiting = cache.AcquireAsync(second, cancelWaiter.Token);
            var reuse = cache.AcquireAsync(second, lifetime.Token);
            pending.AddRange([waiting, reuse]);
            cancelWaiter.Cancel();
            try { await waiting.WaitAsync(lifetime.Token); throw new Exception("Same-key waiter ignored cancellation."); }
            catch (OperationCanceledException) when (cancelWaiter.IsCancellationRequested && !lifetime.IsCancellationRequested) { }
            if (b.IsCompleted) throw new Exception("Cancelling waiter affected its acquisition owner.");
            var successor = cache.AcquireAsync(first, lifetime.Token);
            pending.Add(successor);
            cancelFirst.Cancel();
            try { await a; throw new Exception("First acquisition ignored cancellation."); }
            catch (OperationCanceledException) when (cancelFirst.IsCancellationRequested) { }
            if (b.IsCompleted) throw new Exception("Cancelling first affected the second acquisition.");
            if (successor.IsCompleted || File.Exists(cache.GetCachedPath(first)))
                throw new Exception("Owner cancellation cancelled its successor or published a cache file.");
            handler.Release.TrySetResult();
            var acquired = await b;
            if ((await reuse).WasDownloaded) throw new Exception("Same-key waiter claimed another caller's download as new.");
            if (!acquired.WasDownloaded || !cache.IsCached(second))
                throw new Exception("Concurrent cancellation published wrong cache state.");
            var retry = await successor;
            if (!retry.WasDownloaded || !File.ReadAllBytes(retry.Path).AsSpan().SequenceEqual(payload)
                || (await cache.AcquireAsync(second, lifetime.Token)).WasDownloaded)
                throw new Exception("Retry/cache reuse failed after isolated cancellation.");
            if ((await cache.AcquireAsync(first, lifetime.Token)).WasDownloaded)
                throw new Exception("Successful successor was not reusable from cache.");
        }
        finally
        {
            lifetime.Cancel();
            handler.Release.TrySetResult();
            try { await Task.WhenAll(pending); } catch (OperationCanceledException) { }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class PausedHandler(byte[] archive) : HttpMessageHandler
    {
        private int _started;
        public TaskCompletionSource BothStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (Interlocked.Increment(ref _started) == 2) BothStarted.TrySetResult();
            await Release.Task.WaitAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(archive) };
        }
    }
}
