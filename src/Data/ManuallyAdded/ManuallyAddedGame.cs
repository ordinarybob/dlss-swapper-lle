using System;
using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using SQLite;

namespace DLSS_Swapper.Data.ManuallyAdded;

[Table("manually_added_game")]
public class ManuallyAddedGame : Game
{
    public override GameLibrary GameLibrary => GameLibrary.ManuallyAdded;

    public override bool IsReadyToPlay => true;


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

    protected override Task UpdateCacheImageAsync()
    {
        // NOOP, the image is manually managed by the user.
        return Task.CompletedTask;
    }

    public override bool UpdateFromGame(Game game)
    {
        var didChange = ParentUpdateFromGame(game);

        /*
        if (game is ManuallyAddedGame manuallyAddedGame)
        {

        }
        */

        return didChange;
    }
}
