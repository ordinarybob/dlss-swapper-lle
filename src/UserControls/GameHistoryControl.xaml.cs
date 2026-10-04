using System;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace DLSS_Swapper.UserControls;
public sealed partial class GameHistoryControl : UserControl, IDisposable
{
    public GameHistoryControlModel ViewModel { get; private set; }

    public GameHistoryControl(Game game)
    {
        InitializeComponent();
        ViewModel = new GameHistoryControlModel(game);
        DataContext = ViewModel;
    }

    public Task LoadAsync() => ViewModel.LoadAsync();

    public void Dispose() => ViewModel.TranslationProperties.Dispose();
}
