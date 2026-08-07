using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data.ManuallyAdded;

internal sealed class WikipediaResponse
{
    [JsonPropertyName("query")]
    public WikipediaQuery? Query { get; set; }
}

internal sealed class WikipediaQuery
{
    [JsonPropertyName("pages")]
    public List<WikipediaPage> Pages { get; set; } = [];

    [JsonPropertyName("redirects")]
    public List<WikipediaRedirect> Redirects { get; set; } = [];
}

internal sealed class WikipediaPage
{
    [JsonPropertyName("pageid")]
    public int PageId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<WikipediaImageReference> Images { get; set; } = [];

    [JsonPropertyName("imageinfo")]
    public List<WikipediaImageInfo> ImageInfo { get; set; } = [];
}

internal sealed class WikipediaRedirect
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
}

internal sealed class WikipediaImageReference
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

internal sealed class WikipediaImageInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("mime")]
    public string Mime { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
