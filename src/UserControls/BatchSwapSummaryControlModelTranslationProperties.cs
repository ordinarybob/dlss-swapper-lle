using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.UserControls;

public class BatchSwapSummaryControlModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string SwappedText => ResourceHelper.GetString("GamesPage_Batch_Summary_Swapped");

    [TranslationProperty]
    public string PresetsAppliedText => ResourceHelper.GetString("GamesPage_Batch_Summary_PresetsApplied");

    [TranslationProperty]
    public string AlreadyCurrentText => ResourceHelper.GetString("GamesPage_Batch_Summary_AlreadyCurrent");

    [TranslationProperty]
    public string SkippedText => ResourceHelper.GetString("GamesPage_Batch_Summary_Skipped");

    [TranslationProperty]
    public string ErrorsText => ResourceHelper.GetString("GamesPage_Batch_Summary_Errors");

    [TranslationProperty]
    public string ResultsText => ResourceHelper.GetString("GamesPage_Batch_Summary_Results");

    [TranslationProperty]
    public string CopyAllText => ResourceHelper.GetString("GamesPage_Batch_Summary_CopyAll");

    [TranslationProperty]
    public string SaveReportText => ResourceHelper.GetString("GamesPage_Batch_Summary_SaveReport");

    [TranslationProperty]
    public string SuccessText => ResourceHelper.GetString("General_Success");

    [TranslationProperty]
    public string ErrorText => ResourceHelper.GetString("General_Error");
}
