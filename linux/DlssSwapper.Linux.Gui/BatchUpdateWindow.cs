using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DLSS_Swapper.Data.Streamline;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class BatchUpdateWindow : Window
{
    private sealed record Choice(DllCatalogEntry? Entry)
    {
        public override string ToString() => Entry is null ? LanguageAppearance.Get("GamesPage_Batch_NoChange", "Don't change") : DllReleaseDisplay.Name(Entry);
    }
    private readonly IReadOnlyList<ScanResult> _scans;
    private readonly Dictionary<DllType, ComboBox> _families = [];
    private readonly Dictionary<string, CheckBox> _components = [];
    private readonly CheckBox _include = new() { Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_22", "Include Streamline updates (experimental)") };
    private readonly Button _componentPicker = new() { Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_21", "Components…"), IsEnabled = false };
    private readonly TextBlock _sdkStatus = Text("");
    private readonly ComboBox _sdkVersion = new() { PlaceholderText = "Streamline SDK version", HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false };
    private readonly TextBlock _status = Text("");
    private readonly StackPanel _settings = new() { Spacing = 12 };
    private readonly Button _apply = new() { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Apply"), MinWidth = 120 };
    private readonly Button _cancel = new() { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Cancel"), MinWidth = 120 };
    private readonly Button _report = new() { Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_20", "View / save report"), IsEnabled = false };
    private readonly HttpClient _http;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly StreamlineLibraryService _sdk;
    private readonly string? _dllCacheRoot;
    private readonly bool _downloadedOnly;
    private readonly int _concurrency;
    private CancellationTokenSource? _operation;
    private Task _metadataTask = Task.CompletedTask;
    private bool _closed;
    private bool _busy;
    public IReadOnlyList<OperationResult> Results { get; private set; } = [];
    public event Action? Applied;

    public BatchUpdateWindow(DllCatalog catalog, IReadOnlyList<ScanResult> scans, HttpMessageHandler? httpHandler = null, string? sdkCacheRoot = null, string? dllCacheRoot = null, bool downloadedOnly = false, int concurrency = 15)
    {
        _scans = scans;
        _dllCacheRoot = dllCacheRoot;
        _downloadedOnly = downloadedOnly;
        _concurrency = Math.Clamp(concurrency, 1, 26);
        _http = new HttpClient(httpHandler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DLSS-Swapper-LLE-Linux");
        _sdk = new(_http, sdkCacheRoot);
        Title = LanguageAppearance.Format("Linux_BatchUpdateWindow_19", "Batch update — {0} games", scans.Count);
        Width = 850; Height = 720; MinWidth = 640; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        var top = new StackPanel { Spacing = 8 };
        top.Children.Add(Text(Title, 21));
        var latest = new Button { Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_18", "Select latest detected DLL versions") };
        latest.Click += (_, _) => { if (_busy) return; foreach (var picker in _families.Values) picker.SelectedIndex = picker.ItemCount > 1 ? 1 : 0; if (_include.IsChecked == true) _sdkVersion.SelectedIndex = 0; };
        top.Children.Add(latest);
        top.Children.Add(Text(LanguageAppearance.Get("Linux_BatchUpdateWindow_17", "Only installed files are updated. First originals are kept. NVIDIA preset controls are unavailable in this Linux app.")));
        layout.Children.Add(top);
        var detected = scans.SelectMany(scan => scan.Dlls).Select(dll => dll.Type).ToHashSet();
        using var cache = new DownloadCache(cacheRoot: dllCacheRoot);
        foreach (var family in DllTypes.All.Where(family => detected.Contains(family.Type)))
        {
            var row = new Grid { ColumnDefinitions = new("220,*"), ColumnSpacing = 12 };
            row.Children.Add(Text(family.DisplayName));
            var picker = new ComboBox { ItemsSource = new[] { new Choice(null) }.Concat(catalog.GetEntries(family.Type).Where(entry => !downloadedOnly || cache.IsCached(entry)).Select(entry => new Choice(entry))).ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            Grid.SetColumn(picker, 1); row.Children.Add(picker); _families.Add(family.Type, picker); _settings.Children.Add(row);
        }
        var streamlineControls = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8 };
        streamlineControls.Children.Add(_include); Grid.SetColumn(_componentPicker, 1); streamlineControls.Children.Add(_componentPicker);
        top.Children.Add(streamlineControls);
        top.Children.Add(_sdkVersion);
        top.Children.Add(new ScrollViewer { Content = _sdkStatus, MaxHeight = 60,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var componentPanel = new StackPanel { Spacing = 6, Width = 300 };
        componentPanel.Children.Add(Text(LanguageAppearance.Get("Linux_BatchUpdateWindow_16", "Select components. Partial sets are not recommended because they can mix incompatible SDK versions.")));
        var installedComponents = scans.SelectMany(scan => scan.StreamlineFiles).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in StreamlineComponentSet.FileNames.Where(installedComponents.Contains))
        {
            var box = new CheckBox { Content = name, IsChecked = true };
            ToolTip.SetTip(box, StreamlineDisplay.Description(name));
            _components.Add(name, box); componentPanel.Children.Add(box);
        }
        _componentPicker.Flyout = new Flyout { Content = new ScrollViewer { Content = componentPanel, MaxHeight = 360,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } };
        if (_components.Count == 0) { _include.IsEnabled = false; _sdkStatus.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_15", "No Streamline components found in the selected games."); }
        _include.IsCheckedChanged += async (_, _) =>
        {
            _componentPicker.IsEnabled = !_busy && _include.IsChecked == true;
            _sdkVersion.IsVisible = _include.IsChecked == true;
            if (_include.IsChecked != true) return;
            if (_metadataTask.IsCompleted) _metadataTask = CheckVersionAsync();
            await _metadataTask;
        };
        var scroll = new ScrollViewer { Content = _settings, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8 };
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 140 });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actions.Children.Add(_apply); actions.Children.Add(_cancel); footer.Children.Add(actions);
        _report.Click += async (_, _) => await new OperationReportWindow(LanguageAppearance.Get("Linux_BatchResults", "Batch update results"), Results).ShowDialog(this);
        actions.Children.Add(_report);
        Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;
        _apply.Click += async (_, _) => await ApplyAsync();
        _cancel.Click += (_, _) => { if (_busy) RequestCancellation(); else Close(); };
        Closing += (_, e) => { if (_busy) { e.Cancel = true; RequestCancellation(); } };
        Closed += async (_, _) => { _closed = true; _lifetime.Cancel(); await _metadataTask; _http.Dispose(); _lifetime.Dispose(); };
    }

    private void RequestCancellation()
    {
        _operation?.Cancel();
        _cancel.IsEnabled = false;
        _status.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_14", "Stopping after the current file operation. Completed updates are kept.");
    }

    private async Task CheckVersionAsync()
    {
        _sdkStatus.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_13", "Checking latest SDK version…");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var releases = await _sdk.FetchReleasesAsync(timeout.Token);
            if (_closed) return;
            var selected = (_sdkVersion.SelectedItem as StreamlineSdkRelease)?.Tag;
            var choices = releases.Where(release => !_downloadedOnly || _sdk.FindCached(release.Tag) is not null).ToArray();
            _sdkVersion.ItemsSource = choices;
            _sdkVersion.SelectedItem = choices.FirstOrDefault(release => release.Tag == selected) ?? choices.FirstOrDefault();
            _sdkStatus.Text = _downloadedOnly ? "Downloaded-only: select a cached SDK version." : "Selected SDK downloads automatically when applying.";
        }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); if (!_closed) { _sdkVersion.ItemsSource = _sdk.CachedReleases(); _sdkVersion.SelectedIndex = 0; _sdkStatus.Text = "Release history unavailable; showing downloaded packages."; } }
    }

    private static TextBlock Text(string text, double size = 14) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };

    private async Task ApplyAsync()
    {
        if (_busy) return;
        var candidates = _families.Where(pair => pair.Value.SelectedItem is Choice { Entry: not null })
            .ToDictionary(pair => pair.Key, pair => ((Choice)pair.Value.SelectedItem!).Entry!);
        var components = _include.IsChecked == true ? _components.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key).ToArray() : [];
        var sdkRelease = _sdkVersion.SelectedItem as StreamlineSdkRelease;
        if (components.Length > 0 && sdkRelease is null) { _status.Text = _downloadedOnly ? "No cached Streamline SDK. Download it from Library first." : "Select a Streamline SDK version first."; return; }
        if (candidates.Count == 0 && components.Length == 0) { _status.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_8", "Select a DLL version or Streamline components first."); return; }
        _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _operation.Token;
        _busy = true; _settings.IsEnabled = false; _apply.IsEnabled = false; _cancel.Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_7", "Cancel operation");
        _include.IsEnabled = false; _componentPicker.IsEnabled = false; _sdkVersion.IsEnabled = false;
        try
        {
            _status.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_6", "Preparing the comparison. Any package download here does not change game files.");
            var plan = await Task.Run(() => BatchUpdateWorkflow.PrepareAsync(_scans, candidates, components,
                async ct => _downloadedOnly ? _sdk.FindCached(sdkRelease!.Tag) ?? throw new InvalidOperationException("The selected Streamline SDK is not downloaded.")
                    : (await _sdk.PrepareAsync(sdkRelease!, ct)).DirectoryPath, token, LanguageAppearance.Current));
            token.ThrowIfCancellationRequested();
            var files = plan.Games.Sum(game => game.Dlls.Where(item => item.Status == UpdatePlanStatus.Ready).Sum(item => item.Targets.Count)
                + (game.Streamline is { CanUpdate: true } sdk ? sdk.Components.Count(item => item.UpdateChanges) : 0));
            var details = plan.Games.Select(game => game.Game.Name + "\n"
                + string.Join("\n", game.Dlls.Select(item => $"  {item.Family.DisplayName}: {item.Message}"))
                + (game.Streamline is { } sdk ? "\n" + string.Join("\n", sdk.Components.Select(item => $"  {item.FileName}: {StreamlineDisplay.Text(item.InstalledVersion)} → {StreamlineDisplay.Text(item.PackageVersion)} ({StreamlineDisplay.Text(item.UpdateText)})")) : "")
                + (game.StreamlineError is not null ? "\n  Streamline: " + game.StreamlineError : ""));
            var summary = LanguageAppearance.Format("Linux_BatchConfirmCount", "Update {0} files across {1} selected games?", files, plan.Games.Count) + "\n\n" + string.Join("\n\n", details);
            if (files == 0) { _status.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_5", "No files were changed.\n\n") + string.Join("\n\n", details); return; }
            var warning = ConfirmationDialog.GameFileWriteWarning
                + (plan.Games.Any(game => game.PartialStreamline) ? "\n" + LanguageAppearance.Get("Linux_BatchPartialSdk", "Updating only part of a Streamline set is not recommended. Mixed SDK versions may break game features or prevent the game from starting. Apply the complete set unless you understand the compatibility risks.") : "");
            if (!await new ConfirmationDialog(LanguageAppearance.Get("Linux_BatchConfirmTitle", "Confirm batch update"), summary, warning).ShowDialog<bool>(this))
            { _status.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_4", "Cancelled. No game files changed; any downloaded package remains cached."); return; }
            token.ThrowIfCancellationRequested();
            _status.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_3", "Updating game files…");
            using var cache = new DownloadCache(cacheRoot: _dllCacheRoot);
            Results = await Task.Run(() => BatchUpdateWorkflow.ApplyAsync(plan, cache.GetAsync, token, _concurrency, LanguageAppearance.Current));
            _status.Text = string.Join("\n", Results.Select(result => $"{result.Game.Name} — {result.Family}: {result.Message}"));
            _apply.IsVisible = false; _cancel.Content = LanguageAppearance.Get("General_Close", "Close");
            _report.IsEnabled = Results.Count > 0;
        }
        catch (OperationCanceledException) { _status.Text = LanguageAppearance.Get("Linux_BatchUpdateWindow_2", "Cancelled before applying. No game files changed; any downloaded package remains cached."); }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _status.Text = LanguageAppearance.Format("Linux_BatchUpdateWindow_1", "Batch operation could not finish: {0}", ex.Message); }
        finally { _operation.Dispose(); _operation = null; _busy = false; _settings.IsEnabled = true; _apply.IsEnabled = true; _cancel.IsEnabled = true; _cancel.Content = Results.Count > 0 ? LanguageAppearance.Get("General_Close", "Close") : LanguageAppearance.Get("General_Cancel", "Cancel");
            _include.IsEnabled = _components.Count > 0; _componentPicker.IsEnabled = _include.IsChecked == true; _sdkVersion.IsEnabled = true; }
        if (Results.Count > 0) Applied?.Invoke();
    }
}
