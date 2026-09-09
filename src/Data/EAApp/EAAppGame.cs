using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Interfaces;
using SQLite;
using Windows.Win32;

namespace DLSS_Swapper.Data.EAApp;


[Table("ea_app_game")]
internal class EAAppGame : Game
{
    public override GameLibrary GameLibrary => GameLibrary.EAApp;

    public override bool IsReadyToPlay => true;

    [Column("display_icon_path")]
    public string DisplayIconPath { get; set; } = string.Empty;

    public EAAppGame()
    {
    }

    public EAAppGame(string contentId)
    {
        PlatformId = contentId;
        SetID();
    }

    public override bool UpdateFromGame(Game game)
    {
        var didChange = ParentUpdateFromGame(game);

        if (game is EAAppGame eaAppGame)
        {
            if (DisplayIconPath != eaAppGame.DisplayIconPath)
            {
                DisplayIconPath = eaAppGame.DisplayIconPath;
                didChange = true;
            }
        }
        return didChange;
    }

    static unsafe Windows.Win32.UI.WindowsAndMessaging.HICON ExtractIcon(string path, int index)
    {
        Windows.Win32.UI.WindowsAndMessaging.HICON icon = default;
        fixed (char* fileName = path)
            PInvoke.ExtractIconEx(new Windows.Win32.Foundation.PCWSTR(fileName), index, &icon, null, 1);
        return icon;
    }

    protected override async Task UpdateCacheImageAsync()
    {
        var coverUrl = EAAppLibrary.Instance.SearchForCover(this);

        if (string.IsNullOrWhiteSpace(coverUrl) == false)
        {
            var didDownload = await DownloadCoverAsync(coverUrl).ConfigureAwait(false);
            if (didDownload)
            {
                return;
            }

            Logger.Error($"Unable to download cover for {this.Title} ({coverUrl})");
        }

        // Fall back to using icon from the game exe.
        if (string.IsNullOrWhiteSpace(DisplayIconPath))
        {
            return;
        }
               
        DlssSwapper.Shared.WindowsIconReference iconReference;
        try
        {
            iconReference = DlssSwapper.Shared.WindowsIconReference.Parse(DisplayIconPath);
        }
        catch (FormatException ex)
        {
            Logger.Warning($"Invalid icon reference for {Title}: {ex.Message}");
            return;
        }
        var extension = Path.GetExtension(iconReference.Path);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            using (var memoryStream = new MemoryStream())
            {
                try
                {
                    unsafe
                    {
                        var extractedIcon = ExtractIcon(iconReference.Path, iconReference.Index);
                        if (extractedIcon != IntPtr.Zero)
                        {
                            try
                            {
                                using var icon = Icon.FromHandle(extractedIcon);
                                if (icon is null)
                                {
                                    return;
                                }

                                using (var bitmap = icon.ToBitmap())
                                {
                                    if (bitmap is null)
                                    {
                                        return;
                                    }

                                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                                    memoryStream.Seek(0, SeekOrigin.Begin);

                                }
                            }
                            finally
                            {
                                PInvoke.DestroyIcon(extractedIcon);
                            }
                        }
                        else
                        {
                            Logger.Warning($"Icon not found for {DisplayIconPath} in game {Title}.");
                        }
                    }

                    if (memoryStream.Length == 0)
                    {
                        Logger.Error($"Failed to extract icon from {DisplayIconPath} in game {Title}: Memory stream is empty.");
                        return;
                    }
                        
                    await ResizeCoverAsync(memoryStream).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to extract icon from {DisplayIconPath}: {ex.Message}");
                }
            }
        }
        else if (extension.Equals(".bmp", StringComparison.InvariantCultureIgnoreCase) ||
                extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase) ||
                extension.Equals(".jpg", StringComparison.InvariantCultureIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.InvariantCultureIgnoreCase) ||
                extension.Equals(".webp", StringComparison.InvariantCultureIgnoreCase))
        {
            using (var fileStream = File.OpenRead(iconReference.Path))
            {
                await ResizeCoverAsync(fileStream).ConfigureAwait(false);
            }
        }
        else
        {
            Logger.Error($"Unknown extension {extension} for DisplayIconPath {DisplayIconPath} in game {Title}");
        }
    }
}
