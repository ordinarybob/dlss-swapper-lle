using System.Collections.Generic;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public sealed partial class BatchDllPickerControl : UserControl
{
    internal BatchDllPickerControlModel ViewModel { get; }

    public BatchDllPickerControl(
        EasyContentDialog parentDialog,
        IReadOnlyList<Game> games)
    {
        this.InitializeComponent();
        ViewModel = new BatchDllPickerControlModel(parentDialog, games);
        DataContext = ViewModel;
    }
}
