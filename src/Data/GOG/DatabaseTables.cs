using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SQLite;

namespace DLSS_Swapper.Data.GOG;

internal class LimitedDetail
{
    [Column("id")]
    public int Id { get; set; }

    [Column("productId")]
    public int ProductId { get; set; }

    [Column("languageId")]
    public int LanguageId { get; set; }

    [Column("is_production")]
    public bool IsProduction { get; set; }

    [Column("stored_at")]
    public long StoredAt { get; set; } // Datetime?

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("links")]
    public string Links { get; set; } = string.Empty;

    [Column("images")]
    public string Images { get; set; } = string.Empty;

    [Column("ProductDetailsResponseId")]
    public int ProductDetailsResponseId { get; set; }

    LimitedDetailImages? _imagesData;
    [SQLite.Ignore]
    public LimitedDetailImages? ImagesData
    {
        get
        {
            if (_imagesData is null && string.IsNullOrEmpty(Images) == false)
            {
                // Ensure a failed deserialization does not crash the app.
                try
                {
                    _imagesData = JsonSerializer.Deserialize(Images, SourceGenerationContext.Default.LimitedDetailImages);
                }
                catch (Exception err)
                {
                    Logger.Error(err, $"Error deserialzing GOG images data for {Title}");
                }
            }

            return _imagesData;
        }
    }

    internal class LimitedDetailImages
    {
        [JsonPropertyName("background")]
        public string Background { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("logo")]
        public string Logo { get; set; } = string.Empty;

        [JsonPropertyName("logo2x")]
        public string Logo2x { get; set; } = string.Empty;

        [JsonPropertyName("menuNotificationAv")]
        public string MenuNotificationAv { get; set; } = string.Empty;

        [JsonPropertyName("menuNotificationAv2")]
        public string MenuNotificationAv2 { get; set; } = string.Empty;

        [JsonPropertyName("sidebarIcon")]
        public string SidebarIcon { get; set; } = string.Empty;

        [JsonPropertyName("sidebarIcon2x")]
        public string SidebarIcon2x { get; set; } = string.Empty;
    }
}

internal class WebCache
{
    [Column("id")]
    public int Id { get; set; }

    [Column("releaseKey")]
    public string ReleaseKey { get; set; } = string.Empty;

    [Column("userId")]
    public long UserId { get; set; }
}

internal class WebCacheResource
{
    [Column("webCacheId")]
    public int WebCacheId { get; set; }

    [Column("webCacheResourceTypeId")]
    public int WebCacheResourceTypeId { get; set; }

    [Column("filename")]
    public string Filename { get; set; } = string.Empty;
}

internal class WebCacheResourceType
{
    [Column("id")]
    public int Id { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;
}

internal class GamePieceType
{
    [Column("id")]
    public int Id { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;
}

internal class GamePiece
{
    [Column("releaseKey")]
    public string ReleaseKey { get; set; } = string.Empty;

    [Column("gamePieceTypeId")]
    public int GamePieceTypeId { get; set; }

    [Column("userId")]
    public long UserId { get; set; }

    [Column("value")]
    public string Value { get; set; } = string.Empty;

    internal class GamePieceOriginalImages
    {
        [JsonPropertyName("background")]
        public string Background { get; set; } = string.Empty;

        [JsonPropertyName("squareIcon")]
        public string SquareIcon { get; set; } = string.Empty;

        [JsonPropertyName("verticalCover")]
        public string VerticalCover { get; set; } = string.Empty;
    }

    internal GamePieceOriginalImages? GetValueAsOriginalImages()
    {
        return JsonSerializer.Deserialize(Value, SourceGenerationContext.Default.GamePieceOriginalImages);
    }
}

internal class ResourceImages
{
    [JsonPropertyName("images\\\\background")]
    public string Background { get; set; } = string.Empty;

    [JsonPropertyName("images\\\\logo")]
    public string Logo { get; set; } = string.Empty;

    [JsonPropertyName("images\\\\logo2x")]
    public string Logo2x { get; set; } = string.Empty;

    [JsonPropertyName("images\\\\icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("images\\\\sidebarIcon")]
    public string SidebarIcon { get; set; } = string.Empty;

    [JsonPropertyName("images\\\\sidebarIcon2x")]
    public string SidebarIcon2x { get; set; } = string.Empty;

    [JsonPropertyName("images\\\\menuNotificationAv")]
    public string MenuNotificationAv { get; set; } = string.Empty;

    [JsonPropertyName("images\\\\menuNotificationAv2")]
    public string MenuNotificationAv2 { get; set; } = string.Empty;
}
