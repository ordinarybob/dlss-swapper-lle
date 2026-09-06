using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Cli.Core;

public sealed class StreamlineLibraryService(HttpClient http, string? cacheRoot = null)
{
    public string CacheRoot { get; } = cacheRoot ?? Path.Combine(
        Path.GetDirectoryName(ArtworkService.GetDefaultCacheRoot())!, "streamline");

    public Task<StreamlineSdkRelease> FetchLatestAsync(CancellationToken token) =>
        StreamlineSdkAcquisition.FetchLatestAsync(http, token);

    public async Task<StreamlineSdkPackage> PrepareLatestAsync(CancellationToken token, Action<long, long?>? transferProgress = null)
    {
        var release = await FetchLatestAsync(token).ConfigureAwait(false);
        return await StreamlineSdkAcquisition.PrepareAsync(release, CacheRoot,
            Path.Combine(CacheRoot, "temporary"), (url, destination, cancellation) =>
                DownloadAsync(url, destination, cancellation, transferProgress), token).ConfigureAwait(false);
    }

    public string? FindNewestCached() => StreamlinePackageCache.FindNewest(
        CacheRoot, StreamlineSdkAcquisition.HasCompletePackage);

    private async Task DownloadAsync(string url, Stream destination, CancellationToken token, Action<long, long?>? transferProgress)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is null || finalUri.Scheme != Uri.UriSchemeHttps || finalUri.UserInfo.Length != 0)
            throw new InvalidDataException("The Streamline package redirected to an invalid download URL.");
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var buffer = new byte[81920];
        long received = 0;
        transferProgress?.Invoke(received, total);
        int count;
        while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            received += count;
            transferProgress?.Invoke(received, total);
        }
    }
}
