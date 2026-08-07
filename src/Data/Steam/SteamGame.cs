using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data.Steam.SteamAPI;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using SQLite;

namespace DLSS_Swapper.Data.Steam;

[Table("steam_game")]
internal partial class SteamGame : Game
{
    internal const string SharedArtworkCacheDirectoryName = "DLSS Swapper LLE Artwork Cache";
    static readonly TimeSpan MissingArtworkRetryInterval = TimeSpan.FromDays(7);

    public override GameLibrary GameLibrary => GameLibrary.Steam;

    string PortableDirectCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_600_900.jpg");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadyToPlay))]
    [Column("state_flags")]
    public partial SteamStateFlag StateFlags { get; set; }

    public override bool IsReadyToPlay
    {
        get
        {
            const SteamStateFlag allowedFlags = SteamStateFlag.StateFullyInstalled | SteamStateFlag.StateAppRunning;
            return StateFlags != 0 && (StateFlags & ~allowedFlags) == 0;
        }
    }

    public SteamGame()
    {

    }

    public SteamGame(string appId)
    {
        PlatformId = appId;
        SetID();
    }

    internal async Task<string?> AcquireCoverImagePathAsync()
    {
        await LoadCoverImageAsync().ConfigureAwait(false);
        return FindLocalCoverImage()
            ?? (File.Exists(ExpectedCoverImage) ? ExpectedCoverImage : null);
    }

    protected override async Task UpdateCacheImageAsync()
    {
        // Prefer artwork already held by Steam or the persistent cache beside a
        // standalone SteamLibrary. Either avoids first-init network work.
        var localHeaderImagePath = FindLocalCoverImage();
        if (localHeaderImagePath is not null)
        {
            UseLocalCoverImage(localHeaderImagePath);
            return;
        }

        if (HasRecentMissingArtworkMarker())
        {
            return;
        }

        // Special case for Steamworks redistributable. 
        if (PlatformId == "228980")
        {
            if (await DownloadCoverAsync($"https://steamcdn-a.akamaihd.net/steam/apps/{PlatformId}/header.jpg").ConfigureAwait(false))
            {
                PersistPortableCoverToSharedCache();
            }
            else
            {
                MarkArtworkUnavailable();
            }
            return;            
        }

        // The conventional portrait URL needs one request and covers nearly all
        // Steam titles. Use the metadata service only for the exceptions.
        var didDownload = await DownloadDirectCoverAsync($"https://steamcdn-a.akamaihd.net/steam/apps/{PlatformId}/library_600x900.jpg").ConfigureAwait(false);
        if (didDownload == false)
        {
            didDownload = await DownloadCoverFromIStoreBrowseService();
            if (didDownload)
            {
                PersistPortableCoverToSharedCache();
            }

            if (didDownload == false)
            {
                MarkArtworkUnavailable();
                Logger.Error($"Tried to get Steam cover for {PlatformId} but was unable to get it from the direct CDN or store metadata service.");
            }
        }
    }

