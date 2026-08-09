using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Cli.Core;

public enum ArtworkOrigin
{
    None,
    SteamLocal,
    SteamCache,
    SteamCdn,
    MediaWikiCache,
    MediaWiki,
}

public sealed record ArtworkResult(
    string? Path,
    ArtworkOrigin Origin,
    string? ResolvedSteamAppId,
    string? Warning = null);

public interface IArtworkImageProcessor
{
    Task SavePortraitAsync(
        ReadOnlyMemory<byte> source,
        string destinationPath,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken);
}

public sealed class ArtworkService
{
    private const long MaximumCoverBytes = 15L * 1024 * 1024;
    private static readonly TimeSpan MissingRetryInterval = TimeSpan.FromDays(7);

    private readonly HttpClient _httpClient;
    private readonly IArtworkImageProcessor _imageProcessor;
    private readonly string _cacheRoot;
    private readonly SemaphoreSlim _mediaWikiGate = new(1, 1);
    private readonly TimeSpan _minimumMediaWikiInterval;
    private DateTimeOffset _lastMediaWikiRequest = DateTimeOffset.MinValue;

    public ArtworkService(
        HttpClient httpClient,
        IArtworkImageProcessor imageProcessor,
        string? cacheRoot = null,
        TimeSpan? minimumMediaWikiInterval = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _imageProcessor = imageProcessor
            ?? throw new ArgumentNullException(nameof(imageProcessor));
        _cacheRoot = Path.GetFullPath(cacheRoot ?? GetDefaultCacheRoot());
        _minimumMediaWikiInterval = minimumMediaWikiInterval
            ?? TimeSpan.FromMilliseconds(500);
    }

    public async Task<ArtworkResult> ResolveAsync(
        SelectedGame game,
        LinuxLibraryState state,
        IReadOnlyList<SteamGame> knownSteamGames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(knownSteamGames);

        var appId = game.SteamAppId;
        if (string.IsNullOrWhiteSpace(appId))
        {
            appId = await ResolveManualSteamAppIdAsync(
                game,
                knownSteamGames,
                cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(appId))
        {
            var steam = await ResolveSteamArtworkAsync(
                game,
                appId,
                knownSteamGames,
                cancellationToken).ConfigureAwait(false);
            if (steam.Path is not null)
            {
                return steam with { ResolvedSteamAppId = appId };
            }
        }

        var fallback = await ResolveMediaWikiArtworkAsync(
            game,
            state,
            cancellationToken).ConfigureAwait(false);
        return fallback with { ResolvedSteamAppId = appId };
    }

    public static string NormalizeTitle(string title)
    {
        var decomposed = title.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            if (character is 'Δ' or 'δ')
            {
                normalized.Append("delta");
            }
            else
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString();
    }

    public static string GetDefaultCacheRoot()
    {
        var xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdgCacheHome) && Path.IsPathRooted(xdgCacheHome))
        {
            return Path.Combine(xdgCacheHome, "dlss-swapper-lle", "artwork");
        }

