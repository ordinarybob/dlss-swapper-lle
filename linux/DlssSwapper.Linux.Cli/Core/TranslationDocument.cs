using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record TranslationEdit(string Key, string SourceTranslation, string Comment, string NewTranslation);

public sealed class TranslationDocument
{
    public Dictionary<string, TranslationEdit> Rows { get; }

    public TranslationDocument(Translations source)
    {
        Rows = source.Values.ToDictionary(pair => pair.Key,
            pair => new TranslationEdit(pair.Key, pair.Value, "", ""), StringComparer.Ordinal);
        foreach (var file in new[] { "Resources.resw", "Linux.resw" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Translations", "en-US", file);
            if (!File.Exists(path)) continue;
            foreach (var pair in Translations.ReadComments(path))
                if (Rows.TryGetValue(pair.Key, out var row)) Rows[pair.Key] = row with { Comment = pair.Value };
        }
    }

    public void Load(string path)
    {
        ValidateDraftExtension(path);
        var loaded = new Dictionary<string, TranslationEdit>(StringComparer.Ordinal);
        if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            using var csv = new TextFieldParser(path, Encoding.UTF8) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
            csv.SetDelimiters(",");
            var headers = csv.ReadFields() ?? throw new IOException("CSV headers are missing.");
            var key = Array.IndexOf(headers, "key"); var value = Array.IndexOf(headers, "NewTranslations");
            var original = Array.IndexOf(headers, "SourceTranslation"); var comment = Array.IndexOf(headers, "Comment");
            if (key < 0 || value < 0) throw new IOException("CSV requires key and NewTranslations columns.");
            while (!csv.EndOfData)
            {
                var fields = csv.ReadFields()!;
                if (fields.Length != headers.Length) throw new IOException("CSV row does not match its headers.");
                loaded.Add(fields[key], new(fields[key], original < 0 ? "" : fields[original], comment < 0 ? "" : fields[comment], fields[value]));
            }
        }
        else
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? throw new IOException("Invalid translation JSON.");
            foreach (var pair in values) loaded.Add(pair.Key, new(pair.Key, "", "", pair.Value ?? ""));
        }
        foreach (var key in Rows.Keys.ToArray()) Rows[key] = Rows[key] with { NewTranslation = "" };
        foreach (var pair in loaded)
            Rows[pair.Key] = Rows.TryGetValue(pair.Key, out var prior)
                ? prior with { NewTranslation = pair.Value.NewTranslation, Comment = pair.Value.Comment.Length == 0 ? prior.Comment : pair.Value.Comment }
                : pair.Value;
    }

    public Dictionary<string, string> Edits() => Rows.Values.Where(row => !string.IsNullOrWhiteSpace(row.NewTranslation))
        .ToDictionary(row => row.Key, row => row.NewTranslation, StringComparer.Ordinal);

    public Task SaveAsync(string path)
    {
        ValidateDraftExtension(path);
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var text = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? "key,Comment,SourceTranslation,NewTranslations\r\n" + string.Join("\r\n", Rows.Values.Select(row =>
                string.Join(",", new[] { row.Key, row.Comment, row.SourceTranslation, row.NewTranslation }.Select(Quote))))
            : JsonSerializer.Serialize(Edits(), new JsonSerializerOptions { WriteIndented = true });
        return OperationReport.SaveLocalAsync(path, text);
    }

    private static void ValidateDraftExtension(string path)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is not (".json" or ".csv"))
            throw new IOException("Choose a JSON or CSV translation file.");
    }

    public async Task PublishAsync(string path)
    {
        if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a ZIP export file.");
        var edits = Edits();
        if (edits.Count == 0) throw new IOException("There are no translations to export.");
        var document = new XDocument(new XElement("root", edits.Select(pair => new XElement("data",
            new XAttribute("name", pair.Key), new XAttribute(XNamespace.Xml + "space", "preserve"), new XElement("value", pair.Value)))));
        path = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".translation-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            await using (var entry = zip.CreateEntry("Resources.resw").Open())
                await document.SaveAsync(entry, SaveOptions.None, default);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
