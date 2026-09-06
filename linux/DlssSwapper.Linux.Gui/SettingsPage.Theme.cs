using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace DlssSwapper.Linux.Gui;

public sealed partial class SettingsPage
{
    private void LoadThemeSelection()
    {
        var panel = this.FindControl<StackPanel>("ThemeSelectionPanel")!;
        var choices = new[] { "Light", "Dark", "System" };
        var current = choices.Contains(_library!.State.AppTheme) ? _library.State.AppTheme! : "Dark";
        var updating = false;
        foreach (var choice in choices)
        {
            var key = choice == "System" ? "SettingsPage_ThemeSystemSettingDefault" : "SettingsPage_Theme" + choice;
            var radio = new RadioButton
            {
                [!ContentControl.ContentProperty] = new DynamicResourceExtension(key),
                Tag = choice, GroupName = "AppTheme", IsChecked = current == choice
            };
            panel.Children.Add(radio);
            radio.IsCheckedChanged += (_, _) =>
            {
                if (updating || radio.IsChecked != true) return;
                try
                {
                    _library.UpdateState(state => state.AppTheme = choice);
                    current = choice;
                    ThemeAppearance.Apply(choice);
                    _validation.Text = string.Empty;
                }
                catch (Exception ex)
                { AppLog.Write(ApplicationLogLevel.Error, ex.Message);
                    updating = true;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var item in panel.Children.OfType<RadioButton>()) item.IsChecked = Equals(item.Tag, current);
                        updating = false;
                    });
                    _validation.Text = LanguageAppearance.Format("Linux_SettingsWindow_Theme_179", "Could not save theme: {0}", ex.Message);
                }
            };
        }
    }
}