        if (!OperatingSystem.IsWindows())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                home = Environment.GetEnvironmentVariable("HOME");
            }

            if (!string.IsNullOrWhiteSpace(home))
            {
                return Path.Combine(home, ".cache", "dlss-swapper-lle", "artwork");
            }
        }

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            throw new InvalidOperationException("Could not locate the user cache directory.");
        }

        return Path.Combine(localData, "DLSS Swapper LLE", "Linux", "artwork");
    }

    private async Task<ArtworkResult> ResolveSteamArtworkAsync(
        SelectedGame game,
        string appId,
        IReadOnlyList<SteamGame> knownSteamGames,
        CancellationToken cancellationToken)
    {
        foreach (var path in GetSteamLocalCandidates(game, appId, knownSteamGames))
        {
            if (File.Exists(path))
            {
                return new ArtworkResult(path, ArtworkOrigin.SteamLocal, appId);
            }
        }

        var directory = GetLookupDirectory(game.RootPath);
        var cachedPath = Path.Combine(directory, $"{appId}_library_600x900.jpg");
        if (File.Exists(cachedPath))
        {
            return new ArtworkResult(cachedPath, ArtworkOrigin.SteamCache, appId);
        }

        var missingPath = cachedPath + ".missing";
        if (HasRecentMarker(missingPath))
        {
            return new ArtworkResult(null, ArtworkOrigin.None, appId);
        }

        var uri = new Uri(
            $"https://steamcdn-a.akamaihd.net/steam/apps/{Uri.EscapeDataString(appId)}/library_600x900.jpg");
        try
        {
            var bytes = await DownloadImageAsync(uri, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                MarkMissing(missingPath);
                return new ArtworkResult(null, ArtworkOrigin.None, appId);
            }

            Directory.CreateDirectory(directory);
            await WriteAtomicAsync(cachedPath, bytes.Value, cancellationToken).ConfigureAwait(false);
            ClearMarker(missingPath);
            return new ArtworkResult(cachedPath, ArtworkOrigin.SteamCdn, appId);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            return new ArtworkResult(
                null,
                ArtworkOrigin.None,
                appId,
                $"Steam artwork failed: {exception.Message}");
        }
    }

    private async Task<string?> ResolveManualSteamAppIdAsync(
        SelectedGame game,
        IReadOnlyList<SteamGame> knownSteamGames,
        CancellationToken cancellationToken)
    {
        var pathMatch = knownSteamGames.FirstOrDefault(steamGame =>
            PathComparers.FileSystemPath.Equals(steamGame.InstallDirectory, game.RootPath));
        if (pathMatch is not null)
        {
            return pathMatch.AppId;
        }

        var normalizedTitle = NormalizeTitle(game.Name);
        var titleMatches = knownSteamGames
            .Where(steamGame => NormalizeTitle(steamGame.Name) == normalizedTitle)
            .Select(steamGame => steamGame.AppId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (titleMatches.Length == 1)
        {
            return titleMatches[0];
        }

        var directory = GetLookupDirectory(game.RootPath);
        var key = GetLookupKey(game.Name);
        var mappingPath = Path.Combine(directory, $"manual_{key}.steam-appid");
        if (File.Exists(mappingPath))
        {
            var persisted = (await File.ReadAllTextAsync(mappingPath, cancellationToken)
                .ConfigureAwait(false)).Trim();
            if (uint.TryParse(persisted, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return persisted;
            }
        }

        var missingPath = Path.Combine(directory, $"manual_{key}.steam-lookup.missing");
        if (HasRecentMarker(missingPath))
        {
            return null;
        }

        try
        {
            var searchUri = new Uri(
                $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(game.Name)}&l=english&cc=US");
            using var response = await _httpClient.GetAsync(
                searchUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var exactIds = items.EnumerateArray()
                .Where(item => item.TryGetProperty("name", out var name)
                    && NormalizeTitle(name.GetString() ?? string.Empty) == normalizedTitle)
                .Select(item => item.TryGetProperty("id", out var id) ? id.GetInt32() : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            if (exactIds.Length != 1)
            {
                MarkMissing(missingPath);
                return null;
            }

            var appId = exactIds[0].ToString(CultureInfo.InvariantCulture);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(mappingPath, appId, cancellationToken)
                .ConfigureAwait(false);
            ClearMarker(missingPath);
            return appId;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or JsonException)
        {
            return null;
        }
    }

    private async Task<ArtworkResult> ResolveMediaWikiArtworkAsync(
        SelectedGame game,
        LinuxLibraryState state,
        CancellationToken cancellationToken)
    {
        if (!LibraryStateStore.TryValidateArtworkSource(
            state.MediaWikiApiEndpoint,
            state.MediaWikiImageHost,
            out var endpoint,
            out var imageHost,
            out var validationError))
        {
            return new ArtworkResult(null, ArtworkOrigin.None, null, validationError);
        }

        var directory = GetLookupDirectory(game.RootPath);
        var sourceKey = GetSourceKey(endpoint, imageHost);
        var titleKey = GetLookupKey(game.Name);
        var cachedPath = Path.Combine(
            directory,
            $"manual_{titleKey}_{sourceKey}_400_600.png");
        if (File.Exists(cachedPath))
        {
            return new ArtworkResult(cachedPath, ArtworkOrigin.MediaWikiCache, null);
        }

        var missingPath = Path.Combine(
            directory,
            $"manual_{titleKey}.{sourceKey}-lookup.missing");
        if (HasRecentMarker(missingPath))
        {
            return new ArtworkResult(null, ArtworkOrigin.None, null);
        }

        await _mediaWikiGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var searchQuery =
                "action=query&generator=search&gsrnamespace=0&gsrlimit=5&redirects=1"
                + "&prop=images&imlimit=max&format=json&formatversion=2&maxlag=5"
                + $"&gsrsearch={Uri.EscapeDataString(game.Name + " video game")}";
            using var search = await GetMediaWikiJsonAsync(
                BuildQueryUri(endpoint, searchQuery),
                cancellationToken).ConfigureAwait(false);
            if (search is null)
            {
                return new ArtworkResult(null, ArtworkOrigin.None, null);
            }

            var imageTitle = FindExactCoverImage(search.RootElement, game.Name);
            if (imageTitle is null)
            {
                MarkMissing(missingPath);
                return new ArtworkResult(null, ArtworkOrigin.None, null);
            }

            var imageQuery =
                "action=query&prop=imageinfo&iiprop=url%7Cmime%7Csize"
                + "&format=json&formatversion=2&maxlag=5"
                + $"&titles={Uri.EscapeDataString(imageTitle)}";
            using var imageDocument = await GetMediaWikiJsonAsync(
                BuildQueryUri(endpoint, imageQuery),
                cancellationToken).ConfigureAwait(false);
            var imageUri = imageDocument is null
                ? null
                : FindSafePortraitImage(imageDocument.RootElement, imageHost);
            if (imageUri is null)
            {
                MarkMissing(missingPath);
                return new ArtworkResult(null, ArtworkOrigin.None, null);
            }

            var bytes = await DownloadImageAsync(imageUri, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return new ArtworkResult(null, ArtworkOrigin.None, null);
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = cachedPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                await _imageProcessor.SavePortraitAsync(
                    bytes.Value,
                    temporaryPath,
                    maximumWidth: 400,
                    maximumHeight: 600,
                    cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, cachedPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            ClearMarker(missingPath);
            return new ArtworkResult(cachedPath, ArtworkOrigin.MediaWiki, null);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException)
        {
            return new ArtworkResult(
                null,
                ArtworkOrigin.None,
                null,
                $"Fallback artwork failed: {exception.Message}");
        }
        finally
        {
            _mediaWikiGate.Release();
        }
    }

    private async Task<JsonDocument?> GetMediaWikiJsonAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        var delay = _lastMediaWikiRequest + _minimumMediaWikiInterval
            - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("DLSS-Swapper-LLE-Linux/1.0");
        _lastMediaWikiRequest = DateTimeOffset.UtcNow;
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ReadOnlyMemory<byte>?> DownloadImageAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode
            || response.Content.Headers.ContentLength > MaximumCoverBytes)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null
            && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > MaximumCoverBytes)
            {
                throw new InvalidDataException("Artwork exceeds the 15 MiB limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }

        return destination.ToArray();
    }

    private static IEnumerable<string> GetSteamLocalCandidates(
        SelectedGame game,
        string appId,
        IReadOnlyList<SteamGame> knownSteamGames)
    {
        var roots = knownSteamGames
            .Select(steamGame => steamGame.LibraryRoot)
            .Append(FindSteamLibraryRoot(game.RootPath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(PathComparers.FileSystemPath);
        foreach (var root in roots)
        {
            var cache = Path.Combine(root!, "appcache", "librarycache");
            yield return Path.Combine(cache, $"{appId}_library_600x900.jpg");
            yield return Path.Combine(cache, appId, "library_600x900.jpg");
        }
    }

    private string GetLookupDirectory(string installPath)
    {
        var libraryRoot = FindSteamLibraryRoot(installPath);
        if (libraryRoot is not null)
        {
            var parent = Directory.GetParent(libraryRoot)?.FullName;
            if (parent is not null)
            {
                var shared = Path.Combine(parent, "DLSS Swapper LLE Artwork Cache");
                try
                {
                    Directory.CreateDirectory(shared);
                    return shared;
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException)
                {
                    // Fall through to the XDG cache.
                }
            }
        }

        Directory.CreateDirectory(_cacheRoot);
        return _cacheRoot;
    }

    private static string? FindSteamLibraryRoot(string installPath)
    {
        var current = new DirectoryInfo(installPath);
        while (current is not null)
        {
            if (current.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                return current.Parent?.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static Uri BuildQueryUri(string endpoint, string query) =>
        new UriBuilder(endpoint) { Query = query }.Uri;

    private static string? FindExactCoverImage(JsonElement root, string title)
    {
        if (!root.TryGetProperty("query", out var query)
            || !query.TryGetProperty("pages", out var pages)
            || pages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var normalizedTitle = NormalizeArticleTitle(title);
        var pageList = pages.EnumerateArray().ToArray();
        var page = pageList.FirstOrDefault(candidate =>
            candidate.TryGetProperty("title", out var candidateTitle)
            && NormalizeArticleTitle(candidateTitle.GetString() ?? string.Empty) == normalizedTitle);
        if (page.ValueKind == JsonValueKind.Undefined
            && query.TryGetProperty("redirects", out var redirects)
            && redirects.ValueKind == JsonValueKind.Array)
        {
            var redirect = redirects.EnumerateArray().FirstOrDefault(candidate =>
                candidate.TryGetProperty("from", out var from)
                && NormalizeArticleTitle(from.GetString() ?? string.Empty) == normalizedTitle);
            if (redirect.ValueKind != JsonValueKind.Undefined
                && redirect.TryGetProperty("to", out var target))
            {
                var normalizedTarget = NormalizeArticleTitle(target.GetString() ?? string.Empty);
                page = pageList.FirstOrDefault(candidate =>
                    candidate.TryGetProperty("title", out var candidateTitle)
                    && NormalizeArticleTitle(candidateTitle.GetString() ?? string.Empty)
                        == normalizedTarget);
            }
        }

        if (page.ValueKind == JsonValueKind.Undefined
            || !page.TryGetProperty("images", out var images)
            || images.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return images.EnumerateArray()
            .Select(image => image.TryGetProperty("title", out var imageTitle)
                ? imageTitle.GetString()
                : null)
            .Where(imageTitle => imageTitle is not null && IsCoverName(imageTitle))
            .OrderBy(imageTitle => CoverNameScore(imageTitle!))
            .ThenBy(imageTitle => imageTitle!.Length)
            .FirstOrDefault();
    }

    private static Uri? FindSafePortraitImage(JsonElement root, string allowedHost)
    {
        if (!root.TryGetProperty("query", out var query)
            || !query.TryGetProperty("pages", out var pages)
            || pages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("imageinfo", out var imageInfo)
                || imageInfo.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var info in imageInfo.EnumerateArray())
            {
                var width = GetInt32(info, "width");
                var height = GetInt32(info, "height");
                var size = GetInt64(info, "size");
                var mime = GetString(info, "mime");
                var url = GetString(info, "url");
                if (width < 200
                    || height < 200
                    || height <= width
                    || height > width * 2
                    || size <= 0
                    || size > MaximumCoverBytes
                    || !mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps
                    || !uri.Host.Equals(allowedHost, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return uri;
            }
        }

        return null;
    }

    private static string NormalizeArticleTitle(string title)
    {
        const string suffix = " (video game)";
        var value = title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? title[..^suffix.Length]
            : title;
        value = Regex.Replace(
            value,
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
        return NormalizeTitle(value);
    }

    private static bool IsCoverName(string value)
    {
        var normalized = NormalizeTitle(value);
        return normalized.Contains("cover", StringComparison.Ordinal)
            || normalized.Contains("boxart", StringComparison.Ordinal)
            || normalized.Contains("poster", StringComparison.Ordinal)
            || normalized.Contains("keyart", StringComparison.Ordinal);
    }

    private static int CoverNameScore(string value)
    {
        var normalized = NormalizeTitle(value);
        if (normalized.Contains("cover", StringComparison.Ordinal))
        {
            return 0;
        }

        if (normalized.Contains("boxart", StringComparison.Ordinal))
        {
            return 1;
        }

        return normalized.Contains("poster", StringComparison.Ordinal) ? 2 : 3;
    }

    private static string GetLookupKey(string title)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeTitle(title)));
        return Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant();
    }

    private static string GetSourceKey(string endpoint, string host)
    {
        if (endpoint.Equals(
                LinuxLibraryState.DefaultMediaWikiApiEndpoint,
                StringComparison.OrdinalIgnoreCase)
            && host.Equals(
                LinuxLibraryState.DefaultMediaWikiImageHost,
                StringComparison.OrdinalIgnoreCase))
        {
            return "wikipedia";
        }

        return $"mediawiki_{GetLookupKey($"{endpoint}|{host}")[..16]}";
    }

    private static bool HasRecentMarker(string path)
    {
        try
        {
            return File.Exists(path)
                && File.GetLastWriteTimeUtc(path) >= DateTime.UtcNow.Subtract(MissingRetryInterval);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void MarkMissing(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed marker leaves the lookup retryable.
        }
    }

    private static void ClearMarker(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The cached cover still wins over a stale marker.
        }
    }

    private static async Task WriteAtomicAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes.ToArray(), cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static int GetInt32(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;

    private static long GetInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var result)
            ? result
            : 0;

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
