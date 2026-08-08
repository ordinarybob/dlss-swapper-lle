using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Messages;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DLSS_Swapper.Collections;
using System.Collections.Specialized;
using DLSS_Swapper.Data.DLSS;
using DLSS_Swapper.Data.ManuallyAdded;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;

namespace DLSS_Swapper.Pages;

public partial class SettingsPageModel : ObservableObject
{
    readonly WeakReference<SettingsPage> _weakPage;
    readonly DLSSSettingsManager _dlssSettingsManager;
    public string CurrentLogPath => Logger.GetCurrentLogPath();
    public string AppVersion => App.CurrentApp.GetVersionString();

    [ObservableProperty]
    public partial ComboBoxOption SelectedDlssOnScreenIndicator { get; set; }

    public RefreshableObservableCollection<ComboBoxOption> DLSSOnScreenIndicatorOptions { get; init; } = new RefreshableObservableCollection<ComboBoxOption>()
    {
        new ComboBoxOption("General_None", 0),
        new ComboBoxOption("SettingsPage_DLSSDeveloperOptions_IndicatorEnabledForDebugDlssDllOnly", 1),
        new ComboBoxOption("SettingsPage_DLSSDeveloperOptions_IndicatorEnabledForAllDlssDlls", 1024)
    };

    PresetOption? _previousGlobalDlssPreset;
    PresetOption? _previousGlobalDlssDPreset;
    PresetOption? _previousGlobalDlssGPreset;

    public List<PresetOption> DlssPresetOptions { get; } = new List<PresetOption>();
    public List<PresetOption> DlssDPresetOptions { get; } = new List<PresetOption>();
    public List<PresetOption> DlssGPresetOptions { get; } = new List<PresetOption>();

    // Setting global preset for DLSS D does not perform as expected so it is currently disabled.

    [ObservableProperty]
    public partial PresetOption? SelectedGlobalDlssPreset { get; set; }
    
    [ObservableProperty]
    public partial PresetOption? SelectedGlobalDlssDPreset { get; set; }

    [ObservableProperty]
    public partial PresetOption? SelectedGlobalDlssGPreset { get; set; }

    public ObservableCollection<KeyValuePair<string, string>> Languages { get; init; } = new ObservableCollection<KeyValuePair<string, string>>();

    [ObservableProperty]
    public partial KeyValuePair<string, string> SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial bool LightThemeSelected { get; set; } = false;

    [ObservableProperty]
    public partial bool DarkThemeSelected { get; set; } = false;

    [ObservableProperty]
    public partial bool DefaultThemeSelected { get; set; } = false;

    [ObservableProperty]
    public partial bool DlssEnableLogging { get; set; } = false;

    [ObservableProperty]
    public partial bool DlssVerboseLogging { get; set; } = false;

    [ObservableProperty]
    public partial bool DlssLoggingToWindow { get; set; } = false;

    [ObservableProperty]
    public partial bool AllowUntrusted { get; set; } = false;

    [ObservableProperty]
    public partial bool AllowDebugDlls { get; set; } = false;

    [ObservableProperty]
    public partial bool OnlyShowDownloadedDlls { get; set; } = false;

    [ObservableProperty]
    public partial double RecursiveScanConcurrency { get; set; }

    [ObservableProperty]
    public partial double CoverHydrationConcurrency { get; set; }

    [ObservableProperty]
    public partial double UiCollectionBatchSize { get; set; }

    [ObservableProperty]
    public partial double DatabaseWriteBatchSize { get; set; }

    [ObservableProperty]
    public partial double BatchSwapConcurrency { get; set; }

    [ObservableProperty]
    public partial double GridViewPreferredColumns { get; set; }

    [ObservableProperty]
    public partial double GridViewPreferredRows { get; set; }

    [ObservableProperty]
    public partial ComboBoxOption LoggingLevel { get; set; }

