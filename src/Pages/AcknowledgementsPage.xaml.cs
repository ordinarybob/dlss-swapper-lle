using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.Pages;

public sealed partial class AcknowledgementsPage : Page
{
    public static string PageTag { get; } = "PageTag_Acknowledgements";

    public AcknowledgementsPageModel ViewModel { get; private set; }
    public AcknowledgementsPage()
    {
        this.InitializeComponent();
        ViewModel = new AcknowledgementsPageModel(this);
    }
}
