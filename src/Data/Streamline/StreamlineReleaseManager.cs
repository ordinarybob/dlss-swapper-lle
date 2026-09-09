using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data.Streamline;

internal sealed record StreamlinePackage(string Tag, string DirectoryPath, bool WasDownloaded = false);
internal sealed record StreamlineRelease(string Tag, string DownloadUrl)
{
    public override string ToString() => Tag;
}

internal static class StreamlineReleaseManager
{
    internal static async Task<StreamlinePackage> PrepareLatestAsync(
        Action<long, long, double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var release = await StreamlineSdkAcquisition.FetchLatestAsync(
            App.CurrentApp.HttpClient, cancellationToken).ConfigureAwait(false);
        return await PrepareAsync(new(release.Tag, release.DownloadUrl), progress, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<StreamlinePackage> PrepareAsync(StreamlineRelease release,
        Action<long, long, double>? progress = null, CancellationToken cancellationToken = default)
    {
        var package = await StreamlineSdkAcquisition.PrepareAsync(
            new(release.Tag, release.DownloadUrl),
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

    internal static StreamlinePackage? FindCached(string tag)
    {
        if (!StreamlinePackageCache.TryGetVersion(tag, out _)) return null;
        var directory = Path.Combine(Storage.GetStorageFolder(), "streamline", tag);
        return StreamlineSdkAcquisition.HasCompletePackage(directory) ? new(tag, directory) : null;
    }

    internal static IReadOnlyList<StreamlineRelease> CachedReleases()
    {
        var root = Path.Combine(Storage.GetStorageFolder(), "streamline");
        return !Directory.Exists(root) ? [] : Directory.GetDirectories(root).Select(Path.GetFileName).OfType<string>()
            .Where(tag => FindCached(tag) is not null).Select(tag => new StreamlineRelease(tag, ""))
            .OrderByDescending(release => { StreamlinePackageCache.TryGetVersion(release.Tag, out var version); return version; }).ToArray();
    }

    internal static async Task<IReadOnlyList<StreamlineRelease>> FetchReleasesAsync(CancellationToken token = default)
    {
        var releases = await StreamlineSdkAcquisition.FetchReleasesAsync(App.CurrentApp.HttpClient, token).ConfigureAwait(false);
        return releases.Select(release => new StreamlineRelease(release.Tag, release.DownloadUrl))
            .Concat(CachedReleases()).DistinctBy(release => release.Tag, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(release => { StreamlinePackageCache.TryGetVersion(release.Tag, out var version); return version; }).ToArray();
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
