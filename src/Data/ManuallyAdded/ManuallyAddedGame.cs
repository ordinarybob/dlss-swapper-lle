using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DLSS_Swapper.Data.Steam;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using SQLite;

namespace DLSS_Swapper.Data.ManuallyAdded;

[Table("manually_added_game")]
public class ManuallyAddedGame : Game
{
    public override GameLibrary GameLibrary => GameLibrary.ManuallyAdded;

    public override bool IsReadyToPlay => true;

    [Column("steam_app_id")]
    public string? SteamAppId { get; set; }
    [Column("launch_executable")]
    public string? LaunchExecutable { get; set; }
    [Column("launch_arguments")]
    public string? LaunchArguments { get; set; }
    [Column("launch_working_directory")]
    public string? LaunchWorkingDirectory { get; set; }


    public ManuallyAddedGame()
    {

    }
    public ManuallyAddedGame(string id)
    {
        PlatformId = id;
        SetID();
    }

    internal static ManuallyAddedGame CreateForInstallPath(string installPath)
    {
        var normalizedPath = PathHelpers.NormalizePath(installPath);
        return new ManuallyAddedGame(Guid.NewGuid().ToString("D"))
        {
            Title = Path.GetFileName(normalizedPath),
            InstallPath = normalizedPath,
            NeedsProcessing = true,
        };
    }

    public async Task ImportCoverImage(string imagePath)
    {
        using (var fileStream = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await ResizeCoverAsync(fileStream).ConfigureAwait(false);
        }
    }

    protected override async Task UpdateCacheImageAsync()
    {
        var steamAppId = SteamAppId;
        if (string.IsNullOrWhiteSpace(steamAppId))
        {
            steamAppId = await SteamArtworkLookup.ResolveAppIdAsync(Title, InstallPath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(steamAppId) == false)
            {
                SteamAppId = steamAppId;
                await SaveToDatabaseAsync().ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(steamAppId) == false)
        {
            var steamGame = new SteamGame(steamAppId)
            {
                Title = Title,
                InstallPath = InstallPath,
            };
            var steamCoverImagePath = await steamGame.AcquireCoverImagePathAsync().ConfigureAwait(false);
            if (steamCoverImagePath is not null)
            {
                UseLocalCoverImage(steamCoverImagePath);
                return;
            }
        }

        var cachedFallbackCover = WikipediaArtworkLookup.FindCachedCover(Title, InstallPath);
        if (cachedFallbackCover is not null)
        {
            UseLocalCoverImage(cachedFallbackCover);
            return;
        }

        var fallbackCoverUrl = await WikipediaArtworkLookup.ResolveCoverUrlAsync(
            Title,
            InstallPath).ConfigureAwait(false);
        if (fallbackCoverUrl is null)
        {
            return;
        }

        if (await DownloadFallbackCoverAsync(fallbackCoverUrl).ConfigureAwait(false)
            && File.Exists(ExpectedCoverImage))
        {
            var fallbackCoverPath = WikipediaArtworkLookup.PersistCover(
                Title,
                InstallPath,
                ExpectedCoverImage);
            UseLocalCoverImage(fallbackCoverPath);
        }
    }

    async Task<bool> DownloadFallbackCoverAsync(string coverUrl)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, coverUrl);
            request.Headers.UserAgent.ParseAdd("DLSS-Swapper-LLE/1.0");
            using var response = await App.CurrentApp.HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var imageStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await ResizeCoverAsync(imageStream).ConfigureAwait(false);
            return File.Exists(ExpectedCoverImage);
        }
        catch (Exception err)
        {
            Logger.Warning($"Unable to download fallback artwork for '{Title}'. {err.Message}");
            return false;
        }
    }

    public override bool UpdateFromGame(Game game)
    {
        var didChange = ParentUpdateFromGame(game);

        if (game is ManuallyAddedGame manuallyAddedGame)
        {
            if (SteamAppId != manuallyAddedGame.SteamAppId)
            {
                SteamAppId = manuallyAddedGame.SteamAppId;
                didChange = true;
            }
        }

        return didChange;
    }
}
