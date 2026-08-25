using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.UserControls;

public class BatchDllPickerControlModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string DontChangeText =>
        ResourceHelper.GetString("GamesPage_Batch_NoChange");

    [TranslationProperty]
    public string UpdateDetectedDllsToLatestText =>
        ResourceHelper.GetString("GamesPage_Batch_UpdateDetectedDllsToLatest");

    [TranslationProperty]
    public string UpdateDetectedDllsDescriptionText =>
        ResourceHelper.GetString("GamesPage_Batch_UpdateDetectedDllsDescription");

    [TranslationProperty]
    public string NvidiaPresetsText =>
        ResourceHelper.GetString("GamesPage_Batch_NvidiaPresets");

    [TranslationProperty]
    public string NvidiaPresetsDescriptionText =>
        ResourceHelper.GetString("GamesPage_Batch_NvidiaPresetsDescription");

    [TranslationProperty]
    public string DlssPresetText =>
        ResourceHelper.GetString("General_Name_DLSS_Preset");

    [TranslationProperty]
    public string DlssDPresetText =>
        ResourceHelper.GetString("General_Name_DLSSD_Preset");

    [TranslationProperty]
    public string DlssGPresetText =>
        ResourceHelper.GetString("General_Name_DLSSG_Preset");
}
