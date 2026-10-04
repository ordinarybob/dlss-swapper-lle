
using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data.Steam.SteamAPI;

internal class StoreBrowseContext
{

    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = "US";

}
