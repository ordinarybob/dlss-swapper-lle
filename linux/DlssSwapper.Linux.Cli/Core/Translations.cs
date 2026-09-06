using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace DlssSwapper.Linux.Cli.Core;

public sealed class Translations
{
    public static IReadOnlyList<string> Languages { get; } = new[]
    { "ar-SA", "ar-SY", "ca-ES", "cs-CZ", "de-DE", "en-AU", "en-GB", "en-US", "es-ES", "fa-IR", "fi-FI", "fr-FR", "it-IT", "ja-JP", "ko-KR", "pl-PL", "pt-BR", "ru-RU", "th-TH", "tr-TR", "uk-UA", "vi-VN", "zh-CN", "zh-TW" };
    public string Language { get; }
    public bool RightToLeft => CultureInfo.GetCultureInfo(Language).TextInfo.IsRightToLeft;
    public IReadOnlyDictionary<string, string> Values { get; }

    public Translations(string? language, string? directory = null)
    {
        directory ??= Path.Combine(AppContext.BaseDirectory, "Translations");
        Language = Languages.FirstOrDefault(item => string.Equals(item, language, StringComparison.OrdinalIgnoreCase)) ?? "en-US";
        var values = Read(Path.Combine(directory, "en-US", "Resources.resw"));
        var englishSupplement = Path.Combine(directory, "en-US", "Linux.resw");
        if (File.Exists(englishSupplement)) foreach (var pair in Read(englishSupplement)) values[pair.Key] = pair.Value;
        if (Language != "en-US" && File.Exists(Path.Combine(directory, Language, "Resources.resw")))
            foreach (var pair in Read(Path.Combine(directory, Language, "Resources.resw")))
                if (!string.IsNullOrWhiteSpace(pair.Value)) values[pair.Key] = pair.Value;
        var supplement = Path.Combine(directory, Language, "Linux.resw");
        if (Language != "en-US" && File.Exists(supplement))
            foreach (var pair in Read(supplement))
                if (!string.IsNullOrWhiteSpace(pair.Value)) values[pair.Key] = pair.Value;
        Values = values;
    }

    public string Get(string key, string fallback) => Values.GetValueOrDefault(key, fallback);

    public string Format(string key, string fallback, params object?[] arguments)
    {
        var culture = CultureInfo.GetCultureInfo(Language);
        try { return string.Format(culture, Get(key, fallback), arguments); }
        catch (FormatException) { return string.Format(culture, fallback, arguments); }
    }

    public static Dictionary<string, string> Read(string path)
        => ReadElements(path, "value");

    public static Dictionary<string, string> ReadComments(string path)
        => ReadElements(path, "comment");

    private static Dictionary<string, string> ReadElements(string path, string element)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader);
        var root = document.Root ?? throw new IOException("Translation file has no root element.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in root.Elements("data").Where(item => item.Attribute("name") is not null))
        {
            var key = (string)item.Attribute("name")!;
            var value = (string?)item.Element(element) ?? "";
            if (result.TryGetValue(key, out var prior) && prior != value)
                throw new IOException($"Translation has conflicting values for {key}.");
            result[key] = value;
        }
        return result;
    }
}
