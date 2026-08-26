using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data.Streamline;

internal sealed record StreamlinePackage(string Tag, string DirectoryPath);

internal static class StreamlineReleaseManager
{
    const string LatestReleaseApi =
        "https://api.github.com/repos/NVIDIA-RTX/Streamline/releases/latest";

    internal static async Task<StreamlinePackage> PrepareLatestAsync(
        Action<long, long, double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await App.CurrentApp.HttpClient
            .GetAsync(LatestReleaseApi, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(responseStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString();
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new InvalidDataException("NVIDIA's latest Streamline release has no version tag.");
        }

        var asset = root.GetProperty("assets")
            .EnumerateArray()
            .Select(item => new
            {
                Name = item.GetProperty("name").GetString(),
                Url = item.GetProperty("browser_download_url").GetString(),
            })
            .SingleOrDefault(item =>
                item.Name is not null
                && item.Name.StartsWith("streamline-sdk-", StringComparison.OrdinalIgnoreCase)
                && item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (asset?.Url is null)
        {
            throw new InvalidDataException(
                "NVIDIA's latest Streamline release has no unambiguous SDK ZIP asset.");
        }

        var packageDirectory = Path.Combine(
            Storage.GetStorageFolder(),
            "streamline",
            SanitizePathSegment(tag));
        if (HasCompletePackage(packageDirectory))
        {
            return new(tag, packageDirectory);
        }

        var workingDirectory = Path.Combine(Storage.GetTemp(), "streamline");
        Directory.CreateDirectory(workingDirectory);
        var archivePath = Path.Combine(
            workingDirectory,
            $"{SanitizePathSegment(tag)}-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var output = new FileStream(
                archivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                FileDownloader.BufferSize,
                useAsync: true))
            {
                var downloader = new FileDownloader(asset.Url);
                await downloader.DownloadFileToStreamAsync(
                    output,
                    cancellationToken,
                    progressCallback: progress).ConfigureAwait(false);
            }

            StreamlineComponentSet.ExtractProductionFiles(archivePath, packageDirectory);
            return new(tag, packageDirectory);
        }
        finally
        {
            try
            {
                File.Delete(archivePath);
            }
            catch
            {
                // A stale temporary archive is safe to remove during normal cache cleanup.
            }
        }
    }

    internal static StreamlinePackage? FindNewestCached()
    {
        var root = Path.Combine(Storage.GetStorageFolder(), "streamline");
        if (Directory.Exists(root) == false)
        {
            return null;
        }

        var directory = Directory
            .EnumerateDirectories(root)
            .Where(HasCompletePackage)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return directory is null
            ? null
            : new(Path.GetFileName(directory), directory);
    }

    static bool HasCompletePackage(string directory)
    {
        return Directory.Exists(directory)
            && StreamlineComponentSet.FileNames.All(fileName =>
            {
                var path = Path.Combine(directory, fileName);
                return File.Exists(path) && new FileInfo(path).Length > 0;
            });
    }

    static string SanitizePathSegment(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter, '_');
        }

        return value;
    }
}
