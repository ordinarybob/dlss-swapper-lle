using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public partial class DLLPickerControlModel
{
    [RelayCommand]
    async Task SwapDllAsync()
    {
        var selectedRecord = SelectedDLLRecord;
        if (_applying || ResetDllCommand.IsRunning || selectedRecord?.LocalRecord is null)
        {
            return;
        }

        if (selectedRecord.LocalRecord.FileDownloader is not null)
        {
            ShowTempInfoBar(string.Empty, ResourceHelper.GetString("GamePage_DllPicker_WaitToDownloadBeforeSwapping"));
            return;
        }
        _applying = true;
        CanCloseParentDialog = false;
        CanSwap = false;
        _parentDialogWeakReference.TryGetTarget(out var dialog);
        var resetWasEnabled = dialog?.IsSecondaryButtonEnabled ?? false;
        if (dialog is not null) dialog.IsSecondaryButtonEnabled = false;
        try
        {
            if (!selectedRecord.LocalRecord.IsDownloaded)
            {
                ShowTempInfoBar(string.Empty, ResourceHelper.GetString("GamePage_DllPicker_StartingDownload"));
                var download = await selectedRecord.DownloadAsync();
                if (!download.Success)
                {
                    if (!download.Cancelled)
                        ShowTempInfoBar(ResourceHelper.GetString("General_Error"), download.Message, severity: InfoBarSeverity.Error);
                    return;
                }
            }

            var didUpdate = await Game.UpdateDllAsync(selectedRecord);
            if (!didUpdate.Success)
            {
                ShowTempInfoBar(ResourceHelper.GetString("General_Error"), didUpdate.Message, severity: InfoBarSeverity.Error);
                return;
            }
            CanCloseParentDialog = true;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            ShowTempInfoBar(ResourceHelper.GetString("General_Error"), err.Message, severity: InfoBarSeverity.Error);
        }
        finally
        {
            _applying = false;
            CanSwap = SelectedDLLRecord?.LocalRecord is not null;
            if (dialog is not null) dialog.IsSecondaryButtonEnabled = resetWasEnabled;
        }
        if (CanCloseParentDialog) dialog?.Hide();
    }
}
