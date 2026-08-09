using System.Collections.ObjectModel;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusText = "Loading the bundled DLL catalog…";
    private bool _isBusy;
    private bool _isLoadingLibrary = true;
    private bool _hasReadyPreview;
    private int _gameCount;
    private bool _isGridView = true;
    private int _gridColumns = 6;
    private int _gridRows = 5;
    private bool _isBatchMode;
    private int _selectedCount;

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

    public bool IsGridView
    {
        get => _isGridView;
        set
        {
            if (SetProperty(ref _isGridView, value))
            {
                OnPropertyChanged(nameof(IsListView));
            }
        }
    }

    public bool IsListView => !IsGridView;

    public int GridColumns
    {
        get => _gridColumns;
        set => SetProperty(ref _gridColumns, value);
    }

    public int GridRows
    {
        get => _gridRows;
        set => SetProperty(ref _gridRows, value);
    }

    public bool IsBatchMode
    {
        get => _isBatchMode;
        set => SetProperty(ref _isBatchMode, value);
    }

    public int SelectedCount
    {
        get => _selectedCount;
        set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                OnPropertyChanged(nameof(SelectionHeading));
            }
        }
    }

    public string SelectionHeading => $"{SelectedCount} selected";

    public bool CanInteract => !IsBusy;

    public bool CanApplyPreview => !IsBusy && HasReadyPreview;
}
