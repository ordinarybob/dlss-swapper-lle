using System.IO.Compression;
using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Gui;
using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class StreamlineDownloadProgressTests
{
    internal static void Run(Window owner, string root)
    {
        var game = Path.Combine(root, "progress-game"); Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "sl.common.dll"), StreamlineMutationDialogTests.DllBytes("old"));
        var handler = new DownloadHandler();
        var window = new StreamlineGameWindow(game, "Progress", null, handler, Path.Combine(root, "progress-cache"));
        var closed = window.ShowDialog(owner);
        Button Button(string name) => window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, name));
        bool Text(string part) => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(part) == true);
        var progress = window.GetVisualDescendants().OfType<ProgressBar>().Single();
        void Click(string name) => Button(name).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Until(() => Button("Download selected package").IsEnabled);
        Click("Download selected package");
        Until(() => Text("503") && Button("Download selected package").IsEnabled);
        Click("Download selected package");
        Until(() => progress.IsVisible && progress.Value > 0);
        Check(!Text("503") && Text("bytes (") && !progress.IsIndeterminate && progress.Value < 100,
            "Retry did not replace stale error with determinate byte progress");
        Click("Cancel operation");
        Until(() => Text("Cancelled") && Button("Download selected package").IsEnabled);
        Check(!progress.IsVisible && File.ReadAllBytes(Path.Combine(game, "sl.common.dll"))
            .SequenceEqual(StreamlineMutationDialogTests.DllBytes("old")), "Cancelled download changed game files or retained progress");
        Click("Download selected package");
        Until(() => progress.IsVisible && Text("total size unknown"));
        Check(progress.IsIndeterminate, "Unknown content length used determinate progress");
        handler.Release.TrySetResult();
        Until(() => Text("Downloaded SDK") && Button("Download selected package").IsEnabled);
        TestUi.Flush();
        Check(!progress.IsVisible && !Text("503") && !Text("Downloading Streamline"), "Late progress replaced the completion result");
        Click("Close"); Check(closed.IsCompleted, "Progress dialog did not close");
        Console.WriteLine("PASS Streamline progress, stale error clearing, cancellation and retry");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Until(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10)) { TestUi.Flush(); Thread.Sleep(1); }
        Check(condition(), "Download progress transition timed out");
    }
    private sealed class DownloadHandler : HttpMessageHandler
    {
        private int _downloads;
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.Host == "api.github.com") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("[{\"tag_name\":\"v2.12.0\",\"assets\":[{\"name\":\"streamline-sdk-v2.12.0.zip\",\"browser_download_url\":\"https://fixture.invalid/sdk.zip\"}]}]")
            });
            if (++_downloads == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request });
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                foreach (var name in StreamlineComponentSet.FileNames)
                { using var entry = zip.CreateEntry("bin/x64/" + name, CompressionLevel.NoCompression).Open(); entry.Write(StreamlineMutationDialogTests.DllBytes("new")); }
            var content = new StreamContent(new PausedStream(buffer.ToArray(), Release.Task));
            if (_downloads == 2) content.Headers.ContentLength = buffer.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = content });
        }
    }
    private sealed class PausedStream(byte[] bytes, Task release) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        private bool _started;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (_started) await release.WaitAsync(token);
            _started = true;
            return await _inner.ReadAsync(buffer[..Math.Min(buffer.Length, 1024)], token);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
