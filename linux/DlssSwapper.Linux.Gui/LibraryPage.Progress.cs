using Avalonia.Controls;

namespace DlssSwapper.Linux.Gui;

public sealed partial class LibraryPage
{
    private void ShowTransferProgress(string label, long received, long? total)
    {
        FindRequired<Grid>("LibraryDownloadProgressHost").IsVisible = true;
        var progress = FindRequired<ProgressBar>("LibraryDownloadProgress");
        progress.IsIndeterminate = total is not > 0;
        progress.Value = total is > 0 ? Math.Clamp(100d * received / total.Value, 0, 100) : 0;
        FindRequired<TextBlock>("LibraryDownloadProgressText").Text = total is > 0
            ? $"{label} — {progress.Value:F0}% · {received / 1048576d:F1} / {total.Value / 1048576d:F1} MB"
            : received > 0 ? $"{label} — {received / 1048576d:F1} MB" : label;
    }

    private void FinishTransferProgress()
    {
        if (!HasRecordDownloads && _downloadCancellation is null)
            FindRequired<Grid>("LibraryDownloadProgressHost").IsVisible = false;
    }
}
