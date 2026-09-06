using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DlssSwapper.Linux.Gui;

public sealed class AcknowledgementsWindow : Window
{
    internal sealed record Notice(string Name, string Notes, string License)
    { public override string ToString() => Name; }

    internal static IReadOnlyList<Notice> LoadNotices()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Acknowledgements");
        return Directory.GetDirectories(root).Select(folder =>
        {
            string Read(string file) => File.Exists(Path.Combine(folder, file)) ? File.ReadAllText(Path.Combine(folder, file)) : "";
            // Match exact names on both Windows and Linux; preserve all bundled license notices.
            var licenseNames = new[] { "license.txt", "COPYING.txt", "LICENSE.txt", "LICENSE-2.0.txt", "copyright" };
            var files = Directory.GetFiles(folder);
            var licenses = licenseNames.SelectMany(name => files.Where(file => Path.GetFileName(file) == name))
                .Select(File.ReadAllText);
            return new Notice(Path.GetFileName(folder), Read("notes.md"), string.Join(Environment.NewLine + Environment.NewLine, licenses));
        }).OrderBy(notice => notice.Name == "You" ? 0 : 1).ThenBy(notice => notice.Name, StringComparer.Ordinal).ToArray();
    }

    public AcknowledgementsWindow()
    {
        Title = LanguageAppearance.Get("Linux_Acknowledgements", "Acknowledgements");
        Width = 850; Height = 680; MinWidth = 550; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 10 };
        var choices = new ComboBox { ItemsSource = LoadNotices(), HorizontalAlignment = HorizontalAlignment.Stretch };
        layout.Children.Add(choices);
        var notes = new StackPanel { Spacing = 6 }; Grid.SetRow(notes, 1); layout.Children.Add(notes);
        var license = new TextBox { Name = "LicenseText", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(license, 2); layout.Children.Add(license);
        var close = new Button { Content = LanguageAppearance.Get("General_Close", "Close"), HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetRow(close, 3); layout.Children.Add(close); close.Click += (_, _) => Close();
        Content = layout;
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedItem is not Notice notice) return;
            notes.Children.Clear();
            notes.Children.Add(new TextBlock { Text = Regex.Replace(notice.Notes, @"\[([^\]]+)\]\(([^)]+)\)", "$1"), TextWrapping = TextWrapping.Wrap });
            foreach (Match match in Regex.Matches(notice.Notes, @"\[([^\]]+)\]\((https://[^)]+)\)"))
            {
                var uri = new Uri(match.Groups[2].Value);
                var label = match.Groups[1].Value;
                var link = new Button { Content = Uri.IsWellFormedUriString(label, UriKind.Absolute) ? uri.Host : label };
                ToolTip.SetTip(link, uri.AbsoluteUri);
                link.Click += async (_, _) =>
                {
                    try
                    {
                        if (!await Launcher.LaunchUriAsync(uri)) throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingDesktopLink", "The desktop could not open the link."));
                    }
                    catch (Exception error)
                    {
                        AppLog.Write(ApplicationLogLevel.Error, error.Message);
                        notes.Children.Add(new TextBlock { Text = error.Message, TextWrapping = TextWrapping.Wrap });
                    }
                };
                notes.Children.Add(link);
            }
            license.Text = notice.License;
        };
        choices.SelectedIndex = 0;
    }
}
