using Avalonia;
using Avalonia.Media;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

internal static class LanguageAppearance
{
    private static Translations? _current;
    public static Translations? Current => _current;
    public static event Action? Changed;
    public static string Get(string key, string fallback) => _current?.Get(key, fallback) ?? fallback;
    public static string Format(string key, string fallback, params object?[] arguments) =>
        _current?.Format(key, fallback, arguments) ?? string.Format(fallback, arguments);

    public static void Apply(string language, IReadOnlyDictionary<string, string>? preview = null)
    {
        var translations = new Translations(language);
        if (preview is not null)
            foreach (var pair in preview)
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    ((Dictionary<string, string>)translations.Values)[pair.Key] = pair.Value;
        var previous = _current;
        _current = translations;
        if (Application.Current is not { } app) return;
        if (previous is not null)
            foreach (var key in previous.Values.Keys)
                if (!translations.Values.ContainsKey(key)) app.Resources.Remove(key);
        foreach (var pair in translations.Values) app.Resources[pair.Key] = pair.Value;
        app.Resources["LleFlowDirection"] = translations.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Changed?.Invoke();
    }
}
