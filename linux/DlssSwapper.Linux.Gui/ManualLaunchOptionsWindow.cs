using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

internal sealed class ManualLaunchOptionsWindow : Window
{
    internal ManualLaunchOptionsWindow(string name, ManualGameLaunch draft)
    {
        Title = $"Launch options — {name}";
        Width = 540; Height = 540; MinWidth = 440; MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
        var fields = new StackPanel { Spacing = 8 };
        var method = new ComboBox { Name = "LaunchMethod", ItemsSource = new[] { "Native Linux", "Wine" },
            SelectedIndex = draft.Kind == ManualLaunchKind.Wine ? 1 : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        fields.Children.Add(new TextBlock { Text = "Launch method" }); fields.Children.Add(method);
        TextBox Field(string label, string id, string? value)
        {
            fields.Children.Add(new TextBlock { Text = label });
            var input = new TextBox { Name = id, Text = value ?? "" };
            fields.Children.Add(input); return input;
        }
        var runner = Field("Wine executable", "WineRunner", draft.Runner);
        var browse = new Button { Content = "Browse for Wine…" };
        var error = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        browse.Click += async (_, _) =>
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose Wine executable", AllowMultiple = false });
                if (IsVisible && files.Count > 0) runner.Text = files[0].Path.LocalPath;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        fields.Children.Add(browse);
        var prefix = Field("Wine prefix (optional)", "WinePrefix", draft.WinePrefix);
        void UpdateMethod() => runner.IsEnabled = prefix.IsEnabled = browse.IsEnabled = method.SelectedIndex == 1;
        method.SelectionChanged += (_, _) => UpdateMethod(); UpdateMethod();
        var working = Field("Working folder (optional)", "WorkingDirectory", draft.WorkingDirectory);
        var args = Field("Arguments (one argument per line; do not add shell quoting)", "LaunchArguments", string.Join("\n", draft.Arguments));
        args.AcceptsReturn = true; args.MinHeight = 80;
        fields.Children.Add(error);
        layout.Children.Add(new ScrollViewer { Content = fields });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var done = new Button { Content = "Done" }; var cancel = new Button { Content = "Cancel" };
        done.Click += (_, _) => Close(draft with
        {
            Kind = method.SelectedIndex == 1 ? ManualLaunchKind.Wine : ManualLaunchKind.Native,
            Runner = runner.Text, WinePrefix = prefix.Text, WorkingDirectory = working.Text ?? "",
            Arguments = string.IsNullOrEmpty(args.Text) ? [] : args.Text.Replace("\r\n", "\n").Split('\n')
        });
        cancel.Click += (_, _) => Close();
        actions.Children.Add(done); actions.Children.Add(cancel);
        Grid.SetRow(actions, 1); layout.Children.Add(actions); Content = layout;
    }
}
