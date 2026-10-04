using Microsoft.UI.Xaml;

namespace DLSS_Swapper;

public sealed partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindowModel ViewModel { get; private set; }

    public DiagnosticsWindow()
    {
        this.InitializeComponent();
        ViewModel = new DiagnosticsWindowModel();
        Closed += OnCurrentWindowClosed;
    }

    private void OnCurrentWindowClosed(object sender, WindowEventArgs args)
    {
        ViewModel.TranslationProperties.Dispose();
    }
}
