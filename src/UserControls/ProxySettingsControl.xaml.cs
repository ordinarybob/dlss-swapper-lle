using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public sealed partial class ProxySettingsControl : UserControl
{
    public ProxySettingsModel ViewModel { get; }
    public ProxySettingsControl()
    {
        InitializeComponent();

        ViewModel = new ProxySettingsModel();
        DataContext = ViewModel;
    }
}
