using Avalonia;
using Avalonia.Styling;

namespace DlssSwapper.Linux.Gui;

internal static class ThemeAppearance
{
    // Missing preferences retain the previous Linux dark appearance.
    internal static void Apply(string? preference)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = preference switch
            {
                "Light" => ThemeVariant.Light,
                "System" => ThemeVariant.Default,
                _ => ThemeVariant.Dark
            };
    }
}
