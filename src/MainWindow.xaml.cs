using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Messages;
using DLSS_Swapper.Pages;
using DLSS_Swapper.UserControls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;

namespace DLSS_Swapper;

public sealed partial class MainWindow : Window
{
    const double DefaultWindowWidthDip = 734;
    const double DefaultWindowHeightDip = 1000;
    const double DefaultWindowMarginDip = 16;
    const int MinimumRestoredDimension = 512;
    const int MaximumRestoredDimension = 32_768;
    const int MaximumCoordinateMagnitude = 1_000_000;

    public MainWindowModel ViewModel { get; private set; }

    IntPtr _windowIcon;

    readonly WindowPositionRect _trackedWindow = new WindowPositionRect();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string iconPath, ref IntPtr index);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    static extern int DestroyIcon(IntPtr hIcon);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hWnd);

    public MainWindow()
    {
        this.InitializeComponent();
        ViewModel = new MainWindowModel();

        if (AppWindow?.Presenter is OverlappedPresenter overlappedPresenter)
        {
            var lastWindowSizeAndPosition = Settings.Instance.LastWindowSizeAndPosition;
            _trackedWindow = new WindowPositionRect(lastWindowSizeAndPosition);

            var safeRestoreRect = default(RectInt32);
            var shouldRestoreWindow =
                IsSafeForRestore(_trackedWindow) &&
                TryGetSafeRestoreRect(
                    _trackedWindow.GetRectInt32(),
                    out safeRestoreRect);

            if (shouldRestoreWindow)
            {
                var restoredState = _trackedWindow.State;
                AppWindow.MoveAndResize(safeRestoreRect);
                _trackedWindow = new WindowPositionRect(
                    safeRestoreRect.X,
                    safeRestoreRect.Y,
                    safeRestoreRect.Width,
                    safeRestoreRect.Height)
                {
                    State = restoredState,
                };
            }
            else
            {
                var defaultWindowRect = GetDefaultWindowRect();
                AppWindow.MoveAndResize(defaultWindowRect);
                _trackedWindow = new WindowPositionRect(
                    defaultWindowRect.X,
                    defaultWindowRect.Y,
                    defaultWindowRect.Width,
                    defaultWindowRect.Height);
            }

            if (shouldRestoreWindow &&
                _trackedWindow.State == OverlappedPresenterState.Maximized)
            {
                overlappedPresenter.Maximize();
            }
        }

        AppWindow?.Changed += (AppWindow sender, AppWindowChangedEventArgs args) =>
        {
            if (args.DidPositionChange)
            {
                if (sender.Presenter is OverlappedPresenter presenter)
                {
                    var isCurrentlyMinimizedOrMaximized =
                        presenter.State == OverlappedPresenterState.Minimized ||
                        presenter.State == OverlappedPresenterState.Maximized;

                    if (isCurrentlyMinimizedOrMaximized == false)
                    {
                        _trackedWindow.UpdatePosition(sender.Position);
                    }
                }
            }
        };

        SizeChanged += (object sender, WindowSizeChangedEventArgs args) =>
        {
            if (AppWindow?.Presenter is OverlappedPresenter overlappedPresenter)
            {
                var currentState = overlappedPresenter.State;
                var isTransitioningToMaximized =
                    currentState == OverlappedPresenterState.Maximized &&
                    _trackedWindow.State != OverlappedPresenterState.Maximized;

                if (isTransitioningToMaximized == false && currentState != OverlappedPresenterState.Maximized)
                {
                    _trackedWindow.UpdateFromAppWindow(AppWindow);
                }

                _trackedWindow.State = overlappedPresenter.State;
            }
        };

        Closed += (object sender, WindowEventArgs args) =>
        {
            if (AppWindow?.Presenter is OverlappedPresenter overlappedPresenter)
            {
                Settings.Instance.LastWindowSizeAndPosition = new WindowPositionRect(_trackedWindow);
            }

            if (_windowIcon != IntPtr.Zero)
            {
                _ = DestroyIcon(_windowIcon);
                _windowIcon = IntPtr.Zero;
            }
        };

        if (WindowManager.IsCustomizationSupported)
        {
            var appWindow = App.CurrentApp.WindowManager.GetAppWindowForWindow(this);
            var appWindowTitleBar = appWindow.TitleBar;
            appWindowTitleBar.ExtendsContentIntoTitleBar = true;
            RootGrid.RowDefinitions[0].Height = new GridLength(32);
        }
        else
        {
            RootGrid.RowDefinitions[0].Height = new GridLength(28);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }

        SetIcon();

        UpdateSettingsTabLabel();

        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(
            this,
            static (recipient, _) => ((MainWindow)recipient).UpdateSettingsTabLabel());
    }

    RectInt32 GetDefaultWindowRect()
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(windowHandle);
        var scale = dpi > 0 ? dpi / 96d : 1d;

        var desiredWidth = (int)Math.Ceiling(DefaultWindowWidthDip * scale);
        var desiredHeight = (int)Math.Ceiling(DefaultWindowHeightDip * scale);
        var margin = Math.Max(1, (int)Math.Ceiling(DefaultWindowMarginDip * scale));

        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var width = Math.Min(desiredWidth, Math.Max(1, workArea.Width - (margin * 2)));
        var height = Math.Min(desiredHeight, Math.Max(1, workArea.Height - (margin * 2)));

        return new RectInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height);
    }

    static bool IsSafeForRestore(WindowPositionRect window)
    {
        return window.Width is >= MinimumRestoredDimension and <= MaximumRestoredDimension
            && window.Height is >= MinimumRestoredDimension and <= MaximumRestoredDimension
            && window.X is >= -MaximumCoordinateMagnitude and <= MaximumCoordinateMagnitude
            && window.Y is >= -MaximumCoordinateMagnitude and <= MaximumCoordinateMagnitude
            && Enum.IsDefined(typeof(OverlappedPresenterState), window.State);
    }

    static bool TryGetSafeRestoreRect(
        RectInt32 windowRect,
        out RectInt32 safeRestoreRect)
    {
        safeRestoreRect = default;
        var displayArea = DisplayArea.GetFromRect(windowRect, DisplayAreaFallback.None);
        if (displayArea is null)
        {
            return false;
        }

        var workArea = displayArea.WorkArea;
        var left = Math.Max(windowRect.X, workArea.X);
        var top = Math.Max(windowRect.Y, workArea.Y);
        var right = Math.Min(windowRect.X + windowRect.Width, workArea.X + workArea.Width);
        var bottom = Math.Min(windowRect.Y + windowRect.Height, workArea.Y + workArea.Height);

        if (right - left < 64 || bottom - top < 64)
        {
            return false;
        }

        var width = Math.Min(windowRect.Width, workArea.Width);
        var height = Math.Min(windowRect.Height, workArea.Height);
        var x = Math.Clamp(
            windowRect.X,
            workArea.X,
            workArea.X + workArea.Width - width);
        var y = Math.Clamp(
            windowRect.Y,
            workArea.Y,
            workArea.Y + workArea.Height - height);
        safeRestoreRect = new RectInt32(x, y, width, height);
        return true;
    }

    void UpdateSettingsTabLabel()
    {
        var settingsText = ResourceHelper.GetString("SettingsPage_Title");
        AutomationProperties.SetName(SettingsTab, settingsText);
        ToolTipService.SetToolTip(SettingsTab, settingsText);
    }

    /// <summary>
    /// Default the Window Icon to the icon stored in the .exe, if any.
    ///
    /// The Icon can be overriden by callers by calling SetIcon themselves.
    /// </summary>
    /// via this MAUI PR https://github.com/dotnet/maui/pull/6900
    void SetIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var index = IntPtr.Zero; // 0 = first icon in resources
            _windowIcon = ExtractAssociatedIcon(IntPtr.Zero, processPath, ref index);
            if (_windowIcon != IntPtr.Zero)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

                var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(windowHandle));
                if (appWindow is not null)
                {
                    var iconId = Win32Interop.GetIconIdFromIcon(_windowIcon);
                    appWindow.SetIcon(iconId);
                }
            }
        }
    }

    bool _syncingMainTabs;

    void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_syncingMainTabs)
        {
            return;
        }

        if (MainTabs.SelectedItem is ListViewItem { Tag: string page })
        {
            GoToPage(page);
        }
    }

    void SyncMainTab(string page)
    {
        var desiredItem = MainTabs.Items
            .OfType<ListViewItem>()
            .FirstOrDefault(item => item.Tag is string tag && tag == page);

        if (ReferenceEquals(MainTabs.SelectedItem, desiredItem))
        {
            return;
        }

        _syncingMainTabs = true;
        try
        {
            MainTabs.SelectedItem = desiredItem;
        }
        finally
        {
            _syncingMainTabs = false;
        }
    }

    GameGridPage? gameGridPage;
    LibraryPage? libraryPage;
    SettingsPage? settingsPage;

    public GameGridPage? GameGridPage => gameGridPage;

    void GoToPage(string page)
    {
        ViewModel.AcknowledgementsVisibility = Visibility.Collapsed;

        if (page == GameGridPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content as Page != gameGridPage)
            {
                ContentFrame.Content = gameGridPage ??= new GameGridPage();
            }
        }
        else if (page == LibraryPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content as Page != libraryPage)
            {
                ContentFrame.Content = libraryPage ??= new LibraryPage();
            }
        }
        else if (page == SettingsPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content as Page != settingsPage)
            {
                ContentFrame.Content = settingsPage ??= new SettingsPage();
            }
        }
        else if (page ==  AcknowledgementsPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content is not AcknowledgementsPage)
            {
                ViewModel.AcknowledgementsVisibility = Visibility.Visible;
                ContentFrame.Content = new AcknowledgementsPage();
            }
        }
        else
        {
            Logger.Error($"Attempting to navigate to a page that was not found, {page}");
            return;
        }

        SyncMainTab(page);
    }

    internal void GoToAcknowledgements()
    {
        GoToPage(AcknowledgementsPage.PageTag);
    }

    async void MainContentHost_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Info("Main window content loaded.");
        await DLLManager.Instance.LoadManifestsAsync();

        if (Settings.Instance.HasSelectedSystemPerformance == false)
        {
            Logger.Info("Showing first-run game library storage selector.");
            var hardDriveOption = new CheckBox
            {
                Content = ResourceHelper.GetString("MainWindow_SystemPerformance_HardDrive"),
            };

            var content = new StackPanel()
            {
                Spacing = 12,
            };
            content.Children.Add(new TextBlock()
            {
                Text = ResourceHelper.GetString("MainWindow_SystemPerformance_Message"),
                TextWrapping = TextWrapping.Wrap,
            });
            content.Children.Add(hardDriveOption);

            var dialog = new EasyContentDialog(RootGrid.XamlRoot)
            {
                Title = ResourceHelper.GetString("MainWindow_SystemPerformance_Title"),
                PrimaryButtonText = ResourceHelper.GetString("General_Apply"),
                DefaultButton = ContentDialogButton.Primary,
                Content = content,
            };
            await dialog.ShowAsync();

            var profile = hardDriveOption.IsChecked == true
                ? GameLibraryStorageProfile.HardDrive
                : GameLibraryStorageProfile.Standard;
            Settings.Instance.ApplyGameLibraryStorageProfile(profile);
            Logger.Info($"Applied first-run game library storage profile: {profile}.");
        }

        if (DLLManager.Instance.HasLoadedManifest() == false)
        {
            var dialog = new EasyContentDialog(RootGrid.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                PrimaryButtonText = ResourceHelper.GetString("MainWindow_ManifestCouldNotBeLoaded_UpdateManifest"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetString("MainWindow_ManifestCouldNotBeLoaded_LleMessage"),
            };
            var shouldClose = true;

            var response = await dialog.ShowAsync();
            if (response == ContentDialogResult.Primary)
            {
                dialog = new EasyContentDialog(RootGrid.XamlRoot)
                {
                    Title = ResourceHelper.GetString("MainWindow_AttemptingManifestUpdate"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = new ProgressRing()
                    {
                        IsActive = true,
                        IsIndeterminate = true,
                    },
                };

                var updateTask = DLLManager.Instance.UpdateManifestAsync();
                _ = dialog.ShowAsync();
                await updateTask;
                dialog.Hide();

                if (DLLManager.Instance.HasLoadedManifest() == true)
                {
                    shouldClose = false;
                }
            }

            if (shouldClose)
            {
                dialog = new EasyContentDialog(RootGrid.XamlRoot)
                {
                    Title = ResourceHelper.GetString("MainWindow_DlssSwapperMustClose"),
                    CloseButtonText = ResourceHelper.GetString("General_Close"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetString("MainWindow_DlssSwapperCloseDueToManifest"),
                };
                await dialog.ShowAsync();

                Close();
            }
        }

        if (DLLManager.Instance.ImportedManifest is null)
        {
            var dialog = new EasyContentDialog(RootGrid.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_CouldNotLoadImportedDlls"),
                DefaultButton = ContentDialogButton.Close,
                Content = new ImportSystemDisabledView(),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
            };
            await dialog.ShowAsync();
        }

        _ = DLLManager.Instance.UpdateManifestAsync();

        LoadingStackPanel.Visibility = Visibility.Collapsed;

        GoToPage(GameGridPage.PageTag);

    }

}
