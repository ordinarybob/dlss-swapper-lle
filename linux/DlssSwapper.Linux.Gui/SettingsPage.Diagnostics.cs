using Avalonia.Interactivity;
using Avalonia.Input.Platform;

namespace DlssSwapper.Linux.Gui;

public sealed partial class SettingsPage
{
    private Func<string>? _diagnostics;
    private async void Acknowledgements_Click(object? sender, RoutedEventArgs e)
    {
        try { await new AcknowledgementsWindow().ShowDialog(DialogOwner); }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _validation.Text = error.Message; }
    }
    private async void OpenApplicationLog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = AppLog.CurrentPath;
            if (path is null || !File.Exists(path))
            {
                _validation.Text = LanguageAppearance.Get("Linux_NoApplicationLog", "No application log has been written today.");
                return;
            }
            var file = await StorageProvider.TryGetFileFromPathAsync(new Uri(path));
            if (file is null || !await Launcher.LaunchFileAsync(file))
                throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingDesktopLog", "The desktop could not open the log file."));
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _validation.Text = error.Message; }
    }
    private async void CopyBuildIdentity_Click(object? sender, RoutedEventArgs e) =>
        await CopyBuildIdentityAsync(async text =>
        {
            if (Clipboard is null) throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingClipboard", "Clipboard is unavailable."));
            await Clipboard.SetTextAsync(text);
        });

    internal async Task CopyBuildIdentityAsync(Func<string, Task> copy)
    {
        try
        {
            await copy(DiagnosticsReport.BuildIdentity());
            _validation.Text = LanguageAppearance.Get("Linux_BuildIdentityCopied", "Build information copied.");
        }
        catch (Exception error)
        { AppLog.Write(ApplicationLogLevel.Error, error.Message);
            _validation.Text = LanguageAppearance.Format("Linux_BuildIdentityCopyFailed", "Could not copy build information: {0}", error.Message);
        }
    }
    private async void TranslationToolbox_Click(object? sender, RoutedEventArgs e) =>
        await new TranslationToolboxWindow(_library?.State.Language ?? "en-US").ShowDialog(DialogOwner);
    private async void NetworkTests_Click(object? sender, RoutedEventArgs e) => await new NetworkTestsWindow().ShowDialog(DialogOwner);
    private async void Diagnostics_Click(object? sender, RoutedEventArgs e) =>
        await new DiagnosticsWindow(_diagnostics?.Invoke() ?? DiagnosticsReport.Capture(_library, null)).ShowDialog(DialogOwner);
}
