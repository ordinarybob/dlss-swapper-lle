using Microsoft.UI.Xaml.Controls;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.UserControls;

public sealed partial class DLLPickerControl : UserControl
{
    public DLLPickerControlModel ViewModel { get; private set; }

    public DLLPickerControl(GameControl gameControl, EasyContentDialog parentDialog, Game game, GameAssetType gameAssetType)
    {
        this.InitializeComponent();

        ViewModel = new DLLPickerControlModel(gameControl, parentDialog, this, game, gameAssetType);
        DataContext = ViewModel;
    }
}
