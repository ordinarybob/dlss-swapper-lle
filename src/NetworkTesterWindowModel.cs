using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Messages;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace DLSS_Swapper;

public sealed partial class NetworkTesterWindowModel : ObservableObject, IDisposable
{
    readonly WeakReference<NetworkTesterWindow> _weakWindow;
    readonly string _dlssSwapperDomainTestLink = "dlss-swapper-downloads.beeradmoore.com";
    readonly string _dlssSwapperDownloadTestLink = "https://dlss-swapper-downloads.beeradmoore.com/dlss/nvngx_dlss_v1.0.0.0.zip";
    readonly string _uploadThingDownloadTestLink = "https://hb4kzlkh4u.ufs.sh/f/isdnLt22yljeRWLOje0oeKXyth5OC7M6sI02T3YfL8GPbvpd";
    readonly string _dlssSwapperCoverImageTestLink = "https://dlss-swapper-downloads.beeradmoore.com/test/library_600x900_2x.jpg";
    readonly string _dlssSwapperAlternativeCoverImageTestLink = "https://files.beeradmoore.com/dlss-swapper/test/library_600x900_2x.jpg";
    readonly string _steamCoverImageTestLink = "https://steamcdn-a.akamaihd.net/steam/apps/870780/library_600x900_2x.jpg";
    readonly string _egsCoverImageTestLink = "https://cdn1.epicgames.com/item/calluna/Control_Portrait_Storefront_1200X1600_1200x1600-456c920cae7a0aa9b36670cd5e1237a1?w=600&h=900&resize=1";

