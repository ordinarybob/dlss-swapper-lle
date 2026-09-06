using System.Collections.ObjectModel;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusText = LanguageAppearance.Get("Linux_LoadingCatalog", "Loading the bundled DLL catalog…");
    private bool _isBusy;
    private bool _isLoadingLibrary = true;
    private int _gameCount;
    private bool _isGridView = true;
    private int _gridCardSize = 5;
    private double _gridItemWidth = 118;
    private double _gridItemHeight = 180;
    private bool _isBatchMode;
    private int _selectedCount;
    private string _alertText = string.Empty;
    private string _startupError = string.Empty;

    public string StartupError
    {
        get => _startupError;
        set
        {
            if (SetProperty(ref _startupError, value))
                OnPropertyChanged(nameof(HasStartupError));
        }
    }

    public bool HasStartupError => !string.IsNullOrEmpty(StartupError);

    public ObservableCollection<GameRowViewModel> Games { get; } = [];
    public ObservableCollection<DlssSwapper.Shared.GameGroup<GameRowViewModel>> GameGroups { get; } = [];

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HeaderStatusText));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanInteract));
                OnPropertyChanged(nameof(CanActOnSelection));
                OnPropertyChanged(nameof(HeaderStatusText));
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
                OnPropertyChanged(nameof(VisibleGameCountText));
                OnPropertyChanged(nameof(HeaderStatusText));
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
                OnPropertyChanged(nameof(VisibleGameCountText));
            }
        }
    }

    public string GamesHeading => IsLoadingLibrary
        ? $"{LanguageAppearance.Get("GamesPage_Title", "Games")} · {LanguageAppearance.Get("General_Loading", "Loading")}"
        : $"{LanguageAppearance.Get("GamesPage_Title", "Games")} ({GameCount})";

    public string VisibleGameCountText => IsLoadingLibrary
        ? string.Empty
        : $"({GameCount})";

    public string HeaderStatusText => IsPublishingView ? LanguageAppearance.Get("Linux_UpdatingView", "Updating game view…") : IsLoadingLibrary || IsBusy
        ? StatusText
        : string.Empty;

    public string AlertText
    {
        get => _alertText;
        set
        {
            if (SetProperty(ref _alertText, value))
            {
                OnPropertyChanged(nameof(HasAlert));
            }
        }
    }

    public bool HasAlert => !string.IsNullOrWhiteSpace(AlertText);

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

    public int GridCardSize
    {
        get => _gridCardSize;
        set => SetProperty(ref _gridCardSize, value);
    }

    public double GridItemWidth
    {
        get => _gridItemWidth;
        set => SetProperty(ref _gridItemWidth, value);
    }

    public double GridItemHeight
    {
        get => _gridItemHeight;
        set => SetProperty(ref _gridItemHeight, value);
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
                OnPropertyChanged(nameof(CanActOnSelection));
            }
        }
    }

    public string SelectionHeading => LanguageAppearance.Format("GamesPage_SelectionMode_CountTemplate", "{0} selected", SelectedCount);

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(GamesHeading));
        OnPropertyChanged(nameof(SelectionHeading));
        OnPropertyChanged(nameof(HeaderStatusText));
    }

    private bool _isPublishingView;
    public bool IsPublishingView
    {
        get => _isPublishingView;
        set
        {
            if (SetProperty(ref _isPublishingView, value))
            {
                OnPropertyChanged(nameof(CanInteract));
                OnPropertyChanged(nameof(CanActOnSelection));
                OnPropertyChanged(nameof(HeaderStatusText));
            }
        }
    }
    public bool CanInteract => !IsBusy && !IsPublishingView;

    public bool CanActOnSelection => CanInteract && SelectedCount > 0;

}