    public RefreshableObservableCollection<ComboBoxOption> LoggingLevelOptions { get; init; } = new RefreshableObservableCollection<ComboBoxOption>()
    {
        new ComboBoxOption("SettingsPage_Logging_Off", (int)DLSS_Swapper.LoggingLevel.Off),
        new ComboBoxOption("SettingsPage_Logging_Verbose", (int)DLSS_Swapper.LoggingLevel.Verbose),
        new ComboBoxOption("SettingsPage_Logging_Debug", (int)DLSS_Swapper.LoggingLevel.Debug),
        new ComboBoxOption("SettingsPage_Logging_Info", (int)DLSS_Swapper.LoggingLevel.Info),
        new ComboBoxOption("SettingsPage_Logging_Warning", (int)DLSS_Swapper.LoggingLevel.Warning),
        new ComboBoxOption("SettingsPage_Logging_Error", (int)DLSS_Swapper.LoggingLevel.Error),
    };

    [ObservableProperty]
    public partial bool IsCheckingForUpdates { get; set; } = false;

    public ObservableCollection<string> IgnoredPaths { get; set; }

    public IReadOnlyList<string> BuiltInGameAssetDirectoryPatterns { get; } =
        GameAssetCandidatePathIndex.GetBuiltInDirectoryPatterns()
            .Select(static pattern => pattern.Length == 0 ? "(game folder root)" : pattern)
            .ToArray();

    public ObservableCollection<string> CustomGameAssetDirectoryPatterns { get; }

