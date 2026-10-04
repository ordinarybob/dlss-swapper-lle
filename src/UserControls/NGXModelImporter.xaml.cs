using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using DLSS_Swapper.Data.NVIDIA;

namespace DLSS_Swapper.UserControls;

public sealed partial class NGXModelImporter : UserControl
{
    public NGXModelImporterModel ViewModel { get; private set; }

    public NGXModelImporter(List<NGXModel> models)
    {
        InitializeComponent();

        ViewModel = new NGXModelImporterModel(this, models);
        DataContext = ViewModel;
    }
}
