using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class LibraryPage
{
    private CancellationTokenSource? _downloadCancellation;
    private CancellationToken DownloadToken => _downloadCancellation!.Token;

    private Task<IReadOnlyList<LibraryDownloadResult>> DownloadLatestEntriesAsync(IReadOnlyList<DllCatalogEntry> entries)
    {
        var current = 0;
        var token = DownloadToken;
        return LibraryDownloadWorkflow.RunAsync(entries, async (entry, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            var label = new LibraryDownloadResult(entry, LibraryDownloadStatus.NotAttempted).Label;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested)
                    _statusText.Text = LanguageAppearance.Format("Linux_DllLibraryWindow_Downloads_56", "Checking/downloading {0} of {1}: {2}", ++current, entries.Count, label);
            });
            return await AcquireWithProgressAsync(entry, cancellation).ConfigureAwait(false);
        }, token);
    }

    private Task<CacheAcquisition> AcquireWithProgressAsync(DllCatalogEntry entry, CancellationToken token, Action<string>? report = null) =>
        WithTransferProgressAsync(new LibraryDownloadResult(entry, LibraryDownloadStatus.NotAttempted).Label,
            progress => _cache.AcquireAsync(entry, token, (received, total) => progress(received, total)), token, report);

    private Task<DLSS_Swapper.Data.Streamline.StreamlineSdkPackage> PrepareSdkWithProgressAsync(CancellationToken token) =>
        WithTransferProgressAsync("Streamline SDK", progress => _sdk.PrepareLatestAsync(token, progress), token);

    private async Task<T> WithTransferProgressAsync<T>(string label,
        Func<Action<long, long?>, Task<T>> acquire, CancellationToken token, Action<string>? report = null)
    {
        var active = 1;
        var lastPercent = -1L;
        try
        {
            return await acquire((received, total) =>
            {
                var percent = total is > 0 ? received * 100 / total.Value : received / 1_048_576;
                if (percent == lastPercent) return;
                lastPercent = percent;
                Dispatcher.UIThread.Post(() =>
                {
                    if (Volatile.Read(ref active) == 0 || token.IsCancellationRequested) return;
                    var amount = total is > 0
                        ? LanguageAppearance.Format("Linux_LibraryTransferKnown", "{0:N0} / {1:N0} bytes ({2}%)", received, total.Value, percent)
                        : LanguageAppearance.Format("Linux_LibraryTransferUnknown", "{0:N0} bytes (total size unknown)", received);
                    var text = LanguageAppearance.Format("Linux_LibraryTransfer", "Downloading {0}: {1}. Verification must finish before it is ready.", label, amount);
                    if (report is null) _statusText.Text = text; else report(text);
                });
            }).ConfigureAwait(false);
        }
        finally { Interlocked.Exchange(ref active, 0); }
    }

    private void BeginDownload()
    {
        _downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        FindRequired<Button>("CancelDownloadButton").IsEnabled = true;
        SetBusy(true);
    }

    private void EndDownload()
    {
        FindRequired<Button>("CancelDownloadButton").IsEnabled = false;
        _downloadCancellation?.Dispose();
        _downloadCancellation = null;
        SetBusy(false);
    }

    private void CancelDownload_Click(object? sender, RoutedEventArgs e)
    {
        _downloadCancellation?.Cancel();
        foreach (var cancellation in _recordDownloads.Values) cancellation.Cancel();
        FindRequired<Button>("CancelDownloadButton").IsEnabled = false;
        _statusText.Text = LanguageAppearance.Get("Linux_DllLibraryWindow_Downloads_55", "Cancelling downloads… Completed downloads will be kept.");
    }
}
