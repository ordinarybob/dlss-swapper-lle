using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Platform.Storage;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class SettingsPage : UserControl
{
    public event Action<bool>? Finished;
    public event Action? Changed;
    private bool _liveSettings;
    private bool _savingLive;
    private Window DialogOwner => TopLevel.GetTopLevel(this) as Window
        ?? throw new InvalidOperationException("Settings must be attached to a window.");
    private IStorageProvider StorageProvider => DialogOwner.StorageProvider;
    private Avalonia.Input.Platform.IClipboard? Clipboard => DialogOwner.Clipboard;
    private Avalonia.Platform.Storage.ILauncher Launcher => DialogOwner.Launcher;
    private PersistentLibrary? _library;
    private readonly CheckBox _hddMode;
    private readonly NumericUpDown _gridCardSize;
    private readonly TextBox _mediaWikiEndpoint;
    private readonly TextBox _mediaWikiHost;
    private readonly TextBox _customPatterns;
    private readonly TextBox _additionalSteamRoots;
    private readonly TextBlock _validation;

    public bool WasReset { get; private set; }

    public SettingsPage()
    {
        AvaloniaXamlLoader.Load(this);
        _hddMode = FindRequired<CheckBox>("HddModeCheckBox");
        _gridCardSize = FindRequired<NumericUpDown>("GridCardSizeInput");
        _mediaWikiEndpoint = FindRequired<TextBox>("MediaWikiEndpointTextBox");
        _mediaWikiHost = FindRequired<TextBox>("MediaWikiHostTextBox");
        _customPatterns = FindRequired<TextBox>("CustomPatternsTextBox");
        _additionalSteamRoots = FindRequired<TextBox>("AdditionalSteamRootsTextBox");
        _validation = FindRequired<TextBlock>("ValidationTextBlock");
        _hddMode.IsCheckedChanged += (_, _) => { ResetScanArtwork(); FindRequired<NumericUpDown>("BatchConcurrencyInput").Value = 15; FindRequired<NumericUpDown>("UiBatchInput").Value = 550; };
    }

    public SettingsPage(PersistentLibrary library, Func<string>? diagnostics = null)
        : this()
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _diagnostics = diagnostics;
        FindRequired<TextBlock>("BuildIdentityText").Text = DiagnosticsReport.BuildIdentity();
        LoadLibrarySelection();
        LoadThemeSelection();
        var languages = FindRequired<ComboBox>("LanguageComboBox");
        languages.ItemsSource = Translations.Languages.Select(code => System.Globalization.CultureInfo.GetCultureInfo(code).NativeName).ToArray();
        languages.SelectedIndex = Array.IndexOf(Translations.Languages.ToArray(), library.State.Language);
        if (languages.SelectedIndex < 0) languages.SelectedIndex = 7;

        _hddMode.IsChecked = library.State.HddMode;
        FindRequired<NumericUpDown>("ScanConcurrencyInput").Value = library.State.Performance.ScanConcurrency;
        FindRequired<NumericUpDown>("ArtworkConcurrencyInput").Value = library.State.Performance.ArtworkConcurrency;
        FindRequired<NumericUpDown>("BatchConcurrencyInput").Value = Math.Clamp(library.State.BatchSwapConcurrency, 1, 26);
        FindRequired<NumericUpDown>("UiBatchInput").Value = library.State.UiCollectionBatchSize;
        FindRequired<CheckBox>("AllowDebugCheckBox").IsChecked = library.State.AllowDebugDlls;
        FindRequired<CheckBox>("AllowUntrustedCheckBox").IsChecked = library.State.AllowUntrustedDlls;
        FindRequired<CheckBox>("OnlyDownloadedCheckBox").IsChecked = library.State.OnlyShowDownloadedDlls;
        _gridCardSize.Value = library.State.CardSize;
        _mediaWikiEndpoint.Text = library.State.MediaWikiApiEndpoint;
        _mediaWikiHost.Text = library.State.MediaWikiImageHost;
        FindRequired<TextBox>("BuiltInPatternsTextBox").Text = string.Join(
            Environment.NewLine,
            FastScanPatternIndex.BuiltInPatterns.Select(DisplayPattern));
        _customPatterns.Text = string.Join(
            Environment.NewLine,
            library.State.CustomScanPatterns.Select(DisplayPattern));
        FindRequired<TextBox>("IgnoredPathsTextBox").Text = string.Join(Environment.NewLine, library.State.IgnoredPaths);
        var logLevel = FindRequired<ComboBox>("ApplicationLogLevelComboBox");
        logLevel.ItemsSource = Enum.GetNames<ApplicationLogLevel>();
        logLevel.ItemTemplate = new FuncDataTemplate<string>((level, _) => new TextBlock
        {
            [!TextBlock.TextProperty] = new DynamicResourceExtension("SettingsPage_Logging_" + level)
        });
        logLevel.SelectedItem = Enum.TryParse<ApplicationLogLevel>(library.State.ApplicationLoggingLevel, out var savedLevel)
            && Enum.IsDefined(savedLevel) ? savedLevel.ToString() : "Error";
        _additionalSteamRoots.Text = string.Join(
            Environment.NewLine,
            library.State.AdditionalSteamRoots);
        FindRequired<TextBox>("ProviderPrefixesTextBox").Text = string.Join(Environment.NewLine, library.State.ProviderWinePrefixes);
        FindRequired<TextBox>("LegendaryDirectoriesTextBox").Text = string.Join(Environment.NewLine, library.State.LegendaryConfigDirectories);
        FindRequired<TextBox>("HeroicExecutableTextBox").Text = library.State.HeroicExecutable;
        FindRequired<TextBox>("HeroicDirectoriesTextBox").Text = string.Join(Environment.NewLine, library.State.HeroicConfigDirectories);
        FindRequired<TextBlock>("StateLocationTextBlock").Text =
            LanguageAppearance.Format("Linux_SettingsWindow_173", "Configuration: {0}", library.StateDirectory);
    }

    private void ResetArtworkSource_Click(object? sender, RoutedEventArgs e)
    {
        _mediaWikiEndpoint.Text = LinuxLibraryState.DefaultMediaWikiApiEndpoint;
        _mediaWikiHost.Text = LinuxLibraryState.DefaultMediaWikiImageHost;
        _validation.Text = string.Empty;
    }

    private void ResetPerformanceDefaults_Click(object? sender, RoutedEventArgs e)
    {
        var defaults = new LinuxLibraryState();
        defaults.ResetPerformanceDefaults();
        FindRequired<NumericUpDown>("ScanConcurrencyInput").Value = defaults.ScanConcurrency;
        FindRequired<NumericUpDown>("ArtworkConcurrencyInput").Value = defaults.ArtworkConcurrency;
        FindRequired<NumericUpDown>("UiBatchInput").Value = defaults.UiCollectionBatchSize;
        FindRequired<NumericUpDown>("BatchConcurrencyInput").Value = defaults.BatchSwapConcurrency;
    }
    private void ResetScanArtwork()
    {
        var defaults = _hddMode.IsChecked == true ? PerformanceLimits.Hdd : PerformanceLimits.Standard;
        FindRequired<NumericUpDown>("ScanConcurrencyInput").Value = defaults.ScanConcurrency;
        FindRequired<NumericUpDown>("ArtworkConcurrencyInput").Value = defaults.ArtworkConcurrency;
    }

    private async void ResetLocalData_Click(object? sender, RoutedEventArgs e)
    {
        if (_library is null)
        {
            _validation.Text = LanguageAppearance.Get("Linux_SettingsWindow_171", "The persistent library is unavailable.");
            return;
        }

        var confirmed = await new ConfirmationDialog(
            LanguageAppearance.Get("Linux_GuiRemainingResetTitle", "Reset Linux local data"),
            LanguageAppearance.Get("Linux_GuiRemainingResetPrompt", "Remove this user's LLE Linux settings, learned paths, game preferences, history, downloaded DLL cache, and application artwork cache?"),
            LanguageAppearance.Get("Linux_GuiRemainingResetWarning", "SteamLibrary-adjacent artwork is preserved. This cannot be undone."))
            .ShowDialog<bool>(DialogOwner);
        if (!confirmed)
        {
            return;
        }

        try
        {
            _library.ResetLocalData();
            WasReset = true;
            Finished?.Invoke(true);
        }
        catch (Exception exception)
        { AppLog.Write(ApplicationLogLevel.Error, exception.Message);
            _validation.Text = LanguageAppearance.Format("Linux_SettingsWindow_172", "Reset failed: {0}", exception.Message);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Finished?.Invoke(false);

    private async void Proxy_Click(object? sender, RoutedEventArgs e)
    {
        if (_library is not null) await new ProxySettingsWindow(_library).ShowDialog(DialogOwner);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_library is null)
        {
            _validation.Text = LanguageAppearance.Get("Linux_SettingsWindow_171", "The persistent library is unavailable.");
            return;
        }

        if (!LibraryStateStore.TryValidateArtworkSource(
            _mediaWikiEndpoint.Text,
            _mediaWikiHost.Text,
            out var endpoint,
            out var host,
            out var error))
        {
            _validation.Text = error switch
            {
                "Enter an absolute HTTPS MediaWiki API URL without credentials, a query, or a fragment." =>
                    LanguageAppearance.Get("Linux_SettingsApiInvalid", error),
                "Enter one DNS host name for cover images, without a scheme or path." =>
                    LanguageAppearance.Get("Linux_SettingsImageHostInvalid", error),
                _ => error
            };
            return;
        }

        try
        {
        var prefixes = ParseLines(FindRequired<TextBox>("ProviderPrefixesTextBox").Text);
        var ignoredPaths = GameViewPolicy.NormalizeIgnoredPaths(ParseLines(FindRequired<TextBox>("IgnoredPathsTextBox").Text));
        var directories = ParseLines(FindRequired<TextBox>("LegendaryDirectoriesTextBox").Text);
        var heroicDirectories = ParseLines(FindRequired<TextBox>("HeroicDirectoriesTextBox").Text);
        if (heroicDirectories.Any(path => !System.IO.Path.IsPathFullyQualified(path) || new System.IO.DirectoryInfo(path).Name != "heroic"))
            throw new System.IO.IOException(LanguageAppearance.Get("Linux_SettingsHeroicFoldersInvalid", "Heroic folders must be absolute paths ending in heroic."));
        var heroicExecutable = FindRequired<TextBox>("HeroicExecutableTextBox").Text?.Trim();
        if (!string.IsNullOrEmpty(heroicExecutable) && !System.IO.Path.IsPathFullyQualified(heroicExecutable))
            throw new System.IO.IOException(LanguageAppearance.Get("Linux_SettingsHeroicExeInvalid", "The Heroic executable path must be absolute, or blank to use PATH."));
        if (prefixes.Concat(directories).Any(path => !System.IO.Path.IsPathFullyQualified(path)))
            throw new System.IO.IOException(LanguageAppearance.Get("Linux_SettingsProviderFoldersInvalid", "Provider folders must be absolute paths, one per line. Missing or disconnected folders can remain saved."));
        _library.UpdateState(state =>
        {
            state.HddMode = _hddMode.IsChecked == true;
            state.Language = Translations.Languages[Math.Max(0, FindRequired<ComboBox>("LanguageComboBox").SelectedIndex)];
            state.HasSelectedStorageProfile = true;
            state.BatchSwapConcurrency = (int)(FindRequired<NumericUpDown>("BatchConcurrencyInput").Value ?? 15);
            state.UiCollectionBatchSize = (int)(FindRequired<NumericUpDown>("UiBatchInput").Value ?? 550);
            state.ScanConcurrency = (int)(FindRequired<NumericUpDown>("ScanConcurrencyInput").Value ?? state.Performance.ScanConcurrency);
            state.ArtworkConcurrency = (int)(FindRequired<NumericUpDown>("ArtworkConcurrencyInput").Value ?? state.Performance.ArtworkConcurrency);
            state.AllowDebugDlls = FindRequired<CheckBox>("AllowDebugCheckBox").IsChecked == true;
            state.AllowUntrustedDlls = FindRequired<CheckBox>("AllowUntrustedCheckBox").IsChecked == true;
            state.OnlyShowDownloadedDlls = FindRequired<CheckBox>("OnlyDownloadedCheckBox").IsChecked == true;
            state.CardSize = Decimal.ToInt32(
                _gridCardSize.Value ?? ResponsiveGridLayout.DefaultCardSize);
            state.MediaWikiApiEndpoint = endpoint;
            state.MediaWikiImageHost = host;
            state.CustomScanPatterns = ParseLines(_customPatterns.Text).ToList();
            state.IgnoredPaths = ignoredPaths;
            state.ApplicationLoggingLevel = FindRequired<ComboBox>("ApplicationLogLevelComboBox").SelectedItem as string ?? "Error";
            state.AdditionalSteamRoots = ParseLines(_additionalSteamRoots.Text).ToList();
            state.ProviderWinePrefixes = prefixes.ToList();
            state.LegendaryConfigDirectories = directories.ToList();
            state.HeroicConfigDirectories = heroicDirectories.ToList();
            state.HeroicExecutable = string.IsNullOrEmpty(heroicExecutable) ? null : heroicExecutable;
        });
        if (!string.Equals(LanguageAppearance.Current?.Language, _library.State.Language, StringComparison.OrdinalIgnoreCase))
            LanguageAppearance.Apply(_library.State.Language);
        AppLog.ChangeLevel(_library.State.ApplicationLoggingLevel);
        _validation.Text = string.Empty;
        if (_liveSettings) Changed?.Invoke(); else Finished?.Invoke(true);
        }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _validation.Text = LanguageAppearance.Format("Linux_SettingsWindow_170", "Could not save settings: {0}", ex.Message); }
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Required settings control '{name}' is missing.");

    private async void AddIgnoredPath_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = true });
            var input = FindRequired<TextBox>("IgnoredPathsTextBox");
            input.Text = string.Join(Environment.NewLine, ParseLines(input.Text)
                .Concat(folders.Select(folder => folder.TryGetLocalPath() ?? throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingLocalFolder", "Choose a local folder."))))
                .Distinct(PathComparers.FileSystemPath));
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); _validation.Text = error.Message; }
    }

    private static string[] ParseLines(string? value) =>
        (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static string DisplayPattern(string pattern) =>
        pattern.Length == 0 ? LanguageAppearance.Get("Linux_GuiRemainingFolderRoot", "(game folder root)") : pattern;
}
