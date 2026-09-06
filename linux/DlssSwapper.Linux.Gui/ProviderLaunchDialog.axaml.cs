using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Gui;

public sealed partial class ProviderLaunchDialog : Window
{
    public ProviderLaunchDialog() => AvaloniaXamlLoader.Load(this);

    public ProviderLaunchDialog(string gameName, IReadOnlyList<ProviderLaunch> choices, bool forSetup = false) : this()
    {
        this.FindControl<TextBlock>("GameNameText")!.Text = gameName;
        this.FindControl<ComboBox>("LauncherChoices")!.ItemsSource = choices;
        this.FindControl<ComboBox>("LauncherChoices")!.SelectedIndex = 0;
        if (forSetup)
        {
            this.FindControl<Button>("LaunchButton")!.Content = LanguageAppearance.Get("Linux_ProviderLaunchDialog_158", "Configure");
            this.FindControl<TextBlock>("IntroText")!.Text = LanguageAppearance.Get("Linux_ProviderLaunchDialog_157", "Choose the launcher configuration to edit. Nothing will be launched.");
        }
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = this.FindControl<ComboBox>("LauncherChoices")?.SelectedItem as ProviderLaunch;
        if (this.FindControl<TextBlock>("ConfigurationText") is { } detail)
            detail.Text = selected is null ? LanguageAppearance.Get("Linux_ProviderLaunchDialog_156", "Choose a launcher.") : LanguageAppearance.Format("Linux_ProviderLaunchDialog_155", "Game ID: {0}\nConfiguration: {1}", selected.AppName, selected.ConfigurationDirectory);
        if (this.FindControl<Button>("LaunchButton") is { } button) button.IsEnabled = selected is not null;
    }

    private void Launch_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ComboBox>("LauncherChoices")!.SelectedItem is ProviderLaunch selected) Close(selected);
    }
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
