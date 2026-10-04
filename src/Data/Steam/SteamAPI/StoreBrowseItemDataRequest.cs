using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data.Steam.SteamAPI;

internal class StoreBrowseItemDataRequest
{
    [JsonPropertyName("include_assets")]
    public bool IncludeAssets { get; set; }

}
