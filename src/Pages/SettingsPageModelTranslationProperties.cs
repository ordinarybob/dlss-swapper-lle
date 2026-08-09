using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Pages;

public class SettingsPageModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty] public string PerformanceText => ResourceHelper.GetString("SettingsPage_Performance");
    [TranslationProperty] public string PerformanceInfo => ResourceHelper.GetString("SettingsPage_PerformanceInfo");
    [TranslationProperty] public string ConcurrentGameScansText => ResourceHelper.GetString("SettingsPage_ConcurrentGameScans");
    [TranslationProperty] public string ConcurrentGameScansInfo => ResourceHelper.GetString("SettingsPage_ConcurrentGameScansInfo");
    [TranslationProperty] public string ConcurrentArtworkLoadingText => ResourceHelper.GetString("SettingsPage_ConcurrentArtworkLoading");
    [TranslationProperty] public string ConcurrentArtworkLoadingInfo => ResourceHelper.GetString("SettingsPage_ConcurrentArtworkLoadingInfo");
    [TranslationProperty] public string UiBatchText => ResourceHelper.GetString("SettingsPage_UiBatch");
    [TranslationProperty] public string UiBatchInfo => ResourceHelper.GetString("SettingsPage_UiBatchInfo");
    [TranslationProperty] public string DatabaseBatchText => ResourceHelper.GetString("SettingsPage_DatabaseBatch");
    [TranslationProperty] public string DatabaseBatchInfo => ResourceHelper.GetString("SettingsPage_DatabaseBatchInfo");
    [TranslationProperty] public string ConcurrentGameUpdatesText => ResourceHelper.GetString("SettingsPage_ConcurrentGameUpdates");
    [TranslationProperty] public string ConcurrentGameUpdatesInfo => ResourceHelper.GetString("SettingsPage_ConcurrentGameUpdatesInfo");
    [TranslationProperty] public string ResetPerformanceDefaultsText => ResourceHelper.GetString("SettingsPage_ResetPerformanceDefaults");
    [TranslationProperty] public string FallbackCoverArtText => ResourceHelper.GetString("SettingsPage_FallbackCoverArt");
    [TranslationProperty] public string FallbackCoverArtInfo => ResourceHelper.GetString("SettingsPage_FallbackCoverArtInfo");
    [TranslationProperty] public string MediaWikiApiEndpointText => ResourceHelper.GetString("SettingsPage_MediaWikiApiEndpoint");
    [TranslationProperty] public string AllowedImageHostText => ResourceHelper.GetString("SettingsPage_AllowedImageHost");
    [TranslationProperty] public string SaveSourceText => ResourceHelper.GetString("SettingsPage_SaveSource");
    [TranslationProperty] public string ResetDefaultText => ResourceHelper.GetString("SettingsPage_ResetDefault");
    [TranslationProperty] public string FastScanPathsText => ResourceHelper.GetString("SettingsPage_FastScanPaths");
    [TranslationProperty] public string FastScanPathsInfo => ResourceHelper.GetString("SettingsPage_FastScanPathsInfo");
    [TranslationProperty] public string BuiltInPatternsText => ResourceHelper.GetString("SettingsPage_BuiltInPatterns");
    [TranslationProperty] public string LearnedCustomPatternsText => ResourceHelper.GetString("SettingsPage_LearnedCustomPatterns");
    [TranslationProperty] public string PatternExampleText => ResourceHelper.GetString("SettingsPage_PatternExample");
    [TranslationProperty] public string AddPatternText => ResourceHelper.GetString("SettingsPage_AddPattern");
    [TranslationProperty] public string RemoveText => ResourceHelper.GetString("General_Remove");
    [TranslationProperty] public string GamesGridText => ResourceHelper.GetString("SettingsPage_GamesGrid");
    [TranslationProperty] public string GamesGridInfo => ResourceHelper.GetString("SettingsPage_GamesGridInfo");
    [TranslationProperty] public string CardSizeText => ResourceHelper.GetString("SettingsPage_CardSize");
    [TranslationProperty] public string CardSizeInfo => ResourceHelper.GetString("SettingsPage_CardSizeInfo");
    [TranslationProperty] public string ResetLocalDataText => ResourceHelper.GetString("SettingsPage_ResetLocalData");
    [TranslationProperty] public string ResetLocalDataInfo => ResourceHelper.GetString("SettingsPage_ResetLocalDataInfo");
    [TranslationProperty] public string ResetAllLocalDataText => ResourceHelper.GetString("SettingsPage_ResetAllLocalData");

    [TranslationProperty]
    public string VersionText => $"{ResourceHelper.GetString("General_Version")}:";

    [TranslationProperty]
    public string BuildDateText => $"{ResourceHelper.GetString("SettingsPage_BuildDate")}:";

    [TranslationProperty]
    public string BuildCommitText => $"{ResourceHelper.GetString("SettingsPage_BuildCommit")}:";

    [TranslationProperty]
    public string CopyText => ResourceHelper.GetString("General_Copy");

    [TranslationProperty]
    public string LleCheckpointInfo => ResourceHelper.GetString("SettingsPage_LleCheckpointInfo");

    [TranslationProperty]
    public string LleRepositoryLabel => ResourceHelper.GetString("SettingsPage_LleRepositoryLabel");

    [TranslationProperty]
    public string UpstreamSourceLabel => ResourceHelper.GetString("SettingsPage_UpstreamSourceLabel");

    [TranslationProperty]
    public string NetworkTesterText => ResourceHelper.GetString("SettingsPage_OpenNetworkTester");

    [TranslationProperty]
    public string GeneralTroubleshootingGuideText => ResourceHelper.GetString("SettingsPage_GeneralTroubleshootingGuide");

    [TranslationProperty]
    public string DiagnosticsText => ResourceHelper.GetString("SettingsPage_OpenDiagnostics");

    [TranslationProperty]
    public string AcknowledgementsText => ResourceHelper.GetString("SettingsPage_OpenAcknowledgements");

    [TranslationProperty]
    public string AllowDebugDllsInfo => ResourceHelper.GetString("SettingsPage_AllowDebugDllsInfo");

    [TranslationProperty]
    public string AllowUntrustedInfo => ResourceHelper.GetString("SettingsPage_AllowUntrustedInfo");

    [TranslationProperty]
    public string ApplicationRunsInAdministrativeModeInfo => ResourceHelper.GetString("General_ApplicationRunningAsAdmin");

    [TranslationProperty]
    public string WarningText => ResourceHelper.GetString("General_Warning");

    [TranslationProperty]
    public string YourCurrentLogfileText => ResourceHelper.GetString("SettingsPage_YourCurrentLogFile");

    [TranslationProperty]
    public string OpenTranslationToolboxText => ResourceHelper.GetString("SettingsPage_OpenTranslationToolbox");

    [TranslationProperty]
    public string ThemeLightText => ResourceHelper.GetString("SettingsPage_ThemeLight");

    [TranslationProperty]
    public string ThemeDarkText => ResourceHelper.GetString("SettingsPage_ThemeDark");

    [TranslationProperty]
    public string ThemeSystemSettingDefaultText => ResourceHelper.GetString("SettingsPage_ThemeSystemSettingDefault");

    [TranslationProperty]
    public string ThemeModeText => ResourceHelper.GetString("SettingsPage_ThemeMode");

    [TranslationProperty]
    public string GameLibrariesText => ResourceHelper.GetString("SettingsPage_GameLibraries");

    [TranslationProperty]
    public string IgnoredPathsText => ResourceHelper.GetString("SettingsPage_IgnoredPaths");

    [TranslationProperty]
    public string AddIgnoredPathText => ResourceHelper.GetString("SettingsPage_AddIgnoredPath");

    [TranslationProperty]
    public string DLSSOptionsText => ResourceHelper.GetString("SettingsPage_DLSSOptions");

    [TranslationProperty]
    public string DLSSOptionsGlobalPresetText => ResourceHelper.GetString("SettingsPage_DLSSOptions_GlobalPreset");

    [TranslationProperty]
    public string DLSSDOptionsGlobalPresetText => ResourceHelper.GetString("SettingsPage_DLSSDOptions_GlobalPreset");

    [TranslationProperty]
    public string DLSSGOptionsGlobalPresetText => ResourceHelper.GetString("SettingsPage_DLSSGOptions_GlobalPreset");

    [TranslationProperty]
    public string DLSSDeveloperOptionsText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions");

    [TranslationProperty]
    public string ShowOnScreenIndicatorText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_ShowOnScreenIndicator");

    [TranslationProperty]
    public string VerboseLoggingText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_VerboseLogging");

    [TranslationProperty]
    public string EnableLoggingToFileText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_EnableLoggingToFile");

    [TranslationProperty]
    public string EnableLoggingToConsoleWindowText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_EnableLoggingToConsoleWindow");

    [TranslationProperty]
    public string AllowUntrustedText => ResourceHelper.GetString("SettingsPage_SettingsAllowUntrusted");

    [TranslationProperty]
    public string AllowDebugDllsText => ResourceHelper.GetString("SettingsPage_AllowDebugDlls");

    [TranslationProperty]
    public string ShowOnlyDownloadedDllsText => ResourceHelper.GetString("SettingsPage_ShowOnlyDownloadedDlls");

    [TranslationProperty]
    public string ApliesOnlyToDllPickerNotLibraryText => ResourceHelper.GetString("SettingsPage_AppliesOnlyToDllPickerNotLibrary");

    [TranslationProperty]
    public string TroubleshootingText => ResourceHelper.GetString("SettingsPage_Troubleshooting");

    [TranslationProperty]
    public string PageTitle => ResourceHelper.GetString("SettingsPage_Title");

    [TranslationProperty]
    public string LoggingText => ResourceHelper.GetString("SettingsPage_Logging");

    [TranslationProperty]
    public string AboutText => ResourceHelper.GetString("SettingsPage_About");

    [TranslationProperty]
    public string YesText => ResourceHelper.GetString("General_Yes");

    [TranslationProperty]
    public string NoText => ResourceHelper.GetString("General_No");

    [TranslationProperty]
    public string LanguageText => ResourceHelper.GetString("SettingsPage_Language");

    [TranslationProperty]
    public string DLSSPresetInfoTooltipText => ResourceHelper.GetString("GamePage_DLSSPresetInfo_Tooltip");

    [TranslationProperty]
    public string DLSSPresetInfoText => ResourceHelper.GetString("GamePage_DLSSPresetInfo");

    [TranslationProperty]
    public string NVAPIErrorTooltipText => ResourceHelper.GetString("GamePage_NVAPIError_Tooltip");

    [TranslationProperty]
    public string NetworkingText => ResourceHelper.GetString("SettingsPage_Networking");

    [TranslationProperty]
    public string ProxySettingsText => ResourceHelper.GetString("SettingsPage_ProxySettings");
}
