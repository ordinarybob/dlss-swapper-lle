using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class NgxModelImportWindow : Window
{
    private readonly DllCatalog _catalog;
    private readonly DownloadCache _cache;
    private readonly PersistentLibrary _library;
    private readonly CancellationTokenSource _cancel = new();
    private readonly StackPanel _items = new() { Spacing = 12 };
    private readonly TextBlock _status = new() { Text = LanguageAppearance.Get("Linux_NgxModelImportWindow_147", "Scanning configured NVIDIA model cache…"), TextWrapping = TextWrapping.Wrap };
    private readonly Button _import = new() { Content = LanguageAppearance.Get("Linux_NgxModelImportWindow_146", "Import selected"), IsEnabled = false };
    private readonly Button _selectAll = new() { [!ContentControl.ContentProperty] = new DynamicResourceExtension("GamesPage_SelectionMode_SelectAll"), IsEnabled = false };
    private readonly Button _stop = new() { Content = LanguageAppearance.Get("Linux_BatchUpdateWindow_7", "Cancel operation") };
    private readonly List<(NgxModelCandidate Model, CheckBox Check, TextBlock Status)> _rows = [];
    private readonly Dictionary<string, string> _hashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NgxServerModel> _serverModels = new(StringComparer.Ordinal);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly bool _fromServer;
    private bool _busy = true;
    private bool _closing;

    public NgxModelImportWindow(DllCatalog catalog, DownloadCache cache, PersistentLibrary library, bool fromServer = false)
    {
        _catalog = catalog; _cache = cache; _library = library;
        _fromServer = fromServer;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DLSS-Swapper-LLE-Linux");
        if (fromServer) _status.Text = LanguageAppearance.Get("Linux_NgxModelImportWindow_145", "Fetching NVIDIA model versions…");
        Title = LanguageAppearance.Get("Linux_DllLibraryWindow_50", "Import NVIDIA models"); Width = 850; Height = 650; MinWidth = 500; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        layout.Children.Add(new TextBlock { Text = fromServer
            ? LanguageAppearance.Get("Linux_NgxModelImportWindow_144", "Select NVIDIA model versions to download and import into the Library. Driver files and games are not changed.")
            : LanguageAppearance.Get("Linux_NgxModelImportWindow_143", "Select cached NVIDIA models to add to the Library. Driver files and games are not changed."), TextWrapping = TextWrapping.Wrap });
        var list = new ScrollViewer { Content = _items }; Grid.SetRow(list, 1); layout.Children.Add(list);
        var statusScroll = new ScrollViewer { Content = _status, MaxHeight = 140 }; Grid.SetRow(statusScroll, 2); layout.Children.Add(statusScroll);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var close = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Close") };
        foreach (var button in new[] { _selectAll, _import, _stop, close }) { button.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(button); }
        Grid.SetRow(actions, 3); layout.Children.Add(actions); Content = layout;
        _selectAll.Click += (_, _) => { foreach (var row in _rows.Where(row => row.Check.IsEnabled)) row.Check.IsChecked = true; };
        _stop.Click += (_, _) => _cancel.Cancel();
        close.Click += (_, _) => Close();
        _import.Click += async (_, _) => await ImportAsync();
        Opened += async (_, _) => await DiscoverAsync();
        Closing += (_, e) => { _closing = true; _cancel.Cancel(); e.Cancel = _busy; };
        Closed += (_, _) => { _cancel.Dispose(); _http.Dispose(); };
    }

    private async Task DiscoverAsync()
    {
        try
        {
            if (_fromServer)
            {
                foreach (var model in await NgxModelServer.ListAsync(_http, _cancel.Token, LanguageAppearance.Current))
                {
                    var check = new CheckBox { Content = LanguageAppearance.Format("Linux_NgxModelImportWindow_141", "{0} {1} — {2:N0} bytes", DllTypes.Get(model.Type).DisplayName, model.Version, model.Size) };
                    var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
                    var card = new StackPanel(); card.Children.Add(check); card.Children.Add(status); _items.Children.Add(card);
                    _serverModels.Add(model.Key, model);
                    _rows.Add((new(model.Key, model.Type, model.Version, model.Size), check, status));
                    check.IsCheckedChanged += (_, _) => UpdateButtons();
                }
                _status.Text = LanguageAppearance.Format("Linux_NgxModelImportWindow_142", "{0} model versions available. Select versions to download and import.", _rows.Count);
                return;
            }
            var result = await Task.Run(() => NgxModelDiscovery.Discover(NgxModelDiscovery.ConfigurationPaths(), _cancel.Token, LanguageAppearance.Current));
            var verifier = new DllSignatureVerifier();
            foreach (var model in result.Models)
            {
                _cancel.Token.ThrowIfCancellationRequested();
                var check = new CheckBox { Content = LanguageAppearance.Format("Linux_NgxModelImportWindow_141", "{0} {1} — {2:N0} bytes", DllTypes.Get(model.Type).DisplayName, model.Version, model.Size), IsEnabled = false };
                var status = new TextBlock { Text = LanguageAppearance.Get("Linux_NgxModelImportWindow_140", "Checking identity and signature…"), TextWrapping = TextWrapping.Wrap };
                var card = new StackPanel(); card.Children.Add(check);
                card.Children.Add(new TextBlock { Text = model.Path, TextWrapping = TextWrapping.Wrap }); card.Children.Add(status); _items.Children.Add(card);
                _rows.Add((model, check, status));
                check.IsCheckedChanged += (_, _) => UpdateButtons();
                try
                {
                    var hash = await Task.Run(() => DllScanner.ComputeMd5(model.Path), _cancel.Token);
                    _hashes[model.Path] = hash;
                    var known = _catalog.FindByHash(model.Type, hash);
                    if (known is not null && _cache.IsCached(known)
                        && await Task.Run(() => DllScanner.ComputeMd5(_cache.GetCachedPath(known)), _cancel.Token) == hash)
                    { status.Text = LanguageAppearance.Get("Linux_NgxModelImportWindow_139", "Already in the Library"); continue; }
                    var trust = known is { IsSignatureValid: true, IsImported: false }
                        ? new DllSignatureResult(true, LanguageAppearance.Get("Linux_GuiRemainingTrustedCatalog", "Trusted catalog match"))
                        : await verifier.VerifyAsync(model.Path, _cancel.Token);
                    status.Text = trust.Message;
                    check.IsEnabled = trust.IsValid || _catalog.Policy.AllowUntrusted;
                    if (!trust.IsValid && check.IsEnabled) status.Text += LanguageAppearance.Get("Linux_NgxModelImportWindow_138", " Unverified import is allowed by your settings.");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); status.Text = LanguageAppearance.Format("Linux_NgxModelImportWindow_137", "Cannot import: {0}", error.Message); }
            }
            _status.Text = LanguageAppearance.Format("Linux_NgxModelImportWindow_136", "{0} recognized model(s). Verify your selection before importing.\n", _rows.Count) + string.Join("\n", result.Warnings);
        }
        catch (OperationCanceledException) { _status.Text = LanguageAppearance.Get("Linux_NgxModelImportWindow_135", "Scan cancelled. Close and reopen to scan again."); }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _status.Text = LanguageAppearance.Format("Linux_NgxModelImportWindow_134", "Model discovery failed: {0}", error.Message); }
        finally { FinishOperation(); }
    }

    private async Task ImportAsync()
    {
        if (_busy || _cancel.IsCancellationRequested) return;
        var selected = _rows.Where(row => row.Check.IsEnabled && row.Check.IsChecked == true).ToArray();
        if (selected.Length == 0) return;
        _busy = true; UpdateButtons();
        try
        {
            var results = new List<DllImportSourceResult>();
            var workflow = new DllImportWorkflow(_catalog, _cache, _library, translations: LanguageAppearance.Current);
            foreach (var row in selected)
            {
                if (_cancel.IsCancellationRequested) { results.Add(new(row.Model.Path, false, LanguageAppearance.Get("Linux_GuiRemainingImportCancelled", "Cancelled; not imported."))); continue; }
                try
                {
                    row.Status.Text = _fromServer ? LanguageAppearance.Get("Linux_NgxModelImportWindow_133", "Downloading and validating…") : LanguageAppearance.Get("Linux_NgxModelImportWindow_132", "Importing…");
                    var message = _fromServer
                        ? await NgxModelServer.ImportAsync(_http, _serverModels[row.Model.Path], workflow, _cancel.Token, LanguageAppearance.Current)
                        : await workflow.ImportModelAsync(row.Model.Path, _cancel.Token, _hashes[row.Model.Path]);
                    results.Add(new(row.Model.Path, true, message)); row.Status.Text = message; row.Check.IsEnabled = false;
                }
                catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); results.Add(new(row.Model.Path, false, error.Message)); row.Status.Text = error.Message; }
            }
            _status.Text = DllImportSources.Describe(results, LanguageAppearance.Current);
        }
        finally { FinishOperation(); }
    }

    private void FinishOperation() { _busy = false; UpdateButtons(); if (_closing) Close(); }
    private void UpdateButtons()
    {
        _import.IsEnabled = !_busy && !_cancel.IsCancellationRequested && _rows.Any(row => row.Check.IsEnabled && row.Check.IsChecked == true);
        _selectAll.IsEnabled = !_busy && !_cancel.IsCancellationRequested && _rows.Any(row => row.Check.IsEnabled);
        _stop.IsEnabled = _busy && !_cancel.IsCancellationRequested;
    }
}
