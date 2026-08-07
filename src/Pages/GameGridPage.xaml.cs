using DLSS_Swapper.Data;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using AsyncAwaitBestPractices;
using CommunityToolkit.WinUI;
using DLSS_Swapper.Helpers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DLSS_Swapper.Pages;


/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GameGridPage : Page
{
    public static string PageTag { get; } = "PageTag_Games";

    /*
    public List<IGameLibrary> GameLibraries { get; } = new List<IGameLibrary>();

    Dictionary<GameLibrary, ObservableCollection<Game>> allGames = new Dictionary<GameLibrary, ObservableCollection<Game>>();


    public List<GameGroup> GroupedGameGroups { get; } = new List<GameGroup>();
    public List<GameGroup> UngroupedGameGroups { get; } = new List<GameGroup>();

    ObservableCollection<Game> FavouriteGames = new ObservableCollection<Game>();
    ObservableCollection<Game> AllGames = new ObservableCollection<Game>();
    */

    bool _loadingGamesAndDlls;
    GridView? _responsiveGridView;
    ScrollViewer? _responsiveGridScrollViewer;
    XamlRoot? _responsiveGridXamlRoot;
    double _lastResponsiveViewportWidth = double.NaN;
    double _lastResponsiveViewportHeight = double.NaN;
    double _lastResponsiveHorizontalPadding = double.NaN;
    int _lastResponsivePreferredColumns = -1;
    int _lastResponsivePreferredRows = -1;
    double _lastResponsiveRasterizationScale = double.NaN;
    TaskCompletionSource? _visibleCoverOpened;
    bool _isHeaderFilterFlyoutOpen;

    public GameGridPageModel ViewModel { get; private set; }

    public GameGridPage()
    {
        this.InitializeComponent();
        ViewModel = new GameGridPageModel(this);
        DataContext = ViewModel;
        Unloaded += Page_Unloaded;
    }

    internal void PrepareVisibleCoverWait()
    {
        _visibleCoverOpened = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal async Task WaitForVisibleCoverAsync()
    {
        var visibleCoverOpened = _visibleCoverOpened;
        if (visibleCoverOpened is null)
        {
            return;
        }

        await Task.WhenAny(visibleCoverOpened.Task, Task.Delay(750));
        if (ReferenceEquals(_visibleCoverOpened, visibleCoverOpened))
        {
            _visibleCoverOpened = null;
        }
    }

    void CoverImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        _visibleCoverOpened?.TrySetResult();
    }

    bool hasFirstLoaded;
    void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (hasFirstLoaded)
        {
            return;
        }
        hasFirstLoaded = true;

        if (DataContext is GameGridPageModel gameGridPageModel)
        {
            gameGridPageModel.InitialLoadAsync().SafeFireAndForget((err) =>
            {
                Logger.Error(err, $"Unable to perform initial load");
            });
        }

        //await LoadGamesAndDlls();
        //await LoadGamesFromCacheAsync();
        //UpdateGameLibraries();
        //await LoadGames();
    }


    async Task LoadGamesAndDlls()
    {
        // TODO: REMOVE
        await Task.Delay(1);

        if (_loadingGamesAndDlls)
            return;

        _loadingGamesAndDlls = true;

        // TODO: Fade?
        //LoadingStackPanel.Visibility = Visibility.Visible;

        /*
        var tasks = new List<Task>();
        tasks.Add(LoadGamesAsync());


        await Task.WhenAll(tasks);

        */
        App.CurrentApp.RunOnUIThread(() =>
        {
            //LoadingStackPanel.Visibility = Visibility.Collapsed;
            _loadingGamesAndDlls = false;
        });
    }

    internal void ScrollToGame(Game game)
    {
        if (MainContentControl.ContentTemplateRoot is GridView mainGridView)
        {
            App.CurrentApp.RunOnUIThreadAsync(async () =>
            {
                var indexOfGame = mainGridView.Items.IndexOf(game);
                if (indexOfGame >= 0)
                {
                    await mainGridView.SmoothScrollIntoViewWithItemAsync(indexOfGame);
                }
            }).SafeFireAndForget();
        }
        else if (MainContentControl.ContentTemplateRoot is ListView mainListView)
        {
            App.CurrentApp.RunOnUIThreadAsync(async () =>
            {
                var indexOfGame = mainListView.Items.IndexOf(game);
                if (indexOfGame >= 0)
                {
                    await mainListView.SmoothScrollIntoViewWithItemAsync(indexOfGame);
                }
            }).SafeFireAndForget();
        }
    }

    internal void ReloadMainContentControl()
    {
        MainContentControl.Content = null;
        MainContentControl.Content = ViewModel;
    }

    // This fires for both the GridView and the ListView
    void GridAndListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Game selectedGame)
        {
            if (selectedGame.Processing)
            {
                var dialog = new EasyContentDialog(XamlRoot)
                {
                    Title = ResourceHelper.GetString("Game_CurrentlyProcessing"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamePage_ProcessingPleaseWaitTemplate", selectedGame.Title),
                };
                _ = dialog.ShowAsync();
                return;
            }

            var gameControl = new GameControl(selectedGame);
            _ = gameControl.ShowAsync();
        }
    }


    static readonly SolidColorBrush _cardHoverRestBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    // The card grid's parent is the hover ring Border surrounding the cover; its
    // 2px thickness is always reserved, only the brush changes on hover.
    void GameCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid card && card.Parent is Border hoverBorder)
        {
            hoverBorder.BorderBrush = (Brush)Resources["CardHoverBorderBrush"];
        }
    }

    void GameCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid card && card.Parent is Border hoverBorder)
        {
            hoverBorder.BorderBrush = _cardHoverRestBrush;
        }
    }

    void MainGridView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not GridView gridView)
        {
            return;
        }

        AttachResponsiveGridLayout(gridView);
        gridView.DispatcherQueue.TryEnqueue(() =>
        {
            if (gridView.IsLoaded)
            {
                AttachResponsiveGridLayout(gridView);
            }
        });
    }

    void MainGridView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is GridView gridView)
        {
            // Use the new control size immediately. The ScrollViewer reports its
            // exact viewport in a subsequent layout callback (including any
            // vertical scrollbar), which performs the final pixel-level update.
            UpdateResponsiveGridLayout(gridView, e.NewSize.Width, e.NewSize.Height);
        }
    }

    void AttachResponsiveGridLayout(GridView gridView)
    {
        if (ReferenceEquals(_responsiveGridView, gridView) == false)
        {
            DetachResponsiveGridLayout();
            _responsiveGridView = gridView;
            gridView.Unloaded += MainGridView_Unloaded;
        }

        var scrollViewer = gridView.FindDescendant<ScrollViewer>();
        if (scrollViewer is not null
            && ReferenceEquals(_responsiveGridScrollViewer, scrollViewer) == false)
        {
            if (_responsiveGridScrollViewer is not null)
            {
                _responsiveGridScrollViewer.SizeChanged -= ResponsiveGridScrollViewer_SizeChanged;
            }

            _responsiveGridScrollViewer = scrollViewer;
            scrollViewer.SizeChanged += ResponsiveGridScrollViewer_SizeChanged;
        }

        var xamlRoot = gridView.XamlRoot;
        if (xamlRoot is not null
            && ReferenceEquals(_responsiveGridXamlRoot, xamlRoot) == false)
        {
            if (_responsiveGridXamlRoot is not null)
            {
                _responsiveGridXamlRoot.Changed -= ResponsiveGridXamlRoot_Changed;
            }

            _responsiveGridXamlRoot = xamlRoot;
            xamlRoot.Changed += ResponsiveGridXamlRoot_Changed;
        }

        UpdateResponsiveGridLayout(gridView);
    }

    void MainGridView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _responsiveGridView))
        {
            DetachResponsiveGridLayout();
        }
    }

    void ResponsiveGridScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_responsiveGridView is not null)
        {
            UpdateResponsiveGridLayout(_responsiveGridView);
        }
    }

    void ResponsiveGridXamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (_responsiveGridView is not null)
        {
            UpdateResponsiveGridLayout(_responsiveGridView);
        }
    }

    void DetachResponsiveGridLayout()
    {
        if (_responsiveGridScrollViewer is not null)
        {
            _responsiveGridScrollViewer.SizeChanged -= ResponsiveGridScrollViewer_SizeChanged;
            _responsiveGridScrollViewer = null;
        }
        if (_responsiveGridView is not null)
        {
            _responsiveGridView.Unloaded -= MainGridView_Unloaded;
            _responsiveGridView = null;
        }
        if (_responsiveGridXamlRoot is not null)
        {
            _responsiveGridXamlRoot.Changed -= ResponsiveGridXamlRoot_Changed;
            _responsiveGridXamlRoot = null;
        }

        _lastResponsiveViewportWidth = double.NaN;
        _lastResponsiveViewportHeight = double.NaN;
        _lastResponsiveHorizontalPadding = double.NaN;
        _lastResponsivePreferredColumns = -1;
        _lastResponsivePreferredRows = -1;
        _lastResponsiveRasterizationScale = double.NaN;
    }

    internal void RefreshResponsiveGridLayout()
    {
        if (_responsiveGridView is not null)
        {
            UpdateResponsiveGridLayout(_responsiveGridView);
        }
    }

    void UpdateResponsiveGridLayout(
        GridView gridView,
        double immediateViewportWidth = 0,
        double immediateViewportHeight = 0)
    {
        var scrollViewer = gridView.FindDescendant<ScrollViewer>();
        var viewportWidth = immediateViewportWidth > 0
            ? immediateViewportWidth
            : scrollViewer is not null && scrollViewer.ViewportWidth > 0
                ? scrollViewer.ViewportWidth
                : gridView.ActualWidth;
        var viewportHeight = immediateViewportHeight > 0
            ? immediateViewportHeight
            : scrollViewer is not null && scrollViewer.ViewportHeight > 0
                ? scrollViewer.ViewportHeight
                : gridView.ActualHeight;
        var horizontalPadding = gridView.Padding.Left + gridView.Padding.Right;
        var preferredColumns = Settings.Instance.GridViewPreferredColumns;
        var preferredRows = Settings.Instance.GridViewPreferredRows;
        var rasterizationScale = gridView.XamlRoot?.RasterizationScale ?? 1d;
        var itemsWrapGrid = gridView.ItemsPanelRoot as ItemsWrapGrid;

        if (itemsWrapGrid is not null
            && double.IsNaN(itemsWrapGrid.ItemWidth) == false
            && Math.Abs(viewportWidth - _lastResponsiveViewportWidth) < 0.25
            && Math.Abs(viewportHeight - _lastResponsiveViewportHeight) < 0.25
            && Math.Abs(horizontalPadding - _lastResponsiveHorizontalPadding) < 0.01
            && preferredColumns == _lastResponsivePreferredColumns
            && preferredRows == _lastResponsivePreferredRows
            && Math.Abs(rasterizationScale - _lastResponsiveRasterizationScale) < 0.001)
        {
            return;
        }

        _lastResponsiveViewportWidth = viewportWidth;
        _lastResponsiveViewportHeight = viewportHeight;
        _lastResponsiveHorizontalPadding = horizontalPadding;
        _lastResponsivePreferredColumns = preferredColumns;
        _lastResponsivePreferredRows = preferredRows;
        _lastResponsiveRasterizationScale = rasterizationScale;

        var metrics = ResponsiveGameGridLayout.Calculate(
            viewportWidth,
            viewportHeight,
            horizontalPadding,
            preferredColumns,
            preferredRows,
            rasterizationScale);

        // The GridView's own ItemsWrapGrid performs the grouped layout; modern
        // virtualizing panels ignore GroupStyle.Panel, so the explicit cell size
        // must be applied here rather than through a panel template binding.
        if (itemsWrapGrid is not null)
        {
            itemsWrapGrid.ItemWidth = metrics.CellWidth;
            itemsWrapGrid.ItemHeight = metrics.CellHeight;
        }

        ViewModel.GridViewCardWidth = metrics.CardWidth;
        ViewModel.GridViewCardHeight = metrics.CardHeight;
    }

    void MainGridView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;

            if (sender is GridView gridView)
            {
                // Wheel up prefers fewer, larger cards; wheel down prefers more,
                // smaller cards. The setting clamps and persists itself.
                Settings.Instance.GridViewPreferredColumns += delta > 0 ? -1 : 1;
                UpdateResponsiveGridLayout(gridView);
            }

            e.Handled = true;
        }
    }

    private void ClearSearchBox_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }

    bool _isSyncingSelection;

    ListViewBase? GetActiveListControl()
    {
        return MainContentControl.ContentTemplateRoot as ListViewBase;
    }

    void ApplyListSelectionLayout(ListViewBase listControl, bool isSelecting)
    {
        if (listControl is ListView listView)
        {
            listView.ItemContainerStyle = (Style)Resources[
                isSelecting ? "SelectingListViewItemStyle" : "CompactListViewItemStyle"];
        }
    }

    void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachResponsiveGridLayout();
    }

    internal void EnterSelectionMode()
    {
        var listControl = GetActiveListControl();
        if (listControl is null)
        {
            return;
        }

        ApplyListSelectionLayout(listControl, true);
        listControl.SelectionMode = ListViewSelectionMode.Multiple;
        listControl.IsItemClickEnabled = false;
        listControl.SelectionChanged += ListControl_SelectionChanged;
    }

    internal void ExitSelectionMode()
    {
        var listControl = GetActiveListControl();
        if (listControl is null)
        {
            return;
        }

        listControl.SelectionChanged -= ListControl_SelectionChanged;
        _isSyncingSelection = true;
        try
        {
            listControl.SelectedItems.Clear();
        }
        finally
        {
            _isSyncingSelection = false;
        }

        listControl.SelectionMode = ListViewSelectionMode.None;
        ApplyListSelectionLayout(listControl, false);
        listControl.IsItemClickEnabled = true;
    }

    void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        var flyout = (Flyout)Resources["HeaderFilterFlyout"];
        if (_isHeaderFilterFlyoutOpen)
        {
            flyout.Hide();
            return;
        }

        flyout.ShowAt(FilterButton);
    }

    void FilterFlyout_Opening(object sender, object e)
    {
        _isHeaderFilterFlyoutOpen = true;
    }

    void FilterFlyout_Closed(object sender, object e)
    {
        _isHeaderFilterFlyoutOpen = false;
        if (sender is Flyout { Content: GameFilterControl filterControl }
            && filterControl.DataContext is GameFilterControlViewModel filterViewModel)
        {
            ViewModel.ApplyGameFilter(filterViewModel);
        }
    }

    internal int GetVisibleItemCount()
    {
        return GetActiveListControl()?.Items.Count ?? 0;
    }

    internal int GetVisibleSelectedCount()
    {
        var listControl = GetActiveListControl();
        if (listControl is null)
        {
            return 0;
        }

        var selectedCount = 0;
        foreach (var item in listControl.Items)
        {
            if (item is Game game
                && ViewModel.SelectedGames.Any(selectedGame =>
                    ReferenceEquals(selectedGame, game)))
            {
                selectedCount++;
            }
        }

        return selectedCount;
    }

    internal void SelectAllVisible()
    {
        GetActiveListControl()?.SelectAll();
    }

    internal void DeselectAllVisible()
    {
        GetActiveListControl()?.SelectedItems.Clear();
    }

    internal void BeginSuppressSelectionEvents()
    {
        _isSyncingSelection = true;
    }

    void ListControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        ViewModel.UpdateSelection(e.AddedItems, e.RemovedItems);
    }
}
