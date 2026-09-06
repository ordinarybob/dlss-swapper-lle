using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public sealed partial class LibraryPage
{
    private readonly Dictionary<DllLibraryEntryViewModel, CancellationTokenSource> _recordDownloads = [];
    private bool HasRecordDownloads => _recordDownloads.Count != 0;

    private async Task<bool> DownloadRecordAsync(DllLibraryEntryViewModel row)
    {
        if (_isBusy || _closeRequested) return false;
        if (_recordDownloads.TryGetValue(row, out var existing))
        {
            existing.Cancel();
            row.TransferStatus = LanguageAppearance.Get("Linux_LibraryCancelling", "Cancelling…");
            return false;
        }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _recordDownloads.Add(row, cancellation);
        row.IsDownloading = true;
        row.TransferStatus = LanguageAppearance.Get("Linux_LibraryCheckingDownload", "Checking/downloading…");
        UpdateActionState();
        try
        {
            var acquired = await AcquireWithProgressAsync(row.Entry, cancellation.Token, text => row.TransferStatus = text);
            row.IsCached = true;
            row.TransferStatus = acquired.WasDownloaded
                ? LanguageAppearance.Get("Linux_LibraryVerified", "Downloaded and verified")
                : LanguageAppearance.Get("Linux_LibraryAlreadyVerified", "Already downloaded and verified");
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            row.TransferStatus = LanguageAppearance.Get("Linux_LibraryDownloadCancelled", "Download cancelled");
            return false;
        }
        catch (Exception error)
        { AppLog.Write(ApplicationLogLevel.Error, error.Message);
            row.TransferStatus = LanguageAppearance.Format("Linux_LibraryDownloadFailed", "Download failed: {0}", error.Message);
            return false;
        }
        finally
        {
            _recordDownloads.Remove(row);
            ApplyPendingCatalog();
            row.IsDownloading = false;
            UpdateActionState();
            if (_closeRequested && !HasRecordDownloads && !_isBusy) FinishStopping();
        }
    }
}
