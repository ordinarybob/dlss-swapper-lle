
using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data.Steam.SteamAPI;

internal class StoreItemId
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

}
