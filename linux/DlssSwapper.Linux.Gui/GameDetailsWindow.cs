using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public enum GameDetailsAction { Launch, LaunchSetup, Notes, History, Favorite, Reload, Hide, Cover, UpdateLatest, Streamline, Restore, Remove, ScanDetails }

public sealed class GameDetailsWindow : Window
{
    private readonly GameRowViewModel _game;
    private readonly Dictionary<DllType, Button> _versions = [];
    private readonly WrapPanel _actions = new() { Orientation = Orientation.Horizontal };
    private readonly WrapPanel _icons = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Func<GameDetailsAction, Task> _action;
    private readonly Func<DllType, Task> _chooseVersion;
    private readonly TextBox _name;
    private readonly Button _favorite;
    private readonly Button _hide;
    private bool _busy;

    public GameDetailsWindow(GameRowViewModel game, bool manual, Action<string> saveTitle,
        Func<GameDetailsAction, Task> action, Func<DllType, Task> chooseVersion)
    {
        _game = game; _action = action; _chooseVersion = chooseVersion;
        Title = game.Name; Width = 840; Height = 730; MinWidth = 600; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(24), RowSpacing = 18 };
        var body = new Grid { ColumnDefinitions = new("180,*"), ColumnSpacing = 20 };
        var cover = new Image { Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top };
        cover.Bind(Image.SourceProperty, new Avalonia.Data.Binding(nameof(GameRowViewModel.CoverImage)) { Source = game });
        body.Children.Add(cover);
        var fields = new StackPanel { Spacing = 8 };
        fields.Children.Add(Label("General_Name", "Name"));
        var nameRow = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8 };
        _name = new TextBox { Text = game.Name, IsReadOnly = !manual };
        nameRow.Children.Add(_name);
        var save = new Button { Content = L("General_Save", "Save"), IsVisible = manual, IsEnabled = false };
        Grid.SetColumn(save, 1); nameRow.Children.Add(save); fields.Children.Add(nameRow);
        _name.TextChanged += (_, _) => save.IsEnabled = !string.IsNullOrWhiteSpace(_name.Text) && _name.Text != _game.Name;
        save.Click += (_, _) =>
        {
            try { saveTitle(_name.Text!.Trim()); Title = _game.Name; save.IsEnabled = false; _status.Text = ""; }
            catch (Exception error) { _status.Text = error.Message; }
        };
        fields.Children.Add(Label("GamePage_InstallPath", "Install path"));
        var pathRow = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8 };
        pathRow.Children.Add(new TextBox { Text = game.RootPath, IsReadOnly = true });
        var open = new Button { Content = L("General_OpenFolder", "Open folder") };
        open.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(game.RootPath) { UseShellExecute = true })?.Dispose(); }
            catch (Exception error) { _status.Text = error.Message; }
        };
        Grid.SetColumn(open, 1); pathRow.Children.Add(open); fields.Children.Add(pathRow);
        var families = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 16, RowSpacing = 12, Margin = new Thickness(0, 8, 0, 8) };
        for (var i = 0; i < DllTypes.All.Count; i++)
        {
            if (i % 2 == 0) families.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var family = DllTypes.All[i];
            var cell = new StackPanel { Spacing = 4 };
            cell.Children.Add(new TextBlock { Text = family.DisplayName, FontWeight = FontWeight.SemiBold });
            var button = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Tag = family.Type };
            button.Click += async (_, _) => await RunAsync(() => _chooseVersion(family.Type));
            _versions.Add(family.Type, button); cell.Children.Add(button);
            Grid.SetRow(cell, i / 2); Grid.SetColumn(cell, i % 2); families.Children.Add(cell);
        }
        fields.Children.Add(families);
        fields.Children.Add(new TextBlock { Text = L("Linux_NvidiaControlsUnavailable", "NVIDIA driver presets require Windows."), TextWrapping = TextWrapping.Wrap });
        fields.Children.Add(_status);
        var scroll = new ScrollViewer { Content = fields, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetColumn(scroll, 1); body.Children.Add(scroll); root.Children.Add(body);
        SizeChanged += (_, _) => { var wide = ClientSize.Width >= 780; body.ColumnDefinitions[0].Width = new GridLength(wide ? 180 : 0); cover.IsVisible = wide; body.ColumnSpacing = wide ? 20 : 0; };
        Add(GameDetailsAction.Launch, "GamePage_Launch", "Launch");
        if (manual || game.Game.ProviderLaunchChoices.Count > 0) Add(GameDetailsAction.LaunchSetup, "Linux_Xaml_50", "Launch setup");
        Add(GameDetailsAction.Notes, "GamePage_Notes", "Notes");
        Add(GameDetailsAction.History, "GamePage_History", "History");
        _favorite = Add(GameDetailsAction.Favorite, "GamePage_Favorite", "Favorite");
        Add(GameDetailsAction.Reload, "General_Reload", "Reload");
        _hide = Add(GameDetailsAction.Hide, "GamePage_Hide", "Hide");
        Add(GameDetailsAction.Cover, "Linux_AddCover", "Add custom cover");
        Add(GameDetailsAction.UpdateLatest, "GamesPage_Batch_UpdateDetectedDllsToLatest", "Update detected DLLs to latest");
        Add(GameDetailsAction.Streamline, "Linux_Xaml_55", "Streamline components (experimental)");
        Add(GameDetailsAction.Restore, "Linux_Xaml_53", "Restore original DLLs…");
        Add(GameDetailsAction.ScanDetails, "Linux_Xaml_52", "Scan details");
        Add(GameDetailsAction.Remove, "General_Remove", "Remove");
        var close = new Button { Content = L("General_Close", "Close"), IsCancel = true, Margin = new Thickness(0, 0, 4, 4) };
        close.Click += (_, _) => { if (!_busy) Close(); }; _actions.Children.Add(close);
        var footer = new StackPanel { Spacing = 4 };
        footer.Children.Add(_icons); footer.Children.Add(_actions);
        Grid.SetRow(footer, 1); root.Children.Add(footer); Content = root;
        Closing += (_, e) => e.Cancel = _busy;
        Refresh();
    }

    private Button Add(GameDetailsAction action, string key, string fallback)
    {
        var button = new Button { Content = L(key, fallback), Margin = new Thickness(0, 0, 4, 4), Tag = action };
        var geometry = action switch
        {
            GameDetailsAction.Launch => "M6,3 L21,12 L6,21 Z",
            GameDetailsAction.Notes => "M4,3 H20 V21 H4 Z M8,8 H16 M8,12 H16 M8,16 H13",
            GameDetailsAction.History => "M4,6 A9,9 0 1 1 3,15 M4,2 V7 H9 M12,7 V12 L16,15",
            GameDetailsAction.Favorite => "M12,2 L15,8 L22,9 L17,14 L18,22 L12,18 L6,22 L7,14 L2,9 L9,8 Z",
            GameDetailsAction.Reload => "M20,7 V12 H15 M20,12 A8,8 0 1 0 18,17",
            GameDetailsAction.Hide => "M2,12 Q12,0 22,12 Q12,24 2,12 M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8",
            GameDetailsAction.Cover => "M3,3 H21 V21 H3 Z M3,17 L9,10 L14,16 L17,12 L21,17 M16,6 L17,7",
            _ => null
        };
        if (geometry is not null)
        {
            button.Width = 40; button.MinWidth = 40; button.Height = 36; button.Padding = new Thickness(8);
            var icon = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(geometry), Stretch = Stretch.Uniform, Width = 20, Height = 20, StrokeThickness = 1.5 };
            icon.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, button.GetObservable(ForegroundProperty));
            button.Content = icon;
            ToolTip.SetTip(button, L(key, fallback));
            Avalonia.Automation.AutomationProperties.SetName(button, L(key, fallback));
        }
        button.Click += async (_, _) => await RunAsync(() => _action(action));
        (geometry is not null || action == GameDetailsAction.LaunchSetup ? _icons : _actions).Children.Add(button); return button;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true; _actions.IsEnabled = false; _icons.IsEnabled = false;
        foreach (var button in _versions.Values) button.IsEnabled = false;
        try { await action(); }
        catch (Exception error) { _status.Text = error.Message; }
        finally { _busy = false; _actions.IsEnabled = true; _icons.IsEnabled = true; Refresh(); }
    }

    public void Refresh()
    {
        Title = _game.Name;
        ToolTip.SetTip(_favorite, _game.FavoriteActionText); ToolTip.SetTip(_hide, _game.HideActionText);
        Avalonia.Automation.AutomationProperties.SetName(_favorite, _game.FavoriteActionText);
        Avalonia.Automation.AutomationProperties.SetName(_hide, _game.HideActionText);
        foreach (var (type, button) in _versions)
        {
            var dlls = _game.ScanResult?.Dlls.Where(file => file.Type == type).ToArray() ?? [];
            button.Content = dlls.Length == 0 ? L("General_NotFound", "Not found")
                : string.Join(", ", dlls.Select(file => file.Version).Distinct()) + "  ▾";
            button.IsEnabled = !_busy && dlls.Length > 0;
            ToolTip.SetTip(button, string.Join("\n", dlls.Select(file => file.Path)));
        }
    }

    internal void CloseAfterRemoval()
    {
        _busy = false;
        Close();
    }

    private static string L(string key, string fallback) => LanguageAppearance.Get(key, fallback);
    private static TextBlock Label(string key, string fallback) => new() { Text = L(key, fallback), FontWeight = FontWeight.SemiBold };
}
