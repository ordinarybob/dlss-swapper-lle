using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Pages;

public class GameGridPageModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string NewDllsText => ResourceHelper.GetString("GamesPage_NewDlls");

    [TranslationProperty]
    public string AddGameText => ResourceHelper.GetString("GamesPage_AddGame");

    [TranslationProperty]
    public string AddSingleGameText => ResourceHelper.GetString("GamesPage_ManuallyAdding_AddSingleGame");

    [TranslationProperty]
    public string AddMultipleGameFoldersText => ResourceHelper.GetString("GamesPage_ManuallyAdding_AddMultipleGameFolders");

    [TranslationProperty]
    public string AddMultiGameDirectoryText => ResourceHelper.GetString("GamesPage_ManuallyAdding_AddMultiGameDirectory");

    [TranslationProperty]
    public string RefreshText => ResourceHelper.GetString("General_Refresh");

    [TranslationProperty]
    public string RefreshPreserveExcludedText => ResourceHelper.GetString("GamesPage_Refresh_PreserveExcluded");

    [TranslationProperty]
    public string RestoreExcludedAndRefreshText => ResourceHelper.GetString("GamesPage_Refresh_RestoreExcluded");

    [TranslationProperty]
    public string FilterText => ResourceHelper.GetString("General_Filter");

    [TranslationProperty]
    public string SearchText => ResourceHelper.GetString("General_Search");

    [TranslationProperty]
    public string ViewTypeText => ResourceHelper.GetString("GamesPage_ViewType");

    [TranslationProperty]
    public string GridViewText => ResourceHelper.GetString("GamesPage_ViewType_GridView");

    [TranslationProperty]
    public string ListViewText => ResourceHelper.GetString("GamesPage_ViewType_ListView");

    [TranslationProperty]
    public string PageTitle => ResourceHelper.GetString("GamesPage_Title");

    [TranslationProperty]
    public string ApplicationRunsInAdministrativeModeInfo => ResourceHelper.GetString("General_ApplicationRunningAsAdmin");

    [TranslationProperty]
    public string SelectGamesText => ResourceHelper.GetString("GamesPage_SelectionMode_SelectGames");

    [TranslationProperty]
    public string ApplyDllText => ResourceHelper.GetString("GamesPage_SelectionMode_ApplyDll");

    [TranslationProperty]
    public string CloseText => ResourceHelper.GetString("General_Close");
}
