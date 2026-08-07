using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Data.Steam;

namespace DLSS_Swapper.Data.ManuallyAdded;

internal static class WikipediaArtworkLookup
{
    const long MaximumCoverBytes = 15 * 1024 * 1024;
    static readonly TimeSpan MissingLookupRetryInterval = TimeSpan.FromDays(7);
    static readonly TimeSpan MinimumApiRequestInterval = TimeSpan.FromMilliseconds(500);
    static readonly SemaphoreSlim LookupGate = new(1, 1);
    static DateTime _lastApiRequestUtc = DateTime.MinValue;

    internal static string? FindCachedCover(string title, string installPath)
    {
        var coverPath = GetCoverPath(title, installPath);
        return File.Exists(coverPath) ? coverPath : null;
    }

    internal static async Task<string?> ResolveCoverUrlAsync(string title, string installPath)
    {
        if (string.IsNullOrWhiteSpace(title) || HasRecentMissingLookupMarker(title, installPath))
        {
            return null;
        }

        await LookupGate.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                var searchUrl = "https://en.wikipedia.org/w/api.php"
                    + "?action=query&generator=search&gsrnamespace=0&gsrlimit=5"
                    + "&redirects=1&prop=images&imlimit=max&format=json&formatversion=2&maxlag=5"
                    + $"&gsrsearch={Uri.EscapeDataString(title + " video game")}";
                var searchResponse = await GetResponseAsync(searchUrl).ConfigureAwait(false);
                if (searchResponse is null)
                {
                    return null;
                }

                var page = FindExactPage(searchResponse, title);
                var imageTitle = page?.Images
                    .Where(image => IsCoverImageName(image.Title))
                    .OrderBy(image => GetCoverImageNameScore(image.Title))
                    .ThenBy(image => image.Title.Length)
                    .Select(image => image.Title)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(imageTitle))
                {
                    MarkLookupUnavailable(title, installPath);
                    return null;
                }

                var imageInfoUrl = "https://en.wikipedia.org/w/api.php"
                    + "?action=query&prop=imageinfo&iiprop=url%7Cmime%7Csize"
                    + "&format=json&formatversion=2&maxlag=5"
                    + $"&titles={Uri.EscapeDataString(imageTitle)}";
                var imageResponse = await GetResponseAsync(imageInfoUrl).ConfigureAwait(false);
                var imageInfo = imageResponse?.Query?.Pages
                    .SelectMany(candidate => candidate.ImageInfo)
                    .FirstOrDefault();
                if (IsUsablePortraitCover(imageInfo) == false)
                {
                    MarkLookupUnavailable(title, installPath);
                    return null;
                }

