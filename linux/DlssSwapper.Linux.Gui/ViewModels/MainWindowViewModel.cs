using System.Collections.ObjectModel;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusText = "Loading the bundled DLL catalog…";
    private bool _isBusy;
    private bool _isLoadingLibrary = true;
    private bool _hasReadyPreview;
    private int _gameCount;

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

    public bool IsLoadingLibrary
    {
        get => _isLoadingLibrary;
        set
        {
            if (SetProperty(ref _isLoadingLibrary, value))
            {
                OnPropertyChanged(nameof(GamesHeading));
            }
        }
    }

    public int GameCount
    {
        get => _gameCount;
        set
        {
            if (SetProperty(ref _gameCount, value))
            {
                OnPropertyChanged(nameof(GamesHeading));
            }
        }
    }

    public string GamesHeading => IsLoadingLibrary
        ? "Games · Loading"
        : $"Games ({GameCount})";

    public bool CanInteract => !IsBusy;

    public bool CanApplyPreview => !IsBusy && HasReadyPreview;
}
