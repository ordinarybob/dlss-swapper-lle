using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data.Streamline;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.Pages;

public partial class LibraryPageModel
{
    const string StreamlineLibraryTag = "Streamline";
    sealed record LibraryDownloadResult(string Name, bool Downloaded, string? Error = null);
    Task<LibraryDownloadResult>? _streamlineDownload;
    bool _checkingStreamline;
    int _downloadGeneration;

    public Visibility StreamlineVisibility => SelectedSelectorBarItem?.Tag is string
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DllLibraryVisibility => StreamlineVisibility == Visibility.Visible
        ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    public partial string StreamlineVersion { get; set; } = "Streamline SDK";

    [ObservableProperty]
    public partial string StreamlineStatus { get; set; } = "Checking latest version…";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadStreamlineCommand))]
    public partial bool IsStreamlineDownloading { get; set; }

    internal async Task RefreshStreamlineAsync()
    {
        if (_checkingStreamline || IsStreamlineDownloading) return;
        _checkingStreamline = true;
        var generation = _downloadGeneration;
        try
        {
            var cached = await Task.Run(StreamlineReleaseManager.FindNewestCached);
            if (generation != _downloadGeneration) return;
            StreamlineVersion = cached is null ? "Streamline SDK" : $"Streamline SDK {cached.Tag}";
            StreamlineStatus = "Checking latest version…";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var release = await StreamlineReleaseManager.FetchLatestAsync(timeout.Token);
            if (generation != _downloadGeneration) return;
            StreamlineVersion = $"Streamline SDK {release.Tag}";
            StreamlineStatus = cached?.Tag == release.Tag
                ? "Downloaded · ready for game and batch updates"
                : cached is null ? "Available to download"
                : $"Update available · {cached.Tag} downloaded";
        }
        catch (Exception ex)
        {
            if (generation == _downloadGeneration)
                StreamlineStatus = $"Could not check latest version: {ex.Message}";
        }
        finally { _checkingStreamline = false; }
    }

    bool CanDownloadStreamline() => !IsStreamlineDownloading;

    [RelayCommand(CanExecute = nameof(CanDownloadStreamline))]
    async Task DownloadStreamlineAsync() => await AcquireStreamlineAsync();

    // Both entry points share one operation, including its final result.
    Task<LibraryDownloadResult> AcquireStreamlineAsync()
    {
        if (_streamlineDownload is { IsCompleted: false }) return _streamlineDownload;
        return _streamlineDownload = DownloadStreamlineCoreAsync();
    }

    async Task<LibraryDownloadResult> DownloadStreamlineCoreAsync()
    {
        IsStreamlineDownloading = true;
        ++_downloadGeneration;
        StreamlineStatus = "Downloading latest package…";
        try
        {
            var package = await StreamlineReleaseManager.PrepareLatestAsync();
            StreamlineVersion = $"Streamline SDK {package.Tag}";
            StreamlineStatus = "Downloaded · ready for game and batch updates";
            return new($"Streamline SDK {package.Tag}", package.WasDownloaded);
        }
        catch (Exception ex)
        {
            StreamlineStatus = $"Download failed: {ex.Message}";
            return new("Streamline SDK", false, ex.Message);
        }
        finally { IsStreamlineDownloading = false; }
    }
}
