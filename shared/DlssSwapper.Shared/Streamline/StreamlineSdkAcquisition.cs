using System.Text.Json;

namespace DLSS_Swapper.Data.Streamline;

public sealed record StreamlineSdkRelease(string Tag, string DownloadUrl);
public sealed record StreamlineSdkPackage(string Tag, string DirectoryPath, bool WasDownloaded);

public static class StreamlineSdkAcquisition
{
    public const string LatestReleaseApi = "https://api.github.com/repos/NVIDIA-RTX/Streamline/releases/latest";
    private static readonly SemaphoreSlim PreparationLock = new(1, 1);

    public static async Task<StreamlineSdkRelease> FetchLatestAsync(HttpClient http, CancellationToken token = default)
    {
        using var response = await http.GetAsync(LatestReleaseApi, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
        return ParseRelease(document.RootElement);
    }

    public static StreamlineSdkRelease ParseRelease(JsonElement root)
    {
        var tag = root.GetProperty("tag_name").GetString();
        if (string.IsNullOrWhiteSpace(tag) || !StreamlinePackageCache.TryGetVersion(tag, out _))
            throw new InvalidDataException("NVIDIA's latest Streamline release has no supported stable version tag.");
        var assets = root.GetProperty("assets").EnumerateArray().Where(item =>
        {
            var name = item.GetProperty("name").GetString();
            return name is not null && name.StartsWith("streamline-sdk-", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        if (assets.Length != 1)
            throw new InvalidDataException("NVIDIA's latest Streamline release has no unambiguous SDK ZIP asset.");
        var url = assets[0].GetProperty("browser_download_url").GetString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0)
            throw new InvalidDataException("The Streamline SDK download URL is invalid.");
        return new(tag, url!);
    }

    public static async Task<StreamlineSdkPackage> PrepareAsync(
        StreamlineSdkRelease release, string cacheRoot, string temporaryRoot,
        Func<string, Stream, CancellationToken, Task> download, CancellationToken token = default)
    {
        if (!StreamlinePackageCache.TryGetVersion(release.Tag, out _))
            throw new InvalidDataException("Unsupported Streamline package version.");
        await PreparationLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var destination = Path.Combine(cacheRoot, release.Tag);
            if (HasCompletePackage(destination)) return new(release.Tag, destination, false);
            Directory.CreateDirectory(temporaryRoot);
            var archive = Path.Combine(temporaryRoot, $"{release.Tag}-{Guid.NewGuid():N}.zip");
            try
            {
                await using (var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true))
                {
                    await download(release.DownloadUrl, output, token).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();
                StreamlineComponentSet.ExtractProductionFiles(archive, destination);
                return new(release.Tag, destination, true);
            }
            finally
            {
                try { File.Delete(archive); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        finally { PreparationLock.Release(); }
    }

    public static bool HasCompletePackage(string directory)
    {
        try { StreamlineComponentSet.ValidatePackage(directory); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (BadImageFormatException) { return false; }
    }
}