    [ObservableProperty]
    public partial string NewGameAssetDirectoryPattern { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GameAssetDirectoryPatternError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FallbackCoverArtApiUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FallbackCoverArtImageHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FallbackCoverArtSourceError { get; set; } = string.Empty;

    bool _hasSetDefaults;

    public SettingsPageModelTranslationProperties TranslationProperties { get; } = new SettingsPageModelTranslationProperties();

    public SettingsPageModel(SettingsPage page)
    {
        _weakPage = new WeakReference<SettingsPage>(page);
        WeakReferenceMessenger.Default.Register<GridDensityChangedMessage>(this, (sender, message) =>
        {
            GridViewPreferredColumns = Settings.Instance.GridViewPreferredColumns;
            GridViewPreferredRows = Settings.Instance.GridViewPreferredRows;
        });

        LanguageManager.Instance.OnLanguageChanged += () =>
        {
            DLSSOnScreenIndicatorOptions.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            LoggingLevelOptions.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            foreach (var dlssPresetOption in DlssPresetOptions)
            {
                dlssPresetOption.UpdateNameFromTranslation();
            }
            foreach (var dlssPresetOption in DlssDPresetOptions)
            {
                dlssPresetOption.UpdateNameFromTranslation();
            }
            foreach (var dlssPresetOption in DlssGPresetOptions)
            {
                dlssPresetOption.UpdateNameFromTranslation();
            }
        };

        var knownLanguages = LanguageManager.Instance.GetKnownLanguages();
        foreach (var knownLanguage in knownLanguages)
        {
            var languageName = LanguageManager.Instance.GetLanguageName(knownLanguage);
            Languages.Add(new KeyValuePair<string, string>(knownLanguage, languageName));
        }

        //work with selected language state
        SelectedLanguage = Languages.FirstOrDefault(x => x.Key == Settings.Instance.Language);

        _dlssSettingsManager = new DLSSSettingsManager();
        LightThemeSelected = Settings.Instance.AppTheme == ElementTheme.Light;
        DarkThemeSelected = Settings.Instance.AppTheme == ElementTheme.Dark;
        DefaultThemeSelected = Settings.Instance.AppTheme == ElementTheme.Default;

        var dlssShowOnScreenIndicatorIndicator = _dlssSettingsManager.GetShowDlssIndicator();
        SelectedDlssOnScreenIndicator = DLSSOnScreenIndicatorOptions.FirstOrDefault(x => x.Value == dlssShowOnScreenIndicatorIndicator) ?? DLSSOnScreenIndicatorOptions[0];

        var logLevel = _dlssSettingsManager.GetLogLevel();
        if (logLevel == 1)
        {
            DlssEnableLogging = true;
        }
        else if (logLevel == 2)
        {
            DlssEnableLogging = true;
            DlssVerboseLogging = true;
        }

        DlssLoggingToWindow = _dlssSettingsManager.GetLoggingWindow();
        AllowUntrusted = Settings.Instance.AllowUntrusted;
        AllowDebugDlls = Settings.Instance.AllowDebugDlls;
        OnlyShowDownloadedDlls = Settings.Instance.OnlyShowDownloadedDlls;
        RecursiveScanConcurrency = Settings.Instance.RecursiveScanConcurrency;
        CoverHydrationConcurrency = Settings.Instance.CoverHydrationConcurrency;
        UiCollectionBatchSize = Settings.Instance.UiCollectionBatchSize;
        DatabaseWriteBatchSize = Settings.Instance.DatabaseWriteBatchSize;
        BatchSwapConcurrency = Settings.Instance.BatchSwapConcurrency;
        GridViewPreferredColumns = Settings.Instance.GridViewPreferredColumns;
        GridViewPreferredRows = Settings.Instance.GridViewPreferredRows;


        var loggingLevel = Settings.Instance.LoggingLevel;
        LoggingLevel = LoggingLevelOptions.FirstOrDefault(x => x.Value == (int)loggingLevel) ?? LoggingLevelOptions.Last();

        IgnoredPaths = new ObservableCollection<string>(Settings.Instance.IgnoredPaths);
        var normalizedCustomPatterns = GameAssetCandidatePathIndex.NormalizeCustomDirectoryPatterns(
            Settings.Instance.CustomGameAssetDirectoryPatterns);
        if (Settings.Instance.CustomGameAssetDirectoryPatterns.SequenceEqual(
            normalizedCustomPatterns,
            StringComparer.OrdinalIgnoreCase) == false)
        {
            Settings.Instance.CustomGameAssetDirectoryPatterns = normalizedCustomPatterns;
        }

        CustomGameAssetDirectoryPatterns = new ObservableCollection<string>(
            normalizedCustomPatterns);
        FallbackCoverArtApiUrl = Settings.Instance.FallbackCoverArtApiUrl;
        FallbackCoverArtImageHost = Settings.Instance.FallbackCoverArtImageHost;

        if (NVAPIHelper.Instance.IsSupported)
        {
            DlssPresetOptions.AddRange(NVAPIHelper.Instance.DlssPresetOptions);
            var dlssGlobalPreset = NVAPIHelper.Instance.GetGlobalDLSSPreset();
            if (dlssGlobalPreset.Success)
            {
                SelectedGlobalDlssPreset = NVAPIHelper.Instance.DlssPresetOptions.FirstOrDefault(x => x.Value == dlssGlobalPreset.Result);
            }

            DlssDPresetOptions.AddRange(NVAPIHelper.Instance.DlssDPresetOptions);
            var dlssDGlobalPreset = NVAPIHelper.Instance.GetGlobalDLSSDPreset();
            if (dlssDGlobalPreset.Success)
            {
                SelectedGlobalDlssDPreset = NVAPIHelper.Instance.DlssDPresetOptions.FirstOrDefault(x => x.Value == dlssDGlobalPreset.Result);
            }

            DlssGPresetOptions.AddRange(NVAPIHelper.Instance.DlssGPresetOptions);
            var dlssGGlobalPreset = NVAPIHelper.Instance.GetGlobalDLSSGPreset();
            if (dlssGGlobalPreset.Success)
            {
                SelectedGlobalDlssGPreset = NVAPIHelper.Instance.DlssGPresetOptions.FirstOrDefault(x => x.Value == dlssGGlobalPreset.Result);
            }

        }
        else
        {
            var notSupportedPresetOption = new PresetOption(ResourceHelper.GetString("General_NotSupported"), 0);

            DlssPresetOptions.Add(notSupportedPresetOption);
            SelectedGlobalDlssPreset = notSupportedPresetOption;

            DlssDPresetOptions.AddRange(notSupportedPresetOption);
            SelectedGlobalDlssDPreset = notSupportedPresetOption;

            DlssGPresetOptions.AddRange(notSupportedPresetOption);
            SelectedGlobalDlssGPreset = notSupportedPresetOption;

        }

        _hasSetDefaults = true;
    }

    partial void OnSelectedGlobalDlssPresetChanging(PresetOption? value)
    {
        _previousGlobalDlssPreset = SelectedGlobalDlssPreset;
    }

    partial void OnSelectedGlobalDlssDPresetChanging(PresetOption? value)
    {
        _previousGlobalDlssDPreset = SelectedGlobalDlssDPreset;
    }

    partial void OnSelectedGlobalDlssGPresetChanging(PresetOption? value)
    {
        _previousGlobalDlssGPreset = SelectedGlobalDlssGPreset;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_hasSetDefaults == false)
        {
            return;
        }

        if (e.PropertyName == nameof(SelectedLanguage))
        {
            Settings.Instance.Language = SelectedLanguage.Key;
            LanguageManager.Instance.ChangeLanguage(SelectedLanguage.Key);
        }
        else if (e.PropertyName == nameof(LightThemeSelected))
        {
            if (LightThemeSelected == true)
            {
                Settings.Instance.AppTheme = ElementTheme.Light;
                ((App)Application.Current).WindowManager.UpdateColors(ElementTheme.Light);
            }
        }
        else if (e.PropertyName == nameof(DarkThemeSelected))
        {
            if (DarkThemeSelected == true)
            {
                Settings.Instance.AppTheme = ElementTheme.Dark;
                ((App)Application.Current).WindowManager.UpdateColors(ElementTheme.Dark);
            }
        }
        else if (e.PropertyName == nameof(DefaultThemeSelected))
        {
            if (DefaultThemeSelected == true)
            {
                Settings.Instance.AppTheme = ElementTheme.Default;
                ((App)Application.Current).WindowManager.UpdateColors(ElementTheme.Default);
            }
        }
        else if (e.PropertyName == nameof(SelectedDlssOnScreenIndicator))
        {
            _dlssSettingsManager.SetShowDlssIndicator(SelectedDlssOnScreenIndicator.Value);
        }
        else if (e.PropertyName == nameof(DlssEnableLogging) || e.PropertyName == nameof(DlssVerboseLogging))
        {
            if (DlssEnableLogging == true)
            {
                if (DlssVerboseLogging == true)
                {
                    _dlssSettingsManager.SetLogLevel(2);
                }
                else
                {
                    _dlssSettingsManager.SetLogLevel(1);
                }
            }
            else
            {
                _dlssSettingsManager.SetLogLevel(0);
            }
        }
        else if (e.PropertyName == nameof(DlssLoggingToWindow))
        {
            _dlssSettingsManager.SetLoggingWindow(DlssLoggingToWindow);
        }
        else if (e.PropertyName == nameof(AllowUntrusted))
        {
            Settings.Instance.AllowUntrusted = AllowUntrusted;
            App.CurrentApp.MainWindow.FilterDLLRecords();
        }
        else if (e.PropertyName == nameof(AllowDebugDlls))
        {
            Settings.Instance.AllowDebugDlls = AllowDebugDlls;
            App.CurrentApp.MainWindow.FilterDLLRecords();
        }
        else if (e.PropertyName == nameof(OnlyShowDownloadedDlls))
        {
            Settings.Instance.OnlyShowDownloadedDlls = OnlyShowDownloadedDlls;
        }
        else if (e.PropertyName == nameof(RecursiveScanConcurrency) && double.IsFinite(RecursiveScanConcurrency))
        {
            Settings.Instance.RecursiveScanConcurrency = (int)Math.Round(RecursiveScanConcurrency);
        }
        else if (e.PropertyName == nameof(CoverHydrationConcurrency) && double.IsFinite(CoverHydrationConcurrency))
        {
            Settings.Instance.CoverHydrationConcurrency = (int)Math.Round(CoverHydrationConcurrency);
        }
        else if (e.PropertyName == nameof(UiCollectionBatchSize) && double.IsFinite(UiCollectionBatchSize))
        {
            Settings.Instance.UiCollectionBatchSize = (int)Math.Round(UiCollectionBatchSize);
        }
        else if (e.PropertyName == nameof(DatabaseWriteBatchSize) && double.IsFinite(DatabaseWriteBatchSize))
        {
            Settings.Instance.DatabaseWriteBatchSize = (int)Math.Round(DatabaseWriteBatchSize);
        }
        else if (e.PropertyName == nameof(BatchSwapConcurrency) && double.IsFinite(BatchSwapConcurrency))
        {
            Settings.Instance.BatchSwapConcurrency = (int)Math.Round(BatchSwapConcurrency);
        }
        else if (e.PropertyName == nameof(GridViewPreferredColumns) && double.IsFinite(GridViewPreferredColumns))
        {
            Settings.Instance.GridViewPreferredColumns = (int)Math.Round(GridViewPreferredColumns);
        }
        else if (e.PropertyName == nameof(GridViewPreferredRows) && double.IsFinite(GridViewPreferredRows))
        {
            Settings.Instance.GridViewPreferredRows = (int)Math.Round(GridViewPreferredRows);
        }
        else if (e.PropertyName == nameof(LoggingLevel))
        {
            var loggingLevel  = (DLSS_Swapper.LoggingLevel)LoggingLevel.Value;
            Settings.Instance.LoggingLevel = loggingLevel;
            Logger.ChangeLoggingLevel(loggingLevel);
        }
        else if (e.PropertyName == nameof(SelectedGlobalDlssPreset))
        {
            if (NVAPIHelper.Instance.IsSupported && SelectedGlobalDlssPreset is not null)
            {
                var setDLSSPresetResult = NVAPIHelper.Instance.SetGlobalDLSSPreset(SelectedGlobalDlssPreset.Value);
                if (setDLSSPresetResult.Success == false)
                {
                    if (_weakPage.TryGetTarget(out var page))
                    {
                        page.DispatcherQueue.TryEnqueue(() =>
                        {
                            SelectedGlobalDlssPreset = _previousGlobalDlssPreset;
                        });

                        _ = NVAPIHelper.Instance.DisplayNVAPIErrorAsync(page.XamlRoot);
                    }
                }
            }
        }
        else if (e.PropertyName == nameof(SelectedGlobalDlssDPreset))
        {
            if (NVAPIHelper.Instance.IsSupported && SelectedGlobalDlssDPreset is not null)
            {
                var setDLSSDPresetResult = NVAPIHelper.Instance.SetGlobalDLSSDPreset(SelectedGlobalDlssDPreset.Value);
                if (setDLSSDPresetResult.Success == false)
                {
                    if (_weakPage.TryGetTarget(out var page))
                    {
                        page.DispatcherQueue.TryEnqueue(() =>
                        {
                            SelectedGlobalDlssDPreset = _previousGlobalDlssDPreset;
                        });

                        _ = NVAPIHelper.Instance.DisplayNVAPIErrorAsync(page.XamlRoot);
                    }
                }
            }
        }
        else if (e.PropertyName == nameof(SelectedGlobalDlssGPreset))
        {
            if (NVAPIHelper.Instance.IsSupported && SelectedGlobalDlssGPreset is not null)
            {
                var setDLSSGPresetResult = NVAPIHelper.Instance.SetGlobalDLSSGPreset(SelectedGlobalDlssGPreset.Value);
                if (setDLSSGPresetResult.Success == false)
                {
                    if (_weakPage.TryGetTarget(out var page))
                    {
                        page.DispatcherQueue.TryEnqueue(() =>
                        {
                            SelectedGlobalDlssGPreset = _previousGlobalDlssGPreset;
                        });

                        _ = NVAPIHelper.Instance.DisplayNVAPIErrorAsync(page.XamlRoot);
                    }
                }
            }
        }
    }

