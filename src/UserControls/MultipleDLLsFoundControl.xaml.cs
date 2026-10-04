using DLSS_Swapper.Data;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public sealed partial class MultipleDLLsFoundControl : UserControl
{
    public MultipleDLLsFoundControlModel ViewModel { get; set; }

    public MultipleDLLsFoundControl(Game game, GameAssetType gameAssetType)
    {
        this.InitializeComponent();
        ViewModel = new MultipleDLLsFoundControlModel(game, gameAssetType);
        DataContext = ViewModel;
    }
}