    async Task<bool> DownloadDirectCoverAsync(string url)
    {
        var directCoverImage = GetDirectCoverDestination();
        var temporaryPath = directCoverImage + ".download";
        try
        {
            using (var fileStream = File.Create(temporaryPath))
            {
                var fileDownloader = new FileDownloader(url, 0);
                await fileDownloader.DownloadFileToStreamAsync(fileStream).ConfigureAwait(false);
            }

            File.Move(temporaryPath, directCoverImage, true);
            ClearMissingArtworkMarker();
            UseLocalCoverImage(directCoverImage);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    string? FindLocalCoverImage()
    {
        var steamInstallPath = SteamLibrary.GetInstallPath();
        if (string.IsNullOrWhiteSpace(steamInstallPath) == false)
        {
            var steamArtworkCache = Path.Combine(steamInstallPath, "appcache", "librarycache");
            var conventionalCover = Path.Combine(steamArtworkCache, $"{PlatformId}_library_600x900.jpg");
            if (File.Exists(conventionalCover))
            {
                return conventionalCover;
            }

            var nestedCover = Path.Combine(steamArtworkCache, PlatformId, "library_600x900.jpg");
            if (File.Exists(nestedCover))
            {
                return nestedCover;
            }
        }

        var sharedArtworkCache = GetSharedArtworkCacheDirectory(InstallPath);
        if (sharedArtworkCache is not null)
        {
            var sharedCover = Path.Combine(sharedArtworkCache, $"{PlatformId}_library_600x900.jpg");
            if (File.Exists(sharedCover))
            {
                return sharedCover;
            }

            var sharedFallbackCover = Path.Combine(sharedArtworkCache, $"{PlatformId}_library_600x900.png");
            if (File.Exists(sharedFallbackCover))
            {
                return sharedFallbackCover;
            }
        }

        if (File.Exists(PortableDirectCoverImage))
        {
            return PortableDirectCoverImage;
        }

        return null;
    }

    string GetDirectCoverDestination()
    {
        var sharedArtworkCache = GetSharedArtworkCacheDirectory(InstallPath);
        if (sharedArtworkCache is null)
        {
            return PortableDirectCoverImage;
        }

        try
        {
            Directory.CreateDirectory(sharedArtworkCache);
            return Path.Combine(sharedArtworkCache, $"{PlatformId}_library_600x900.jpg");
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to use shared Steam artwork cache {sharedArtworkCache}; using the portable cache instead. {err.Message}");
            return PortableDirectCoverImage;
        }
    }

    void PersistPortableCoverToSharedCache()
    {
        if (File.Exists(ExpectedCoverImage) == false)
        {
            return;
        }

        var sharedArtworkCache = GetSharedArtworkCacheDirectory(InstallPath);
        if (sharedArtworkCache is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(sharedArtworkCache);
            var sharedCover = Path.Combine(sharedArtworkCache, $"{PlatformId}_library_600x900.png");
            File.Copy(ExpectedCoverImage, sharedCover, true);
            ClearMissingArtworkMarker();
            UseLocalCoverImage(sharedCover);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to persist Steam artwork in shared cache {sharedArtworkCache}; keeping the portable copy. {err.Message}");
        }
    }

    string? GetMissingArtworkMarkerPath()
    {
        var sharedArtworkCache = GetSharedArtworkCacheDirectory(InstallPath);
        return sharedArtworkCache is null
            ? null
            : Path.Combine(sharedArtworkCache, $"{PlatformId}_library_600x900.missing");
    }

    bool HasRecentMissingArtworkMarker()
    {
        var markerPath = GetMissingArtworkMarkerPath();
        if (markerPath is null || File.Exists(markerPath) == false)
        {
            return false;
        }

        try
        {
            return File.GetLastWriteTimeUtc(markerPath) >= DateTime.UtcNow.Subtract(MissingArtworkRetryInterval);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to read Steam artwork marker {markerPath}. {err.Message}");
            return false;
        }
    }

    void MarkArtworkUnavailable()
    {
        var markerPath = GetMissingArtworkMarkerPath();
        if (markerPath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            File.WriteAllText(markerPath, string.Empty);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to persist Steam artwork marker {markerPath}. {err.Message}");
        }
    }

    void ClearMissingArtworkMarker()
    {
        var markerPath = GetMissingArtworkMarkerPath();
        if (markerPath is null || File.Exists(markerPath) == false)
        {
            return;
        }

        try
        {
            File.Delete(markerPath);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to clear Steam artwork marker {markerPath}. {err.Message}");
        }
    }

    internal static string? GetSharedArtworkCacheDirectory(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        try
        {
            DirectoryInfo? currentDirectory = new DirectoryInfo(installPath);
            while (currentDirectory is not null
                && currentDirectory.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase) == false)
            {
                currentDirectory = currentDirectory.Parent;
            }

            var libraryDirectory = currentDirectory?.Parent;
            if (libraryDirectory is null
                || libraryDirectory.Name.Equals("SteamLibrary", StringComparison.OrdinalIgnoreCase) == false)
            {
                return null;
            }

            var volumeRoot = Path.GetPathRoot(libraryDirectory.FullName);
            var libraryParent = libraryDirectory.Parent?.FullName;
            if (string.IsNullOrWhiteSpace(volumeRoot)
                || string.IsNullOrWhiteSpace(libraryParent)
                || Path.TrimEndingDirectorySeparator(volumeRoot).Equals(
                    Path.TrimEndingDirectorySeparator(libraryParent),
                    StringComparison.OrdinalIgnoreCase) == false)
            {
                return null;
            }

            return Path.Combine(volumeRoot, SharedArtworkCacheDirectoryName);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to resolve a shared Steam artwork cache for {installPath}. {err.Message}");
            return null;
        }
    }

    async Task<bool> DownloadCoverFromIStoreBrowseService()
    {
        try
        {
            var getItemsInput = new GetItemsInput();
            getItemsInput.Ids.Add(new StoreItemId() { AppId = Int32.Parse(PlatformId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture) });
            getItemsInput.DataRequest.IncludeAssets = true;

            var jsonPayload = JsonSerializer.Serialize(getItemsInput, SourceGenerationContext.Default.GetItemsInput);
            var payloadUrlEncoded = HttpUtility.UrlEncode(jsonPayload);

            using (var steamApiResponse = await App.CurrentApp.HttpClient.GetAsync($"https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json={payloadUrlEncoded}", System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                if (steamApiResponse.IsSuccessStatusCode == false)
                {
                    Logger.Error($"Failed to load Steam cover for {PlatformId} from IStoreBrowseService. Status code: {steamApiResponse.StatusCode}");
                    return false;
                }

                using (var responseStream = await steamApiResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var response = JsonSerializer.Deserialize(responseStream, SourceGenerationContext.Default.SteamAPIResponseGetItemsResponse);
                    if (response?.Response?.StoreItems.Any() == true)
                    {
                        // We are only doing one search, so we likely only care for the first item.
                        var storeItem = response.Response.StoreItems[0];

                        if (storeItem.Assets is null)
                        {
                            Logger.Error($"No Assets found for {PlatformId} in the response from IStoreBrowseService.");
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(storeItem.Assets.AssetUrlFormat))
                        {
                            Logger.Error($"No AssetUrlFormat found for {PlatformId} in the response from IStoreBrowseService.");
                            return false;
                        }

                        // We are only checking LibraryCapsule2x, hopefully it exists for all games
                        if (string.IsNullOrWhiteSpace(storeItem.Assets.LibraryCapsule2x) == false)
                        {
                            // There are 3 different CDNs, I don't lknow what one they will use, so lets try all of them?
                            var cdns = new[]
                            {
                                    "https://shared.fastly.steamstatic.com",
                                    "https://shared.steamstatic.com",
                                    "https://shared.akamai.steamstatic.com"
                                };

                            foreach (var cdn in cdns)
                            {
                                var coverUrl = $"{cdn}/store_item_assets/{storeItem.Assets.AssetUrlFormat.Replace("${FILENAME}", storeItem.Assets.LibraryCapsule2x)}";
                                var didDownloadCover = await DownloadCoverAsync(coverUrl).ConfigureAwait(false);
                                if (didDownloadCover)
                                {
                                    return true;
                                }
                                Logger.Error($"Could not download cover \"{storeItem.Assets.LibraryCapsule2x}\" with CDN {cdn} so trying next.");
                            }
                        }
                    }
                    else
                    {
                        Logger.Error($"No store items found for {PlatformId} in the response from IStoreBrowseService.");
                    }
                }
            }

            Logger.Error($"Tried all known methods to get Steam cover for {PlatformId}, but all had failed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to load Steam cover for {PlatformId} from IStoreBrowseService.");
        }

        return false;
    }

    public override bool UpdateFromGame(Game game)
    {
        var didChange = ParentUpdateFromGame(game);

        if (game is SteamGame steamGame)
        {
            if (StateFlags != steamGame.StateFlags)
            {
                StateFlags = steamGame.StateFlags;
                didChange = true;
            }
        }

        return didChange;
    }
}
