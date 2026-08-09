using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper;

public class FailToLaunchWindowModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string ApplicationFailToLaunchWindowText => $"{ResourceHelper.GetString("ApplicationTitle")} - {ResourceHelper.GetString("FailedToLaunchPage_WindowTitle")}";

    [TranslationProperty]
    public string CopyDiagnosticsGuidanceText => ResourceHelper.GetString("FailedToLaunchPage_CopyDiagnosticsGuidance");

    [TranslationProperty]
    public string ClickToCopyDetailsText => ResourceHelper.GetString("DiagnosticsPage_ClickToCopyDetails");

    [TranslationProperty]
    public string DlssSwapperFailedToLaunchText => ResourceHelper.GetString("FailedToLaunchPage_DlssSwapperFailedToLaunch");
}
