using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data.Streamline;

internal sealed record StreamlinePackage(string Tag, string DirectoryPath, bool WasDownloaded = false);
internal sealed record StreamlineRelease(string Tag, string DownloadUrl);

internal static class StreamlineReleaseManager
{
    internal static async Task<StreamlinePackage> PrepareLatestAsync(
        Action<long, long, double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var release = await StreamlineSdkAcquisition.FetchLatestAsync(
            App.CurrentApp.HttpClient, cancellationToken).ConfigureAwait(false);
        var package = await StreamlineSdkAcquisition.PrepareAsync(
            release,
            Path.Combine(Storage.GetStorageFolder(), "streamline"),
            Path.Combine(Storage.GetTemp(), "streamline"),
            async (url, output, token) =>
            {
                var downloader = new FileDownloader(url);
                await downloader.DownloadFileToStreamAsync(
                    output, token, progressCallback: progress).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return new(package.Tag, package.DirectoryPath, package.WasDownloaded);
    }

    // Metadata lookup has no package extraction or storage writes.
    internal static async Task<StreamlineRelease> FetchLatestAsync(CancellationToken cancellationToken = default)
    {
        var release = await StreamlineSdkAcquisition.FetchLatestAsync(
            App.CurrentApp.HttpClient, cancellationToken).ConfigureAwait(false);
        return new(release.Tag, release.DownloadUrl);
    }

    internal static StreamlinePackage? FindNewestCached()
    {
        var directory = StreamlinePackageCache.FindNewest(
            Path.Combine(Storage.GetStorageFolder(), "streamline"),
            StreamlineSdkAcquisition.HasCompletePackage);
        return directory is null ? null : new(Path.GetFileName(directory), directory);
    }
}
