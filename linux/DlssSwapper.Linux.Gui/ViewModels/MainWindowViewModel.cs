using System.Collections.ObjectModel;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusText = "Loading the bundled DLL catalog…";
    private bool _isBusy;
    private bool _hasReadyPreview;

    public ObservableCollection<GameRowViewModel> Games { get; } = [];

    public ObservableCollection<ResultRowViewModel> Results { get; } = [];

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanInteract));
                OnPropertyChanged(nameof(CanApplyPreview));
            }
        }
    }

    public bool HasReadyPreview
    {
        get => _hasReadyPreview;
        set
        {
            if (SetProperty(ref _hasReadyPreview, value))
            {
                OnPropertyChanged(nameof(CanApplyPreview));
            }
        }
    }

    public bool CanInteract => !IsBusy;

    public bool CanApplyPreview => !IsBusy && HasReadyPreview;
}
