using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace DLSS_Swapper.Data.NVIDIA;

[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://s3.amazonaws.com/doc/2006-03-01/", IsNullable = false)]
public class ListBucketResult
{
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string Marker { get; set; } = string.Empty;
    public string NextMarker { get; set; } = string.Empty;
    public int MaxKeys { get; set; }
    public bool IsTruncated { get; set; }

    [System.Xml.Serialization.XmlElementAttribute("Contents")]
    public ListBucketResultContents[] Contents { get; set; } = [];

    internal static async Task<List<ListBucketResultContents>> ReadAllAsync(
        string url, Func<string, Stream, CancellationToken, Task> download,
        CancellationToken cancellationToken)
    {
        var contents = new List<ListBucketResultContents>();
        var markers = new HashSet<string>(StringComparer.Ordinal);
        var nextUrl = url;
        var serializer = new XmlSerializer(typeof(ListBucketResult));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream();
            await download(nextUrl, stream, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = 0;
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            var page = serializer.Deserialize(reader) as ListBucketResult
                ?? throw new InvalidDataException("Missing NVIDIA file listing.");
            contents.AddRange(page.Contents);
            if (!page.IsTruncated) return contents;
            var marker = !string.IsNullOrEmpty(page.NextMarker)
                ? page.NextMarker : page.Contents.LastOrDefault()?.Key;
            if (string.IsNullOrEmpty(marker) || !markers.Add(marker))
                throw new InvalidDataException("NVIDIA file listing did not advance to the next page.");
            nextUrl = url + "?marker=" + Uri.EscapeDataString(marker);
        }
    }
}
