using System;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.Pages;

public sealed partial class LibraryPage : Page
{
    public static string PageTag { get; } = "PageTag_Library";

    public LibraryPageModel ViewModel { get; private set; }

    public LibraryPage()
    {
        this.InitializeComponent();
        ViewModel = new LibraryPageModel(this);
        Loaded += async (_, _) =>
        {
            ViewModel.StartDownloadProgress();
            await ViewModel.RefreshStreamlineAsync();
        };
        Unloaded += (_, _) => ViewModel.StopDownloadProgress();
    }

    void MainGridView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // via: https://stackoverflow.com/a/41141249
        if (MainGridView.ItemsPanelRoot is not ItemsWrapGrid itemsPanel
            || e.NewSize.Width <= 0)
        {
            return;
        }

        var columns = Math.Max(1, Math.Ceiling(e.NewSize.Width / 400));
        itemsPanel.ItemWidth = Math.Max(1, (e.NewSize.Width / columns) - 1);
    }

    private void MainGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DLLRecord dllRecord)
        {
            var dialog = new EasyContentDialog(XamlRoot)
            {
                Title = DLLManager.Instance.GetAssetTypeName(dllRecord.AssetType),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                Content = new DLLRecordInfoControl(dllRecord),
            };
            _ = dialog.ShowAsync();
        }
    }
}
