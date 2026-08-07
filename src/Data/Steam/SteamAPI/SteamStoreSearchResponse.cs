using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data.Steam.SteamAPI;

internal sealed class SteamStoreSearchResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<SteamStoreSearchItem> Items { get; set; } = [];
}

internal sealed class SteamStoreSearchItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
