using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.Pages;

public partial class LibraryPageModel
{
    readonly LibraryDownloadProgress _downloadProgress = new();
    readonly Dictionary<Guid, LibraryTransferProgress> _streamlineTransfers = [];
    readonly Dictionary<Guid, FileDownloader> _observedDllDownloads = [];
    DispatcherTimer? _downloadProgressTimer;

    [ObservableProperty] public partial bool IsLibraryDownloading { get; set; }
    [ObservableProperty] public partial bool IsLibraryDownloadIndeterminate { get; set; }
    [ObservableProperty] public partial double LibraryDownloadPercent { get; set; }
    [ObservableProperty] public partial string LibraryDownloadText { get; set; } = "";

    internal void StartDownloadProgress()
    {
        if (_downloadProgressTimer is null)
        {
            _downloadProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _downloadProgressTimer.Tick += (_, _) => RefreshDownloadProgress();
        }
        RefreshDownloadProgress();
        _downloadProgressTimer.Start();
    }

    internal void StopDownloadProgress() => _downloadProgressTimer?.Stop();

    void RefreshDownloadProgress()
    {
        var transfers = new List<LibraryTransferProgress>(_streamlineTransfers.Values);
        foreach (var family in DllFamilyRegistry.All)
        foreach (var record in family.Records(DLLManager.Instance))
        {
            if (record.LocalRecord?.FileDownloader is { } download)
            {
                _observedDllDownloads[download.Guid] = download;
                transfers.Add(new(download.Guid, download.DownloadedBytes,
                    download.TotalBytesToDownload, download.Percent >= 100));
            }
        }
        // Read final counters even when a download finished between UI ticks.
        var observed = _observedDllDownloads.Values.Select(download => new LibraryTransferProgress(
            download.Guid, download.DownloadedBytes, download.TotalBytesToDownload, download.Percent >= 100));
        var state = _downloadProgress.Update(transfers, observed);
        if (!state.Visible) _observedDllDownloads.Clear();
        IsLibraryDownloading = state.Visible;
        IsLibraryDownloadIndeterminate = state.Indeterminate;
        LibraryDownloadPercent = state.Percent;
        LibraryDownloadText = state.Text;
    }
}
