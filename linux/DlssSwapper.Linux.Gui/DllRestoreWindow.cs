using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class DllRestoreWindow : Window
{
    private readonly SelectedGame _game;
    private readonly StackPanel _rows = new() { Spacing = 12 };
    private readonly TextBlock _status = Text(LanguageAppearance.Get("Linux_DllRestoreWindow_69", "Inspecting original backups…"));
    private readonly Button _restore = new() { Content = LanguageAppearance.Get("Linux_DllRestoreWindow_68", "Restore selected families"), IsEnabled = false };
    private readonly Button _close = new() { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Close") };
    private readonly HashSet<DllType> _selected = [];
    private IReadOnlyList<DllRestorePreview> _preview = [];
    private IReadOnlyList<string> _warnings = [];
    private bool _busy = true;
    public IReadOnlyList<OperationResult> Results { get; private set; } = [];

    public DllRestoreWindow(SelectedGame game)
    {
        _game = game; Title = LanguageAppearance.Format("Linux_DllRestoreWindow_67", "Restore original DLLs — {0}", game.Name);
        Width = 780; Height = 620; MinWidth = 520; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), RowSpacing = 12, Margin = new Thickness(20) };
        layout.Children.Add(Text(LanguageAppearance.Get("Linux_DllRestoreWindow_66", "Select DLL families to restore. Every listed backup in a selected family is restored. These ordinary-DLL backups are consumed on successful restoration; Streamline originals are separate and are not touched.")));
        var scroll = new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8 };
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 130 });
        var actions = new WrapPanel(); actions.Children.Add(_restore); actions.Children.Add(_close); footer.Children.Add(actions);
        Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;
        _close.Click += (_, _) => { if (!_busy) Close(); };
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
        Opened += async (_, _) => await LoadAsync();
        _restore.Click += async (_, _) => await RestoreAsync();
    }
    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private async Task LoadAsync()
    {
        try
        {
            var inspection = await Task.Run(() => DllRestoreWorkflow.Inspect(_game, LanguageAppearance.Current));
            _preview = inspection.Files; _warnings = inspection.Warnings;
            _rows.Children.Clear();
            if (_preview.Count == 0) _rows.Children.Add(Text(_warnings.Count == 0 ? LanguageAppearance.Get("Linux_DllRestoreWindow_65", "No ordinary-DLL original backups found.") : LanguageAppearance.Get("Linux_DllRestoreWindow_64", "No readable backups found. Inspection was incomplete; see the warnings below.")));
            foreach (var family in _preview.GroupBy(row => row.Item.Family.Type))
            {
                var box = new CheckBox { Content = LanguageAppearance.Format("Linux_DllRestoreWindow_63", "{0} — {1} file(s)", family.First().Item.Family.DisplayName, family.Count()), IsChecked = _selected.Contains(family.Key) };
                box.IsCheckedChanged += (_, _) => { if (box.IsChecked == true) _selected.Add(family.Key); else _selected.Remove(family.Key); UpdateActions(); };
                _rows.Children.Add(box);
                foreach (var row in family) _rows.Children.Add(Text(LanguageAppearance.Format("Linux_DllRestoreWindow_62", "{0}\nInstalled: {1} → Original: {2}", row.Item.RelativeTargetPath, row.InstalledVersion, row.OriginalVersion)));
            }
            if (Results.Count == 0) _status.Text = LanguageAppearance.Get("Linux_DllRestoreWindow_61", "Choose the families whose original files you want to restore.");
            if (_warnings.Count > 0) _status.Text += LanguageAppearance.Get("Linux_DllRestoreWindow_60", "\nInspection incomplete:\n") + string.Join("\n", _warnings);
        }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _status.Text = LanguageAppearance.Format("Linux_DllRestoreWindow_59", "Could not inspect backups: {0}", ex.Message); _preview = []; }
        finally { _busy = false; UpdateActions(); }
    }
    private void UpdateActions()
    {
        _restore.IsEnabled = !_busy && _preview.Any(row => _selected.Contains(row.Item.Family.Type));
        _rows.IsEnabled = !_busy; _close.IsEnabled = !_busy;
    }
    private async Task RestoreAsync()
    {
        if (_busy) return;
        var selected = _preview.Where(row => _selected.Contains(row.Item.Family.Type)).ToArray();
        if (selected.Length == 0) return;
        _busy = true; UpdateActions();
        try
        {
            var warning = ConfirmationDialog.GameFileWriteWarning + (_warnings.Count > 0 ? "\n" + LanguageAppearance.Get("Linux_IncompleteRestore", "Inspection was incomplete. Only the listed files will be restored; other backups may exist.") + "\n" + string.Join("\n", _warnings) : "");
            if (!await new ConfirmationDialog(LanguageAppearance.Get("Linux_RestoreOriginalsTitle", "Restore originals?"), string.Join("\n", selected.Select(row => $"{row.Item.RelativeTargetPath}: {row.InstalledVersion} → {row.OriginalVersion}")), warning).ShowDialog<bool>(this)) return;
            var results = await Task.Run(() => DllRestoreWorkflow.Apply(selected, CancellationToken.None, LanguageAppearance.Current));
            Results = Results.Concat(results).ToArray();
            _status.Text = string.Join("\n", results.Select(result => $"{result.Target}: {result.Message}"));
            await LoadAsync();
        }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _status.Text = LanguageAppearance.Format("Linux_DllRestoreWindow_58", "Restore could not finish: {0}", ex.Message); }
        finally { _busy = false; UpdateActions(); }
    }
}