                ClearMissingLookupMarker(title, installPath);
                return imageInfo!.Url;
            }
            catch (Exception err)
            {
                // Network and parsing failures remain retryable. Only a successful
                // lookup with no safe cover candidate receives a negative marker.
                Logger.Warning($"Unable to find Wikipedia artwork for manually added game '{title}'. {err.Message}");
                return null;
            }
        }
        finally
        {
            LookupGate.Release();
        }
    }

    internal static string PersistCover(string title, string installPath, string sourcePath)
    {
        var coverPath = GetCoverPath(title, installPath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(coverPath)!);
            File.Copy(sourcePath, coverPath, true);
            ClearMissingLookupMarker(title, installPath);
            return coverPath;
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to persist Wikipedia artwork in {coverPath}. {err.Message}");
            return sourcePath;
        }
    }

    static async Task<WikipediaResponse?> GetResponseAsync(string url)
    {
        var earliestRequestUtc = _lastApiRequestUtc.Add(MinimumApiRequestInterval);
        var delay = earliestRequestUtc - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("DLSS-Swapper-LLE/1.0");
        _lastApiRequestUtc = DateTime.UtcNow;
        using var response = await App.CurrentApp.HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (response.IsSuccessStatusCode == false)
        {
            Logger.Warning($"Wikipedia artwork lookup returned {response.StatusCode}.");
            return null;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize(
            responseStream,
            SourceGenerationContext.Default.WikipediaResponse);
    }

    static WikipediaPage? FindExactPage(WikipediaResponse response, string title)
    {
        var normalizedTitle = NormalizeArticleTitle(title);
        var exactPage = response.Query?.Pages.FirstOrDefault(page =>
            NormalizeArticleTitle(page.Title).Equals(
                normalizedTitle,
                StringComparison.Ordinal));
        if (exactPage is not null)
        {
            return exactPage;
        }

        var exactRedirect = response.Query?.Redirects.FirstOrDefault(redirect =>
            NormalizeArticleTitle(redirect.From).Equals(
                normalizedTitle,
                StringComparison.Ordinal));
        if (exactRedirect is null)
        {
            return null;
        }

        var normalizedTarget = NormalizeArticleTitle(exactRedirect.To);
        return response.Query?.Pages.FirstOrDefault(page =>
            NormalizeArticleTitle(page.Title).Equals(
                normalizedTarget,
                StringComparison.Ordinal));
    }

    static string NormalizeArticleTitle(string title)
    {
        var withoutDisambiguator = RemoveVideoGameDisambiguator(title);
        var normalizedNumerals = Regex.Replace(
            withoutDisambiguator,
            @"\b(VIII|VII|VI|IV|IX|III|II|X|V)\b",
            match => match.Value.ToUpperInvariant() switch
            {
                "II" => "2",
                "III" => "3",
                "IV" => "4",
                "V" => "5",
                "VI" => "6",
                "VII" => "7",
                "VIII" => "8",
                "IX" => "9",
                "X" => "10",
                _ => match.Value,
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalizedNumerals = Regex.Replace(
            normalizedNumerals,
            @"\b(part|episode|chapter|volume|vol)\s+I\b",
            "$1 1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return SteamArtworkLookup.NormalizeTitle(normalizedNumerals);
    }

    static string RemoveVideoGameDisambiguator(string title)
    {
        const string suffix = " (video game)";
        return title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? title[..^suffix.Length]
            : title;
    }

    static bool IsCoverImageName(string imageTitle)
    {
        var normalized = SteamArtworkLookup.NormalizeTitle(imageTitle);
        return normalized.Contains("cover", StringComparison.Ordinal)
            || normalized.Contains("boxart", StringComparison.Ordinal)
            || normalized.Contains("poster", StringComparison.Ordinal)
            || normalized.Contains("keyart", StringComparison.Ordinal);
    }

    static int GetCoverImageNameScore(string imageTitle)
    {
        var normalized = SteamArtworkLookup.NormalizeTitle(imageTitle);
        if (normalized.Contains("cover", StringComparison.Ordinal))
        {
            return 0;
        }
        if (normalized.Contains("boxart", StringComparison.Ordinal))
        {
            return 1;
        }
        if (normalized.Contains("poster", StringComparison.Ordinal))
        {
            return 2;
        }
        return 3;
    }

    static bool IsUsablePortraitCover(WikipediaImageInfo? imageInfo)
    {
        if (imageInfo is null
            || imageInfo.Width < 200
            || imageInfo.Height < 200
            || imageInfo.Height <= imageInfo.Width
            || imageInfo.Height > imageInfo.Width * 2
            || imageInfo.Size <= 0
            || imageInfo.Size > MaximumCoverBytes
            || imageInfo.Mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == false
            || Uri.TryCreate(imageInfo.Url, UriKind.Absolute, out var imageUri) == false)
        {
            return false;
        }

        return imageUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && imageUri.Host.Equals("upload.wikimedia.org", StringComparison.OrdinalIgnoreCase);
    }

    static string GetCoverPath(string title, string installPath)
    {
        return Path.Combine(
            SteamArtworkLookup.GetLookupDirectory(installPath),
            $"manual_{SteamArtworkLookup.GetLookupKey(title)}_wikipedia_400_600.png");
    }

    static string GetMissingLookupMarkerPath(string title, string installPath)
    {
        return Path.Combine(
            SteamArtworkLookup.GetLookupDirectory(installPath),
            $"manual_{SteamArtworkLookup.GetLookupKey(title)}.wikipedia-lookup.missing");
    }

    static bool HasRecentMissingLookupMarker(string title, string installPath)
    {
        var markerPath = GetMissingLookupMarkerPath(title, installPath);
        try
        {
            return File.Exists(markerPath)
                && File.GetLastWriteTimeUtc(markerPath) >= DateTime.UtcNow.Subtract(MissingLookupRetryInterval);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to read manual Wikipedia artwork marker {markerPath}. {err.Message}");
            return false;
        }
    }

    static void MarkLookupUnavailable(string title, string installPath)
    {
        var markerPath = GetMissingLookupMarkerPath(title, installPath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            File.WriteAllText(markerPath, string.Empty);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to persist manual Wikipedia artwork marker {markerPath}. {err.Message}");
        }
    }

    static void ClearMissingLookupMarker(string title, string installPath)
    {
        var markerPath = GetMissingLookupMarkerPath(title, installPath);
        try
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to clear manual Wikipedia artwork marker {markerPath}. {err.Message}");
        }
    }
}
