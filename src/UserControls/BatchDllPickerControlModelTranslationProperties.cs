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
}
