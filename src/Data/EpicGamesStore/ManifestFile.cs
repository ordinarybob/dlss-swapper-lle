using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data.EpicGamesStore;

internal class ManifestFile
{

    [JsonPropertyName("FormatVersion")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("AppCategories")]
    public string[] AppCategories { get; set; } = System.Array.Empty<string>();

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("InstallLocation")]
    public string InstallLocation { get; set; } = string.Empty;

    [JsonPropertyName("CatalogItemId")]
    public string CatalogItemId { get; set; } = string.Empty;

    [JsonPropertyName("AppName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("MainGameAppName")]
    public string MainGameAppName { get; set; } = string.Empty;

}
