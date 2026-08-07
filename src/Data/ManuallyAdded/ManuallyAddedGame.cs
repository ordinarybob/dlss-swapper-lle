using System;
using System.IO;
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
            if (string.IsNullOrWhiteSpace(steamAppId))
            {
                return;
            }

            SteamAppId = steamAppId;
            await SaveToDatabaseAsync().ConfigureAwait(false);
        }

        var steamGame = new SteamGame(steamAppId)
        {
            Title = Title,
            InstallPath = InstallPath,
        };
        var coverImagePath = await steamGame.AcquireCoverImagePathAsync().ConfigureAwait(false);
        if (coverImagePath is not null)
        {
            UseLocalCoverImage(coverImagePath);
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
