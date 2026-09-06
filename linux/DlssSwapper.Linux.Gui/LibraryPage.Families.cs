using Avalonia.Controls;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class LibraryPage
{
    private bool ShowSdkFamily()
    {
        var sdkSelected = _familyComboBox.SelectedIndex == DllTypes.All.Count + 1;
        FindRequired<Border>("StreamlinePanel").IsVisible = sdkSelected;
        FindRequired<Border>("DllPanel").IsVisible = !sdkSelected;
        _searchTextBox.IsEnabled = !sdkSelected;
        return sdkSelected;
    }
}
