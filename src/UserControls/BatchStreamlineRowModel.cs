using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data.Streamline;

namespace DLSS_Swapper.UserControls;

public partial class BatchStreamlineRowModel(string fileName) : ObservableObject
{
    public string FileName { get; } = fileName;
    public string Description => StreamlineComponentDescriptions.GetDescription(FileName);
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
