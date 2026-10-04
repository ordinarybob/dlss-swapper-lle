using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data.Steam.SteamAPI;

internal class SteamStoreItem
{

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("assets")]
    public SteamStoreItemAssets? Assets { get; set; }

}
