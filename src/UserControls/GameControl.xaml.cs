using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DLSS_Swapper.UserControls;

public sealed partial class GameControl : FakeContentDialog
{
    const double CompactDialogWidth = 700;
    const double WideDialogWidth = 916;
    const double WideDialogViewportThreshold = 980;
    const double DialogViewportMargin = 16;

    FrameworkElement? dialogBackground;

    public GameControlModel ViewModel { get; private set; }

    public GameControl(Game game)
    {
        this.InitializeComponent();

        Resources["ContentDialogMinWidth"] = 0;
        Resources["ContentDialogMaxWidth"] = WideDialogWidth;
        Resources["ContentDialogPadding"] = new Thickness(16, 12, 16, 12);

        SizeChanged += GameControl_SizeChanged;

        ViewModel = new GameControlModel(this, game);
        DataContext = ViewModel;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("SmokeLayerBackground") is FrameworkElement smokeLayerBackground)
        {
            smokeLayerBackground.Tapped -= SmokeLayerBackground_Tapped;
            smokeLayerBackground.Tapped += SmokeLayerBackground_Tapped;
        }

        dialogBackground = GetTemplateChild("BackgroundElement") as FrameworkElement;
        UpdateDialogBounds(ActualWidth, ActualHeight);

        if (GetTemplateChild("ContentScrollViewer") is ScrollViewer contentScrollViewer)
        {
            contentScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        var dialogSpace = this.GetTemplateChild("DialogSpace") as Grid;

        if (dialogSpace is not null
            && dialogSpace.Children
                .OfType<FrameworkElement>()
                .Any(child => child.Name == "GameDialogFooterButtons") == false)
        {
            var footerButtons = new ContentControl()
            {
                Name = "GameDialogFooterButtons",
                Template = Resources["FooterButtonsControlTemplate"] as ControlTemplate,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            footerButtons.DataContext = DataContext;
            Grid.SetRow(footerButtons, 1);
            dialogSpace.Children.Add(footerButtons);
        }
    }

    void GameControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDialogBounds(e.NewSize.Width, e.NewSize.Height);
    }

    void UpdateDialogBounds(double viewportWidth, double viewportHeight)
    {
        if (dialogBackground is null || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(0, viewportWidth - (DialogViewportMargin * 2));
        var availableHeight = Math.Max(0, viewportHeight - (DialogViewportMargin * 2));

        dialogBackground.MinWidth = 0;
        var preferredWidth = viewportWidth >= WideDialogViewportThreshold
            ? WideDialogWidth
            : CompactDialogWidth;

        dialogBackground.Width = Math.Min(preferredWidth, availableWidth);
        dialogBackground.MaxWidth = availableWidth;
        dialogBackground.MinHeight = 0;
        dialogBackground.MaxHeight = availableHeight;
    }

    void SmokeLayerBackground_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.CloseCommand.CanExecute(null))
        {
            ViewModel.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    string[] customCoverValidFileTypes = new string[]
    {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".bmp",
    };

    DataPackageOperation coverDragDropAcceptedOperation = DataPackageOperation.None;
    string coverDragDropDragUIOverrideCaption = string.Empty;

    async void CoverButton_DragEnter(object sender, DragEventArgs e)
    {

        // DragOver reapplies the result after async validation completes.
        // See https://github.com/microsoft/microsoft-ui-xaml/issues/8108

        coverDragDropAcceptedOperation = DataPackageOperation.None;
        coverDragDropDragUIOverrideCaption = string.Empty;

        e.AcceptedOperation = coverDragDropAcceptedOperation;
        e.DragUIOverride.Caption = coverDragDropDragUIOverrideCaption;

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 1)
        {
            var storageFile = items[0] as StorageFile;

            if (storageFile is null)
            {
                coverDragDropAcceptedOperation = DataPackageOperation.None;
                coverDragDropDragUIOverrideCaption = ResourceHelper.GetString("GamePage_StorageFileIsNull");
            }
            else if (customCoverValidFileTypes.Contains(storageFile.FileType.ToLower()) == true)
            {
                coverDragDropAcceptedOperation = DataPackageOperation.Copy;
                coverDragDropDragUIOverrideCaption = ResourceHelper.GetString("GamePage_AddCustomCover");
            }
            else
            {
                coverDragDropAcceptedOperation = DataPackageOperation.None;
                coverDragDropDragUIOverrideCaption = ResourceHelper.GetFormattedResourceTemplate("GamePage_InvalidFileTypeTemplate", storageFile.FileType);
            }
        }
        else
        {
            coverDragDropAcceptedOperation = DataPackageOperation.None;
            coverDragDropDragUIOverrideCaption = ResourceHelper.GetString("GamePage_YouMayOnlyDragOneFileCover");
        }
    }

    void CoverButton_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = coverDragDropAcceptedOperation;
        e.DragUIOverride.Caption = coverDragDropDragUIOverrideCaption;
    }

    async void CoverButton_Drop(object sender, DragEventArgs e)
    {
        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 1)
        {
            var storageFile = items[0] as StorageFile;
            if (storageFile is null)
            {
                Logger.Error("storageFile is null");
            }
            else if (customCoverValidFileTypes.Contains(storageFile.FileType.ToLower()) == true)
            {
                using (var stream = await storageFile.OpenStreamForReadAsync())
                {
                    if (DataContext is GameControlModel gameControlModel)
                    {
                        gameControlModel.Game.AddCustomCover(stream);
                    }
                }
            }
            else
            {
                Logger.Error($"\"{storageFile.FileType}\" is an invalid file type");
            }
        }
        else
        {
            Logger.Error("You may only drag over a single cover");
        }
    }

}
