using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DlssSwapper.Linux.Gui;

public sealed class GameTitleDialog : Window
{
    public GameTitleDialog(string title, Func<string, string> saveTitle)
    {
        Title = LanguageAppearance.Get("Linux_GameTitleDialog_91", "Edit game title");
        Width = 520; Height = 260; MinWidth = 340; MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { Margin = new Thickness(18), RowDefinitions = new("Auto,Auto,*,Auto"), RowSpacing = 12 };
        layout.Children.Add(new TextBlock { Text = LanguageAppearance.Get("Linux_GameTitleDialog_90", "Title for this manually added game") });
        var editor = new TextBox { Text = title };
        Grid.SetRow(editor, 1); layout.Children.Add(editor);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(error, 2); layout.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var save = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Save"), IsDefault = true };
        save.Classes.Add("primary");
        var cancel = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Cancel"), IsCancel = true };
        actions.Children.Add(save); actions.Children.Add(cancel);
        Grid.SetRow(actions, 3); layout.Children.Add(actions); Content = layout;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(editor.Text)) { error.Text = LanguageAppearance.Get("Linux_GameTitleDialog_89", "Enter a game title."); return; }
            try { Close(saveTitle(editor.Text)); }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); error.Text = LanguageAppearance.Format("Linux_GameTitleDialog_88", "Could not save title: {0} Your edit is still here; retry or cancel.", ex.Message); }
        };
        cancel.Click += (_, _) => Close(null);
    }
}
