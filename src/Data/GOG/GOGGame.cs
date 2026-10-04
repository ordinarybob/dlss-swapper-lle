using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using SQLite;

namespace DLSS_Swapper.Data.GOG;

[Table("gog_game")]
internal class GOGGame : Game
{
    public override GameLibrary GameLibrary => GameLibrary.GOG;

    public override bool IsReadyToPlay => true;

    [Ignore]
    public List<string> PotentialLocalHeaders { get; } = new List<string>();

    [Column("fallback_header_url")]
    public string FallbackHeaderUrl { get; set; } = string.Empty;

    public GOGGame()
    {

    }

    public GOGGame(string gameId)
    {
        PlatformId = gameId;
        SetID();
    }

    protected override async Task UpdateCacheImageAsync()
    {
        foreach (var potentialLocalHeader in PotentialLocalHeaders)
        {
            if (File.Exists(potentialLocalHeader))
            {
                using (var fileStream = File.Open(potentialLocalHeader, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await ResizeCoverAsync(fileStream).ConfigureAwait(false);
                }
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(FallbackHeaderUrl) == false)
        {
            await DownloadCoverAsync(FallbackHeaderUrl).ConfigureAwait(false);
            return;
        }

        // Prefer catalog box art; the product endpoint provides fallback images.

        try
        {
            var url = "https://catalog.gog.com/v1/catalog?order=desc:score&productType=in:game&query=like:" + Uri.EscapeDataString(Title);
            var fileDownloader = new FileDownloader(url);
            using (var memoryStream = new MemoryStream())
            {
                await fileDownloader.DownloadFileToStreamAsync(memoryStream);
                memoryStream.Position = 0;

                var catalogResponse = await JsonSerializer.DeserializeAsync(
                    memoryStream,
                    SourceGenerationContext.Default.GOGCatalogResponse).ConfigureAwait(false);
                if (catalogResponse is null)
                {
                    throw new Exception($"Could not deserialize GOGCatalogResponse for url, {url}");
                }

                if (catalogResponse.Products.Length == 0)
                {
                    throw new Exception($"Could not find any GOGCatalogProduct for url, {url}");
                }

                foreach (var product in catalogResponse.Products)
                {
                    if (product.Id.Equals(PlatformId, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(product.CoverVertical) == false)
                    {
                        await DownloadCoverAsync(product.CoverVertical).ConfigureAwait(false);
                        return;
                    }
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }

        try
        {
            var url = "https://api.gog.com/products/" + PlatformId;
            var fileDownloader = new FileDownloader(url);

            using (var memoryStream = new MemoryStream())
            {
                await fileDownloader.DownloadFileToStreamAsync(memoryStream);

                memoryStream.Position = 0;

                var gogProduct = await JsonSerializer.DeserializeAsync(
                    memoryStream,
                    SourceGenerationContext.Default.GOGProduct).ConfigureAwait(false);

                if (gogProduct?.Images is not null)
                {
                    if (string.IsNullOrWhiteSpace(gogProduct.Images.Logo) == false)
                    {
                        var newCoverUrl = $"https:{gogProduct.Images.Logo.Replace("glx_logo", "glx_vertical_cover")}";
                        await DownloadCoverAsync(newCoverUrl).ConfigureAwait(false);
                        return;
                    }
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
            DebuggerHelper.BreakIfAttached();
        }

    }

    public override bool UpdateFromGame(Game game)
    {
        var didChange = ParentUpdateFromGame(game);

        if (game is GOGGame gogGame)
        {
            if (FallbackHeaderUrl != gogGame.FallbackHeaderUrl)
            {
                FallbackHeaderUrl = gogGame.FallbackHeaderUrl;
                didChange = true;
            }
        }

        return didChange;
    }
}