    [ObservableProperty]
    public partial FlowDirection AppFlowDirection { get; set; } = FlowDirection.LeftToRight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest1 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest2 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest3 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest4 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest5 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest6 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest7 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest8 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest9 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest10 { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningTest))]
    [NotifyPropertyChangedFor(nameof(IsNotRunningTest))]
    public partial bool RunningTest11 { get; set; } = false;

    public bool IsRunningTest => RunningTest1 || RunningTest2 || RunningTest3 || RunningTest4 || RunningTest5 || RunningTest6 || RunningTest7 || RunningTest8 || RunningTest9 || RunningTest10 || RunningTest11;

    public bool IsNotRunningTest => IsRunningTest == false;

    [ObservableProperty]
    public partial string TestResults { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test1Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test2Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test3Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test4Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test5Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test6Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test7Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test8Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test9Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test10Result { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Test11Result { get; set; } = string.Empty;

    // Each running test disposes its own source in CompleteTest. This field is only
    // the synchronized reference used to cancel the currently active test.
#pragma warning disable CA2213
    CancellationTokenSource? _cancellationTokenSource;
#pragma warning restore CA2213
    readonly object _testCancellationLock = new();
    bool _disposed;

    public NetworkTesterWindowModel(NetworkTesterWindow window) : base()
    {
        _weakWindow = new WeakReference<NetworkTesterWindow>(window);
        
        UpdateFlowDirection();
        
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(
            this,
            static (recipient, _) => ((NetworkTesterWindowModel)recipient).UpdateFlowDirection());

        AppendTestResults("Init", $"DLSS Swapper LLE version: v{App.CurrentApp.GetVersionString()}");
    }
    
    private void UpdateFlowDirection()
    {
        var currentLanguage = Settings.Instance.Language;
        AppFlowDirection = LanguageManager.IsRightToLeftLanguage(currentLanguage)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    public NetworkTesterWindowModelTranslationProperties TranslationProperties { get; } = new NetworkTesterWindowModelTranslationProperties();

    void AppendTestResults(string testName, string message)
    {
        App.CurrentApp.RunOnUIThread(() =>
        {
            TestResults += $"{DateTime.Now.ToString("O")} {testName}: {message}\n";
        });
    }

    [RelayCommand]
    async Task RunTest1Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 1";
        RunningTest1 = true;
        Test1Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Accessing google.com");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader("https://google.com", 1000)
                {
                    LogPrefix = $"{testName}: ",
                };
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test1Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest1 = false;
        }
        catch (Exception err)
        {
            Test1Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest1 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest1 = false;
        }
    }

    [RelayCommand]
    async Task RunTest2Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 2";
        RunningTest2 = true;
        Test2Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Accessing bing.com");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader("https://bing.com", 1000)
                {
                    LogPrefix = $"{testName}: ",
                };
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test2Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest2 = false;
        }
        catch (Exception err)
        {
            Test2Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest2 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest2 = false;
        }
    }

    [RelayCommand]
    async Task RunTest3Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 3";
        RunningTest3 = true;
        Test3Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Downloading from DLSS Swapper DLL file server ({_dlssSwapperDownloadTestLink})");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(_dlssSwapperDownloadTestLink, 1000)
                {
                    LogPrefix = $"{testName}: ",
                };
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test3Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest3 = false;
        }
        catch (Exception err)
        {
            Test3Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest3 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest3 = false;
        }
    }

    [RelayCommand]
    async Task RunTest4Async()
    {
        var testName = "Test 4";
        Test4Result = string.Empty;
        AppendTestResults(testName, $"Downloading from DLSS Swapper DLL file server in a web browser ({_dlssSwapperDownloadTestLink})");

        if (_weakWindow.TryGetTarget(out NetworkTesterWindow? networkTesterWindow) == true)
        {
            var stackPanel = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                Spacing = 12,
            };
            stackPanel.Children.Add(new TextBlock()
            {
                Text = ResourceHelper.GetString("NetworkTesterPage_OpenInBrowserInfo"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            });
            var horizontalGrid = new Grid()
            {
                ColumnSpacing = 12,
            };
            horizontalGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            horizontalGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            horizontalGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            var linkTextBox = new TextBox()
            {
                Text = _dlssSwapperDownloadTestLink,
                IsReadOnly = true,
            };
            Grid.SetColumn(linkTextBox, 0);
            horizontalGrid.Children.Add(linkTextBox);

            var copyButton = new Button()
            {
                Content = ResourceHelper.GetString("General_Copy"),
            };
            copyButton.Tapped += (sender, e) =>
            {
                var package = new DataPackage();
                package.SetText(_dlssSwapperDownloadTestLink);
                Clipboard.SetContent(package);
            };
            Grid.SetColumn(copyButton, 1);

            var openButton = new Button()
            {
                Content = ResourceHelper.GetString("NetworkTesterPage_OpenInBrowser")
            };
            openButton.Tapped += async (sender, e) =>
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(_dlssSwapperDownloadTestLink));
            };
            Grid.SetColumn(openButton, 2);

            horizontalGrid.Children.Add(copyButton);
            horizontalGrid.Children.Add(openButton);

            stackPanel.Children.Add(horizontalGrid);
            var dialog = new EasyContentDialog(networkTesterWindow.Content.XamlRoot)
            {
                Title = ResourceHelper.GetString("NetworkTesterPage_BrowserTestTitle"),
                PrimaryButtonText = ResourceHelper.GetString("NetworkTesterPage_BrowserTesterWorks"),
                SecondaryButtonText = ResourceHelper.GetString("NetworkTesterPage_BrowserTesterStillDoesNotWork"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                Content = stackPanel,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Test4Result = "✅";
                AppendTestResults(testName, $"User reported it downloaded correctly\n");
            }
            else if (result == ContentDialogResult.Secondary)
            {
                Test4Result = "❌";
                AppendTestResults(testName, $"User reported it failed to download in their web browser\n");
            }
            else
            {
                AppendTestResults(testName, $"User cancelled the test\n");
            }
        }
    }

    [RelayCommand]
    async Task RunTest5Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 5";
        RunningTest5 = true;
        Test5Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Downloading game cover from Steam ({_steamCoverImageTestLink})");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(_steamCoverImageTestLink, 1000)
                {
                    LogPrefix = $"{testName}: ",
                };
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test5Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest5 = false;
        }
        catch (Exception err)
        {
            Test5Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest5 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest5 = false;
        }
    }

    [RelayCommand]
    async Task RunTest6Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 6";
        RunningTest6 = true;
        Test6Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Downloading game cover from Epic Game Store ({_egsCoverImageTestLink})");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(_egsCoverImageTestLink, 1000)
                {
                    LogPrefix = $"{testName}: ",
                };
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test6Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest6 = false;
        }
        catch (Exception err)
        {
            Test6Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest6 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest6 = false;
        }
    }

    [RelayCommand]
    async Task RunTest7Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 7";
        RunningTest7 = true;
        Test7Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Downloading game cover from DLSS Swapper file server ({_dlssSwapperCoverImageTestLink})");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(_dlssSwapperCoverImageTestLink, 1000)
                {
                    LogPrefix = $"{testName}: ",
                };
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test7Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest7 = false;
        }
        catch (Exception err)
        {
            Test7Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest7 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest7 = false;
        }
    }

    [RelayCommand]
    async Task RunTest8Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 8";
        RunningTest8 = true;
        Test8Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Downloading game cover from alternative DLSS Swapper file server ({_dlssSwapperAlternativeCoverImageTestLink})");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(_dlssSwapperAlternativeCoverImageTestLink, 1000)
                {
                    LogPrefix = $"{testName}: ",
                };
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test8Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest8 = false;
        }
        catch (Exception err)
        {
            Test8Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest8 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest8 = false;
        }
    }

    [RelayCommand]
    async Task RunTest10Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 10";
        RunningTest10 = true;
        Test10Result = string.Empty;
        AppendTestResults(testName, $"Downloading from DLSS Swapper DLL file server with custom user agent ({_dlssSwapperDownloadTestLink})");

        if (_weakWindow.TryGetTarget(out NetworkTesterWindow? networkTesterWindow) == true)
        {
            var stackPanel = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                Spacing = 12,
            };
            stackPanel.Children.Add(new TextBlock()
            {
                Text = ResourceHelper.GetString("NetworkTesterPage_UseUserAgentInfo"),
                TextWrapping = TextWrapping.Wrap,
            });

            var userAgentTextBox = new TextBox()
            {
                Text = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36 Edg/133.0.0.0",
            };
            stackPanel.Children.Add(userAgentTextBox);

            var dialog = new EasyContentDialog(networkTesterWindow.Content.XamlRoot)
            {
                Title = ResourceHelper.GetString("NetworkTesterPage_CustomUserAgentTest"),
                PrimaryButtonText = ResourceHelper.GetString("NetworkTesterPage_RunTest"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                Content = stackPanel,
            };
            var testStart = DateTime.Now;
            try
            {
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Reset test start
                    testStart = DateTime.Now;

                    AppendTestResults(testName, $"Testing with User-Agent \"{userAgentTextBox.Text}\"");

                    using (var memoryStream = new MemoryStream())
                    {
                        var fileDownloader = new FileDownloader(_dlssSwapperDownloadTestLink, 1000)
                        {
                            LogPrefix = $"{testName}: ",
                        };
                        await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                        {
                            AppendTestResults(testName, $"StatusCode: {statusCode}");
                        },
                        progressCallback: (downloadedBytes, totalBytes, percent) =>
                        {
                            AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                        }, userAgent: userAgentTextBox.Text);
                        AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
                    }
                    Test10Result = "✅";

                }
                else
                {
                    Test10Result = string.Empty;
                    AppendTestResults(testName, $"Aborted");
                    RunningTest10 = false;
                }
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                AppendTestResults(testName, $"Cancelled");
                RunningTest10 = false;
            }
            catch (Exception err)
            {
                Test10Result = "❌";
                AppendTestResults(testName, $"Failed, {err.Message}");
                if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
                {
                    AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
                }
                RunningTest10 = false;
            }
            finally
            {
                CompleteTest(cancellationTokenSource);
                var duration = (DateTime.Now - testStart).TotalSeconds;
                AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");

                RunningTest10 = false;
            }
        }
        else
        {
            CompleteTest(cancellationTokenSource);
            RunningTest10 = false;
        }
    }

    [RelayCommand]
    async Task RunTest11Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;

        var testName = "Test 11";
        RunningTest11 = true;
        Test11Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"Downloading from UploadThing file server ({_uploadThingDownloadTestLink})");

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(_uploadThingDownloadTestLink, 1000);
                await fileDownloader.DownloadFileToStreamAsync(memoryStream, cancellationToken, statusCodeCallback: (statusCode) =>
                {
                    AppendTestResults(testName, $"StatusCode: {statusCode}");
                },
                progressCallback: (downloadedBytes, totalBytes, percent) =>
                {
                    AppendTestResults(testName, $"{downloadedBytes} / {totalBytes} ({percent:0.0}%)");
                });
                AppendTestResults(testName, $"Downloaded {memoryStream.Length} bytes");
            }
            Test11Result = "✅";
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppendTestResults(testName, $"Cancelled");
            RunningTest11 = false;
        }
        catch (Exception err)
        {
            Logger.Error(err, $"{testName} failed");
            Test11Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest11 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest11 = false;
        }
    }

    [RelayCommand]
    void CopyTestResults()
    {
        var package = new DataPackage();
        package.SetText(TestResults);
        Clipboard.SetContent(package);
    }

    [RelayCommand]
    void CancelCurrentTest()
    {
        lock (_testCancellationLock)
        {
            if (_cancellationTokenSource?.IsCancellationRequested == false)
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }

    CancellationTokenSource StartTest()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        lock (_testCancellationLock)
        {
            if (_cancellationTokenSource?.IsCancellationRequested == false)
            {
                _cancellationTokenSource.Cancel();
            }
            _cancellationTokenSource = cancellationTokenSource;
        }
        return cancellationTokenSource;
    }

    void CompleteTest(CancellationTokenSource cancellationTokenSource)
    {
        lock (_testCancellationLock)
        {
            if (ReferenceEquals(_cancellationTokenSource, cancellationTokenSource))
            {
                _cancellationTokenSource = null;
            }
            cancellationTokenSource.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        WeakReferenceMessenger.Default.Unregister<LanguageChangedMessage>(this);
        CancelCurrentTest();
        TranslationProperties.Dispose();
        GC.SuppressFinalize(this);
    }
}
