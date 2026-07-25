using DLSS_Swapper.Data;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using AsyncAwaitBestPractices;
using CommunityToolkit.WinUI;
using DLSS_Swapper.Helpers;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

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
    DispatcherQueueTimer? _saveScrollSizeTimer;
    GridView? _responsiveGridView;
    ScrollViewer? _responsiveGridScrollViewer;
    XamlRoot? _responsiveGridXamlRoot;
    double _lastResponsiveViewportWidth = double.NaN;
    double _lastResponsiveHorizontalPadding = double.NaN;
    double _lastResponsiveCardWidth = double.NaN;
    double _lastResponsiveRasterizationScale = double.NaN;

    public GameGridPageModel ViewModel { get; private set; }

    public GameGridPage()
    {
        this.InitializeComponent();
        ViewModel = new GameGridPageModel(this);
        DataContext = ViewModel;
        Unloaded += Page_Unloaded;
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
            // Use the new control width immediately. The ScrollViewer reports its
            // exact viewport in a subsequent layout callback (including any
            // vertical scrollbar), which performs the final pixel-level update.
            UpdateResponsiveGridLayout(gridView, e.NewSize.Width);
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
        _lastResponsiveHorizontalPadding = double.NaN;
        _lastResponsiveCardWidth = double.NaN;
        _lastResponsiveRasterizationScale = double.NaN;
    }

    void UpdateResponsiveGridLayout(GridView gridView, double immediateViewportWidth = 0)
    {
        var scrollViewer = gridView.FindDescendant<ScrollViewer>();
        var viewportWidth = immediateViewportWidth > 0
            ? immediateViewportWidth
            : scrollViewer is not null && scrollViewer.ViewportWidth > 0
                ? scrollViewer.ViewportWidth
                : gridView.ActualWidth;
        var horizontalPadding = gridView.Padding.Left + gridView.Padding.Right;
        var cardWidth = ViewModel.GridViewItemWidth;
        var rasterizationScale = gridView.XamlRoot?.RasterizationScale ?? 1d;

        if (Math.Abs(viewportWidth - _lastResponsiveViewportWidth) < 0.25
            && Math.Abs(horizontalPadding - _lastResponsiveHorizontalPadding) < 0.01
            && Math.Abs(cardWidth - _lastResponsiveCardWidth) < 0.01
            && Math.Abs(rasterizationScale - _lastResponsiveRasterizationScale) < 0.001)
        {
            return;
        }

        _lastResponsiveViewportWidth = viewportWidth;
        _lastResponsiveHorizontalPadding = horizontalPadding;
        _lastResponsiveCardWidth = cardWidth;
        _lastResponsiveRasterizationScale = rasterizationScale;

        var metrics = ResponsiveGameGridLayout.Calculate(
            viewportWidth,
            horizontalPadding,
            cardWidth,
            rasterizationScale);
        ViewModel.GridViewCellWidth = metrics.CellWidth;
    }

    void MainGridView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;

            if (sender is GridView gridView)
            {
                double scaleAmount = delta > 0 ? 1.05 : 0.95;
                var newWidth = (int)(ViewModel.GridViewItemWidth * scaleAmount);

                if (newWidth >= Settings.MinGridViewItemWidth
                    && newWidth <= Settings.MaxGridViewItemWidth)
                {
                    ViewModel.GridViewItemWidth = newWidth;
                    UpdateResponsiveGridLayout(gridView);

                    if (_saveScrollSizeTimer is null)
                    {
                        _saveScrollSizeTimer = DispatcherQueue.CreateTimer();
                        _saveScrollSizeTimer.Interval = TimeSpan.FromMilliseconds(500);
                        _saveScrollSizeTimer.IsRepeating = false;
                        _saveScrollSizeTimer.Tick += SaveScrollSizeTimer_Tick;
                    }

                    _saveScrollSizeTimer.Stop();
                    _saveScrollSizeTimer.Start();
                }
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

    void SaveScrollSizeTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        Settings.Instance.GridViewItemWidth = ViewModel.GridViewItemWidth;
    }

    void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachResponsiveGridLayout();

        if (_saveScrollSizeTimer is null)
        {
            return;
        }

        _saveScrollSizeTimer.Stop();
        _saveScrollSizeTimer.Tick -= SaveScrollSizeTimer_Tick;
        _saveScrollSizeTimer = null;
        Settings.Instance.GridViewItemWidth = ViewModel.GridViewItemWidth;
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
