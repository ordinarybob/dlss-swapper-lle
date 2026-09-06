using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Gui;

public sealed partial class ProviderWineLaunchDialog : Window
{
    private readonly ProviderLaunch? _launch;
    public ProviderWineLaunchDialog() => AvaloniaXamlLoader.Load(this);
    public ProviderWineLaunchDialog(string name, ProviderLaunch launch, string? runner, bool launchOnSave = true, string? error = null) : this()
    {
        _launch = launch;
        this.FindControl<TextBlock>("GameNameText")!.Text = name;
        this.FindControl<TextBlock>("ClientText")!.Text = LanguageAppearance.Format("Linux_ProviderWineLaunchDialog_163", "Client: {0}\nExecutable: {1}\nRequest: {2}", launch.ClientName ?? "Windows launcher", launch.WindowsExecutable, launch.WindowsArgument);
        this.FindControl<TextBlock>("PrefixText")!.Text = LanguageAppearance.Get("Linux_ProviderWineLaunchDialog_162", "Wine prefix: ") + launch.ConfigurationDirectory;
        this.FindControl<TextBox>("RunnerText")!.Text = runner;
        this.FindControl<Button>("SaveButton")!.Content = launchOnSave ? LanguageAppearance.Get("Linux_ProviderWineLaunchDialog_161", "Save and launch") : LanguageAppearance.Get("General_Save", "Save");
        this.FindControl<TextBlock>("ErrorText")!.Text = error ?? (launchOnSave ? "" : LanguageAppearance.Get("Linux_ProviderWineLaunchDialog_160", "Saving does not launch the game."));
    }
    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = LanguageAppearance.Get("Linux_ProviderWineLaunchDialog_159", "Choose Wine executable"), AllowMultiple = false });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) this.FindControl<TextBox>("RunnerText")!.Text = path;
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); this.FindControl<TextBlock>("ErrorText")!.Text = error.Message; }
    }
    private void Launch_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_launch is null) return;
            var runner = this.FindControl<TextBox>("RunnerText")!.Text?.Trim();
            var request = _launch.CreateStartInfo(wineRunner: runner); // Validation only. The owner performs dispatch after saving.
            Close(request.FileName);
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); this.FindControl<TextBlock>("ErrorText")!.Text = error.Message; }
    }
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
