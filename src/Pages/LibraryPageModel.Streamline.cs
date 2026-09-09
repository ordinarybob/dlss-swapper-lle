using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    int _activeStreamlineDownloads;
    public ObservableCollection<StreamlineLibraryRow> StreamlineReleases { get; } = [];

    public Visibility StreamlineVisibility => SelectedSelectorBarItem?.Tag is string
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DllLibraryVisibility => StreamlineVisibility == Visibility.Visible
        ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    public partial string StreamlineStatus { get; set; } = "Checking latest version…";

    [ObservableProperty]
    public partial bool IsStreamlineDownloading { get; set; }

    internal async Task RefreshStreamlineAsync()
    {
        if (_checkingStreamline) return;
        _checkingStreamline = true;
        try
        {
            if (StreamlineReleases.Count == 0) PopulateStreamlineReleases(await Task.Run(StreamlineReleaseManager.CachedReleases));
            if (!IsStreamlineDownloading) StreamlineStatus = "Loading release history…";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var releases = await StreamlineReleaseManager.FetchReleasesAsync(timeout.Token);
            PopulateStreamlineReleases(releases);
            if (!IsStreamlineDownloading) StreamlineStatus = $"{releases.Count} SDK versions · Downloads do not change game files.";
        }
        catch (Exception ex)
        {
            if (!IsStreamlineDownloading)
            {
                PopulateStreamlineReleases(StreamlineReleaseManager.CachedReleases());
                StreamlineStatus = $"Could not load release history: {ex.Message}. Showing downloaded packages.";
            }
        }
        finally { _checkingStreamline = false; }
    }

    void PopulateStreamlineReleases(System.Collections.Generic.IReadOnlyList<StreamlineRelease> releases)
    {
        StreamlineReleases.Clear();
        foreach (var release in releases)
            StreamlineReleases.Add(new(release, async () => await DownloadStreamlineCoreAsync(release)));
    }

    // Both entry points share one operation, including its final result.
    Task<LibraryDownloadResult> AcquireStreamlineAsync()
    {
        if (_streamlineDownload is { IsCompleted: false }) return _streamlineDownload;
        return _streamlineDownload = DownloadStreamlineCoreAsync();
    }

    async Task<LibraryDownloadResult> DownloadStreamlineCoreAsync(StreamlineRelease? release = null)
    {
        IsStreamlineDownloading = true;
        ++_activeStreamlineDownloads;
        StreamlineStatus = release is null ? "Downloading latest package…" : $"Downloading Streamline {release.Tag}…";
        try
        {
            var package = release is null ? await StreamlineReleaseManager.PrepareLatestAsync()
                : await StreamlineReleaseManager.PrepareAsync(release);
            StreamlineStatus = $"Streamline {package.Tag} downloaded · ready for game and batch updates";
            foreach (var row in StreamlineReleases) row.RefreshDownloaded();
            return new($"Streamline SDK {package.Tag}", package.WasDownloaded);
        }
        catch (Exception ex)
        {
            StreamlineStatus = $"Download failed: {ex.Message}";
            return new("Streamline SDK", false, ex.Message);
        }
        finally { IsStreamlineDownloading = --_activeStreamlineDownloads > 0; }
    }
}

public partial class StreamlineLibraryRow : ObservableObject
{
    readonly StreamlineRelease _release;
    readonly Func<Task> _download;
    public string Version => _release.Tag;
    [ObservableProperty] public partial string Status { get; set; } = "Available to download";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsDownloaded { get; set; }
    internal StreamlineLibraryRow(StreamlineRelease release, Func<Task> download)
    { _release = release; _download = download; RefreshDownloaded(); }
    internal void RefreshDownloaded()
    {
        IsDownloaded = StreamlineReleaseManager.FindCached(_release.Tag) is not null;
        Status = IsDownloaded ? "Downloaded" : "Available to download";
    }
    bool CanDownload() => !IsDownloaded;
    [RelayCommand(CanExecute = nameof(CanDownload))]
    async Task DownloadAsync()
    {
        Status = "Downloading…";
        await _download();
        RefreshDownloaded();
        if (!IsDownloaded) Status = "Download failed. See the message above; retry to download.";
    }
}
