using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record NgxServerModel(string Key, DllType Type, string Version, long Size);

public static partial class NgxModelServer
{
    private const string Server = "https://ngx.download.nvidia.com/";

    [GeneratedRegex(@"^d6e9b45e-d4f6-4a84-a460-bf61decae3e8/(dlss|dlssg|dlssd)/versions/(\d+)/files/160_E658700\.bin$")]
    private static partial Regex ModelKey();

    public static async Task<IReadOnlyList<NgxServerModel>> ListAsync(HttpClient http, CancellationToken token, Translations? translations = null)
    {
        string T(string key, string fallback) => translations?.Get(key, fallback) ?? fallback;
        var models = new Dictionary<string, NgxServerModel>();
        string? marker = null;
        var markers = new HashSet<string>();
        do
        {
            using var response = await http.GetAsync(Server + (marker is null ? "" : "?marker=" + Uri.EscapeDataString(marker)),
                HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, token).ConfigureAwait(false);
            var root = document.Root ?? throw new IOException(T("Linux_NgxEmptyList", "NVIDIA returned an empty model list."));
            var ns = root.Name.Namespace;
            if (root.Name.LocalName != "ListBucketResult") throw new IOException(T("Linux_NgxInvalidList", "NVIDIA returned an invalid model list."));
            var entries = root.Elements(ns + "Contents").ToArray();
            foreach (var entry in entries)
            {
                var key = (string?)entry.Element(ns + "Key") ?? "";
                var match = ModelKey().Match(key);
                if (!match.Success || !uint.TryParse(match.Groups[2].Value, out var version)
                    || !long.TryParse((string?)entry.Element(ns + "Size"), out var size) || size <= 0 || size > 512L * 1024 * 1024) continue;
                var type = match.Groups[1].Value switch
                { "dlss" => DllType.Dlss, "dlssg" => DllType.DlssFrameGeneration, _ => DllType.DlssRayReconstruction };
                models[key] = new(key, type, $"{version >> 16}.{(version >> 8) & 255}.{version & 255}", size);
            }
            marker = (string?)root.Element(ns + "IsTruncated") == "true"
                ? (string?)root.Element(ns + "NextMarker") ?? (string?)entries.LastOrDefault()?.Element(ns + "Key") : null;
            if ((string?)root.Element(ns + "IsTruncated") == "true" && (string.IsNullOrEmpty(marker) || !markers.Add(marker)))
                throw new IOException(T("Linux_NgxNextPageFailed", "NVIDIA model listing could not advance to the next page."));
        } while (marker is not null);
        return models.Values.OrderBy(model => model.Type).ThenByDescending(model => Version.Parse(model.Version)).ToArray();
    }

    public static async Task<string> ImportAsync(HttpClient http, NgxServerModel model, DllImportWorkflow workflow, CancellationToken token, Translations? translations = null)
    {
        string T(string key, string fallback) => translations?.Get(key, fallback) ?? fallback;
        if (!ModelKey().IsMatch(model.Key)) throw new IOException(T("Linux_NgxInvalidKey", "Invalid NVIDIA model key."));
        var temporary = Path.GetTempFileName();
        try
        {
            using var response = await http.GetAsync(Server + model.Key, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    if (output.Length + count > model.Size) throw new IOException(T("Linux_NgxDownloadTooLarge", "Model download exceeds its listed size."));
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                }
                if (output.Length != model.Size) throw new IOException(T("Linux_NgxDownloadIncomplete", "Model download is incomplete."));
            }
            return await workflow.ImportModelAsync(temporary, token).ConfigureAwait(false);
        }
        finally { File.Delete(temporary); }
    }
}
