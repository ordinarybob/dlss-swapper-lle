using System.Threading.Tasks;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Pages;

public partial class GameGridPageModel
{
    public async Task InitialLoadAsync()
    {
        IsGameListLoading = true;
        IsDLSSLoading = true;

        try
        {
            ScanProgressText = ResourceHelper.GetString("General_Loading");
            await GameManager.Instance.LoadGamesFromCacheAsync();

            IsGameListLoading = false;
            gameGridPage.PrepareVisibleCoverWait();
            var runInitialDeepScan = Settings.Instance.HasCompletedInitialDeepScan == false;

            await LoadGamesWithProgressAsync(
                forceNeedsProcessing: runInitialDeepScan,
                exhaustiveScan: runInitialDeepScan,
                candidateLibraryReady: async () =>
                {
                    IsBackgroundScanRunning = true;
                    IsDLSSLoading = false;
                    await gameGridPage.WaitForVisibleCoverAsync();
                });

            if (runInitialDeepScan)
            {
                Settings.Instance.HasCompletedInitialDeepScan = true;
            }
        }
        finally
        {
            // Cache loading and cover preparation can fail before scanning starts.
            IsGameListLoading = false;
            IsBackgroundScanRunning = false;
            IsDLSSLoading = false;
            ScanProgressText = string.Empty;
            PublishVisibleGameCount();
        }
    }
}