    [RelayCommand]
    void ResetPerformanceDefaults()
    {
        RecursiveScanConcurrency = Settings.DefaultRecursiveScanConcurrency;
        CoverHydrationConcurrency = Settings.DefaultCoverHydrationConcurrency;
        UiCollectionBatchSize = Settings.DefaultUiCollectionBatchSize;
        DatabaseWriteBatchSize = Settings.DefaultDatabaseWriteBatchSize;
        BatchSwapConcurrency = Settings.DefaultBatchSwapConcurrency;
    }

    [RelayCommand]
    async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        var githubUpdater = new Data.GitHub.GitHubUpdater();
        var newUpdate = await githubUpdater.CheckForNewGitHubRelease(true);

        if (_weakPage.TryGetTarget(out SettingsPage? settingsPage))
        {
            if (newUpdate is not null)
            {
                await githubUpdater.DisplayNewUpdateDialog(newUpdate, settingsPage.XamlRoot);
            }
            else
            {
                var dialog = new EasyContentDialog(settingsPage.XamlRoot)
                {
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetString("SettingsPage_NoNewUpdatesAvailable"),
                };
                await dialog.ShowAsync();

                IsCheckingForUpdates = false;
                return;
            }
        }

        IsCheckingForUpdates = false;
    }

    [RelayCommand]
    async Task OpenLogFileAsync()
    {
        try
        {
            if (File.Exists(CurrentLogPath))
            {
                FileSystemHelper.OpenFolderInExplorerSelectFile(CurrentLogPath);
            }
            else
            {
                FileSystemHelper.OpenFolderInExplorer(Logger.LogDirectory);
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);

            if (_weakPage.TryGetTarget(out SettingsPage? settingsPage))
            {
                var dialog = new EasyContentDialog(settingsPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Oops"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetString("SettingsPage_CouldNotOpenLogFileTryManual"),
                };

                await dialog.ShowAsync();
            }
        }
    }

    [RelayCommand]
    void OpenAcknowledgements()
    {
        App.CurrentApp.MainWindow.GoToAcknowledgements();
    }

    [RelayCommand]
    void OpenNetworkTester()
    {
        var networkTesterWindow = new NetworkTesterWindow();
        App.CurrentApp.WindowManager.ShowWindow(networkTesterWindow);
    }

    [RelayCommand]
    void OpenDiagnostics()
    {
        var diagnosticsWindow = new DiagnosticsWindow();
        App.CurrentApp.WindowManager.ShowWindow(diagnosticsWindow);
    }

    [RelayCommand]
    void AddGameAssetDirectoryPattern()
    {
        if (TryNormalizeGameAssetDirectoryPattern(
            NewGameAssetDirectoryPattern,
            out var normalizedPattern,
            out var error) == false)
        {
            GameAssetDirectoryPatternError = error;
            return;
        }

        if (GameAssetCandidatePathIndex.GetBuiltInDirectoryPatterns()
                .Contains(normalizedPattern, StringComparer.OrdinalIgnoreCase)
            || CustomGameAssetDirectoryPatterns.Contains(normalizedPattern, StringComparer.OrdinalIgnoreCase))
        {
            GameAssetDirectoryPatternError = "That directory pattern is already included.";
            return;
        }

        var normalizedCustomPatterns = GameAssetCandidatePathIndex.NormalizeCustomDirectoryPatterns(
            CustomGameAssetDirectoryPatterns.Append(normalizedPattern));
        if (normalizedCustomPatterns.Contains(normalizedPattern, StringComparer.OrdinalIgnoreCase) == false)
        {
            GameAssetDirectoryPatternError = "A broader directory pattern already includes that path.";
            return;
        }

        CustomGameAssetDirectoryPatterns.Clear();
        foreach (var pattern in normalizedCustomPatterns)
        {
            CustomGameAssetDirectoryPatterns.Add(pattern);
        }

        Settings.Instance.CustomGameAssetDirectoryPatterns = normalizedCustomPatterns;
        NewGameAssetDirectoryPattern = string.Empty;
        GameAssetDirectoryPatternError = string.Empty;
    }

    [RelayCommand]
    void SaveFallbackCoverArtSource()
    {
        if (WikipediaArtworkLookup.TryValidateSource(
            FallbackCoverArtApiUrl,
            FallbackCoverArtImageHost,
            out var apiEndpoint,
            out var normalizedImageHost,
            out var error) == false)
        {
            FallbackCoverArtSourceError = error;
            return;
        }

        FallbackCoverArtApiUrl = apiEndpoint!.AbsoluteUri;
        FallbackCoverArtImageHost = normalizedImageHost!;
        Settings.Instance.FallbackCoverArtApiUrl = FallbackCoverArtApiUrl;
        Settings.Instance.FallbackCoverArtImageHost = FallbackCoverArtImageHost;
        FallbackCoverArtSourceError = string.Empty;
    }

    [RelayCommand]
    void ResetFallbackCoverArtSource()
    {
        FallbackCoverArtApiUrl = Settings.DefaultFallbackCoverArtApiUrl;
        FallbackCoverArtImageHost = Settings.DefaultFallbackCoverArtImageHost;
        Settings.Instance.FallbackCoverArtApiUrl = FallbackCoverArtApiUrl;
        Settings.Instance.FallbackCoverArtImageHost = FallbackCoverArtImageHost;
        FallbackCoverArtSourceError = string.Empty;
    }

    [RelayCommand]
    void DeleteGameAssetDirectoryPattern(string pattern)
    {
        if (CustomGameAssetDirectoryPatterns.Remove(pattern))
        {
            Settings.Instance.CustomGameAssetDirectoryPatterns =
                CustomGameAssetDirectoryPatterns.ToArray();
        }
    }

    internal void RefreshGameAssetDirectoryPatterns()
    {
        var storedPatterns = Settings.Instance.CustomGameAssetDirectoryPatterns;
        if (CustomGameAssetDirectoryPatterns.SequenceEqual(
            storedPatterns,
            StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        CustomGameAssetDirectoryPatterns.Clear();
        foreach (var pattern in storedPatterns)
        {
            CustomGameAssetDirectoryPatterns.Add(pattern);
        }
    }

    static bool TryNormalizeGameAssetDirectoryPattern(
        string value,
        out string normalizedPattern,
        out string error)
    {
        normalizedPattern = value.Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);
        error = string.Empty;
        if (normalizedPattern.Length == 0)
        {
            error = "Enter a directory path relative to a game folder.";
            return false;
        }

        if (Path.IsPathRooted(normalizedPattern))
        {
            error = "Use a relative directory pattern, not a drive or absolute path.";
            return false;
        }

        var components = normalizedPattern.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var component in components)
        {
            if (component is "." or "..")
            {
                error = "Directory patterns cannot contain . or .. components.";
                return false;
            }

            if (component != "*"
                && (component.Contains('*')
                    || component.Contains('?')
                    || component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            {
                error = "Use literal directory names or * for one directory level.";
                return false;
            }
        }

        normalizedPattern = string.Join(Path.DirectorySeparatorChar, components);
        return true;
    }

    [RelayCommand]
    async Task AddIgnoredPathAsync(string path)
    {
        if (_weakPage.TryGetTarget(out SettingsPage? settingsPage))
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);

            var folderPath = string.Empty;
            try
            {
                var folder = FileSystemHelper.OpenFolder(hWnd, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

                // User cancelled.
                if (string.IsNullOrWhiteSpace(folder))
                {
                    return;
                }

                folderPath = folder;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                var errorDialog = new EasyContentDialog(settingsPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetString("SettingsPage_CouldNotOpenFolderDialog"),
                };
                await errorDialog.ShowAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            // Ensure ends with a directory separator
            if (folderPath.EndsWith(Path.DirectorySeparatorChar) == false)
            {
                folderPath += Path.DirectorySeparatorChar;
            }

            if (IgnoredPaths.Contains(folderPath))
            {
                var errorDialog = new EasyContentDialog(settingsPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetFormattedResourceTemplate("SettingsPage_PathAlreadyIgnored", folderPath),
                };
                await errorDialog.ShowAsync();
                return;
            }

            IgnoredPaths.Add(folderPath);
            Settings.Instance.IgnoredPaths = IgnoredPaths.ToArray();
        }
    }

    [RelayCommand]
    async Task DeleteIgnoredPathAsync(string path)
    {
        if (_weakPage.TryGetTarget(out SettingsPage? settingsPage))
        {
            var dialog = new EasyContentDialog(settingsPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("SettingsPage_DeleteIgnoredPathTitle"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                PrimaryButtonText = ResourceHelper.GetString("General_Delete"),
                Content = ResourceHelper.GetFormattedResourceTemplate("SettingsPage_DeleteIgnoredPathMessage", path),
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                IgnoredPaths.Remove(path);
                Settings.Instance.IgnoredPaths = IgnoredPaths.ToArray();
            }
        }
    }

    [RelayCommand]
    async Task ResetLocalAppDataAsync()
    {
        if (_weakPage.TryGetTarget(out SettingsPage? settingsPage) == false)
        {
            return;
        }

        var dialog = new EasyContentDialog(settingsPage.XamlRoot)
        {
            Title = "Reset all local app data?",
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            PrimaryButtonText = "Reset and restart",
            Content = "This permanently deletes all data stored by DLSS Swapper, including settings, discovered and manually added games, scan results, history, notes, favourites, hidden-game state, cached and custom covers, downloaded and imported DLLs, manifests, logs, temporary files, and saved proxy credentials.\n\nThe app will close and restart at the first-run performance selector. Game installations and .dlsss backup files are not changed.",
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (AppDataReset.TryStart(out var errorMessage))
        {
            App.CurrentApp.Exit();
            return;
        }

        var errorDialog = new EasyContentDialog(settingsPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("General_Error"),
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            DefaultButton = ContentDialogButton.Close,
            Content = $"DLSS Swapper could not start the reset process. No app data was deleted.\n\n{errorMessage}",
        };
        await errorDialog.ShowAsync();
    }

    [RelayCommand]
    void OpenTranslationToolbox()
    {
        var translationToolboxWindow = new TranslationToolboxWindow();
        App.CurrentApp.WindowManager.ShowWindow(translationToolboxWindow);
    }

    [RelayCommand]
    async Task DLSSPresetInfoAsync()
    {
        if (_weakPage.TryGetTarget(out var page))
        {
            var dialog = new EasyContentDialog(page.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamePage_DLSSPresetInfo_Title"),
                PrimaryButtonText = ResourceHelper.GetString("General_Okay"),
                SecondaryButtonText = ResourceHelper.GetString("GamePage_DLSSPresetInfo_OnScreenIndicator"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetString("GamePage_DLSSPresetInfo_Message"),
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/wiki/DLSS-Developer-Options#on-screen-indicator"));
            }
        }
    }

    [RelayCommand]
    async Task OpenVersionAsync()
    {
        if (string.IsNullOrWhiteSpace(BuildInfo.GitTag))
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/releases"));
        }
        else
        {
            await Launcher.LaunchUriAsync(new Uri($"https://github.com/beeradmoore/dlss-swapper/releases/tag/{BuildInfo.GitTag}"));
        }
    }

    [RelayCommand]
    void CopyGitCommit()
    {
        var package = new DataPackage();
        package.SetText(BuildInfo.GitCommitShort);
        Clipboard.SetContent(package);
    }

    [RelayCommand]
    async Task NVAPIErrorAsync()
    {
        if (_weakPage.TryGetTarget(out var page))
        {
            await NVAPIHelper.Instance.DisplayNVAPIErrorAsync(page.XamlRoot);
        }
    }

    [RelayCommand]
    async Task OpenProxySettingsAsync()
    {
        if (_weakPage.TryGetTarget(out var page))
        {
            var proxySettingsControl = new ProxySettingsControl();
            var dialog = new EasyContentDialog(page.XamlRoot)
            {
                Title = ResourceHelper.GetString("SettingsPage_ProxySettings"),
                PrimaryButtonText = ResourceHelper.GetString("General_Save"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Primary,
                Content = proxySettingsControl,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var server = proxySettingsControl.ViewModel.UseProxySettings ? proxySettingsControl.ViewModel.Server : null;
                var username = proxySettingsControl.ViewModel.UseProxySettings && proxySettingsControl.ViewModel.UseAuthentication ? proxySettingsControl.ViewModel.Username : null;
                var password = proxySettingsControl.ViewModel.UseProxySettings && proxySettingsControl.ViewModel.UseAuthentication ? proxySettingsControl.ViewModel.Password : null;
                Settings.ProxySettings.SaveIfRequired(server, username, password);
                App.CurrentApp.RegenerateHttpClient();
            }
        }
    }
}
