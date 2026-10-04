using Microsoft.UI.Xaml;

namespace DLSS_Swapper;

public sealed partial class NetworkTesterWindow : Window
{
    public NetworkTesterWindowModel ViewModel { get; private set; }

    public NetworkTesterWindow()
    {
        ViewModel = new NetworkTesterWindowModel(this);
        this.InitializeComponent();
        Closed += OnCurrentWindowClosed;
    }

    private void OnCurrentWindowClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnCurrentWindowClosed;
        ViewModel.Dispose();
    }
}
