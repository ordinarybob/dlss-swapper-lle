using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DLSS_Swapper.Data.Steam.SteamAPI;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data.Steam;

internal static class SteamArtworkLookup
{
    static readonly TimeSpan MissingLookupRetryInterval = TimeSpan.FromDays(7);

    internal static async Task<string?> ResolveAppIdAsync(string title, string installPath)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalizedInstallPath = PathHelpers.NormalizePath(installPath);
        var loadedSteamGame = GameManager.Instance
            .GetGames<SteamGame>()
            .FirstOrDefault(game => game.InstallPath.Equals(
                normalizedInstallPath,
                StringComparison.OrdinalIgnoreCase));
        if (loadedSteamGame is not null)
        {
            PersistResolvedAppId(title, installPath, loadedSteamGame.PlatformId);
            return loadedSteamGame.PlatformId;
        }

        var persistedAppId = ReadPersistedAppId(title, installPath);
        if (persistedAppId is not null)
        {
            return persistedAppId;
        }

        var manifestAppId = SteamLibrary.TryResolveAppIdFromInstallPath(normalizedInstallPath);
        if (manifestAppId is not null)
        {
            PersistResolvedAppId(title, installPath, manifestAppId);
            return manifestAppId;
        }

        if (HasRecentMissingLookupMarker(title, installPath))
        {
            return null;
        }

        try
        {
            SteamStoreSearchResponse? searchResponse = null;
            foreach (var searchTitle in GetSearchTitles(title))
            {
                var encodedTitle = Uri.EscapeDataString(searchTitle);
                using var response = await App.CurrentApp.HttpClient.GetAsync(
                    $"https://store.steampowered.com/api/storesearch/?term={encodedTitle}&l=english&cc=US",
                    System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (response.IsSuccessStatusCode == false)
                {
                    Logger.Warning($"Steam store search for manually added game '{title}' returned {response.StatusCode}.");
                    return null;
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                searchResponse = JsonSerializer.Deserialize(
                    responseStream,
                    SourceGenerationContext.Default.SteamStoreSearchResponse);
                if (searchResponse?.Items.Count > 0)
                {
                    break;
                }
            }

            var normalizedTitle = NormalizeTitle(title);
            var exactMatch = searchResponse?.Items.FirstOrDefault(item =>
                NormalizeTitle(item.Name).Equals(normalizedTitle, StringComparison.Ordinal));
            exactMatch ??= searchResponse?.Items
                .Where(item => NormalizeTitle(item.Name).StartsWith(normalizedTitle, StringComparison.Ordinal))
                .OrderBy(item => NormalizeTitle(item.Name).Length)
                .FirstOrDefault();
            if (exactMatch is null || exactMatch.Id <= 0)
            {
                MarkLookupUnavailable(title, installPath);
                return null;
            }

            var appId = exactMatch.Id.ToString(CultureInfo.InvariantCulture);
            PersistResolvedAppId(title, installPath, appId);
            return appId;
        }
        catch (Exception err)
        {
            // A transient network or parsing failure must not become a negative
            // cache entry. Only a successful search with no exact match is held.
            Logger.Warning($"Unable to search Steam artwork for manually added game '{title}'. {err.Message}");
            return null;
        }
    }

    internal static string NormalizeTitle(string title)
    {
        var decomposed = title.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (character is 'Δ' or 'δ')
                {
                    result.Append("delta");
                    continue;
                }

                result.Append(char.ToLowerInvariant(character));
            }
        }
        return result.ToString();
    }

    static string[] GetSearchTitles(string title)
    {
        var wordsOnly = new string(title
            .Select(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)
                ? character
                : ' ')
            .ToArray());
        wordsOnly = string.Join(' ', wordsOnly.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));

        var deltaAlias = wordsOnly.Replace(
            " DELTA",
            " Δ",
            StringComparison.OrdinalIgnoreCase);
        return deltaAlias.Equals(wordsOnly, StringComparison.Ordinal)
            ? [wordsOnly]
            : [wordsOnly, deltaAlias];
    }

    internal static string GetLookupKey(string title)
    {
        var normalizedTitle = NormalizeTitle(title);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedTitle));
        return Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant();
    }

    internal static string GetLookupDirectory(string installPath)
    {
        return SteamGame.GetSharedArtworkCacheDirectory(installPath)
            ?? Storage.GetImageCachePath();
    }

    static string GetMappingPath(string title, string installPath)
    {
        return Path.Combine(
            GetLookupDirectory(installPath),
            $"manual_{GetLookupKey(title)}.steam-appid");
    }

    static string GetMissingLookupMarkerPath(string title, string installPath)
    {
        return Path.Combine(
            GetLookupDirectory(installPath),
            $"manual_{GetLookupKey(title)}.steam-lookup.missing");
    }

    static string? ReadPersistedAppId(string title, string installPath)
    {
        var mappingPath = GetMappingPath(title, installPath);
        try
        {
            if (File.Exists(mappingPath) == false)
            {
                return null;
            }

            var appId = File.ReadAllText(mappingPath).Trim();
            return uint.TryParse(appId, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? appId
                : null;
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to read manual Steam artwork mapping {mappingPath}. {err.Message}");
            return null;
        }
    }

    static void PersistResolvedAppId(string title, string installPath, string appId)
    {
        var mappingPath = GetMappingPath(title, installPath);
        var missingPath = GetMissingLookupMarkerPath(title, installPath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mappingPath)!);
            File.WriteAllText(mappingPath, appId);
            if (File.Exists(missingPath))
            {
                File.Delete(missingPath);
            }
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to persist manual Steam artwork mapping {mappingPath}. {err.Message}");
        }
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
            Logger.Warning($"Unable to read manual Steam artwork marker {markerPath}. {err.Message}");
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
            Logger.Warning($"Unable to persist manual Steam artwork marker {markerPath}. {err.Message}");
        }
    }
}
