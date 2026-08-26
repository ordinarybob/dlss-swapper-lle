using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.Streamline;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public sealed partial class StreamlineComponentsControl : UserControl
{
    readonly Game _game;
    IReadOnlyList<string> _installedFiles = [];
    StreamlinePackage? _package;

    public StreamlineComponentsControl(Game game)
    {
        InitializeComponent();
        _game = game;
        Loaded += StreamlineComponentsControl_Loaded;
    }

    async void StreamlineComponentsControl_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= StreamlineComponentsControl_Loaded;
        _package = StreamlineReleaseManager.FindNewestCached();
        await RefreshInstalledAsync();
    }

    async Task RefreshInstalledAsync()
    {
        SetBusy(true, "Checking the game installation…");
        try
        {
            _installedFiles = await Task.Run(() =>
                StreamlineComponentSet.FindInstalled(_game.InstallPath));
            DetectedText.Text = _installedFiles.Count == 0
                ? "No Streamline components found."
                : $"{_installedFiles.Count} Streamline component(s) found.";
            ComponentList.ItemsSource = _installedFiles.Select(path =>
                Path.GetRelativePath(_game.InstallPath, path)).ToArray();
            PackageText.Text = _package is null
                ? "Latest package not checked."
                : $"Cached package: {_package.Tag}";
            StatusText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    async void PrepareButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Checking NVIDIA's latest Streamline release…", showProgress: true);
        try
        {
            _package = await StreamlineReleaseManager.PrepareLatestAsync(
                (downloaded, total, percent) =>
                {
                    DownloadProgress.IsIndeterminate = total <= 0;
                    DownloadProgress.Value = percent;
                    StatusText.Text = total > 0
                        ? $"Downloading package: {percent:N0}%"
                        : $"Downloading package: {downloaded:N0} bytes";
                });
            PackageText.Text = $"Ready: {_package.Tag} production components";
            StatusText.Text = "The package is staged. No game files have been changed.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Package download failed: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_package is null || _installedFiles.Count == 0)
        {
            return;
        }

        var confirmation = new EasyContentDialog(XamlRoot)
        {
            Title = "Update Streamline components?",
            Content = $"Replace {_installedFiles.Count} existing component(s) with {_package.Tag}? " +
                "The app will retain each original component and roll back the entire set if any replacement fails.",
            PrimaryButtonText = "Update",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        SetBusy(true, "Updating the component set…");
        var result = await Task.Run(() =>
            StreamlineComponentSet.UpdateExisting(_package.DirectoryPath, _installedFiles));
        StatusText.Text = result.Message;
        SetBusy(false);
    }

    async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var restorableCount = _installedFiles.Count(path =>
            File.Exists(path + StreamlineComponentSet.BackupSuffix));
        if (restorableCount == 0)
        {
            return;
        }

        var confirmation = new EasyContentDialog(XamlRoot)
        {
            Title = "Restore original Streamline components?",
            Content = $"Restore {restorableCount} component(s) from their first retained originals?",
            PrimaryButtonText = "Restore",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        SetBusy(true, "Restoring the original component set…");
        var result = await Task.Run(() =>
            StreamlineComponentSet.RestoreOriginals(_installedFiles));
        StatusText.Text = result.Message;
        SetBusy(false);
    }

    void SetBusy(bool busy, string? status = null, bool showProgress = false)
    {
        PrepareButton.IsEnabled = busy == false;
        UpdateButton.IsEnabled = busy == false && _package is not null && _installedFiles.Count > 0;
        RestoreButton.IsEnabled = busy == false && _installedFiles.Any(path =>
            File.Exists(path + StreamlineComponentSet.BackupSuffix));
        DownloadProgress.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        if (showProgress)
        {
            DownloadProgress.IsIndeterminate = true;
            DownloadProgress.Value = 0;
        }

        if (status is not null)
        {
            StatusText.Text = status;
        }
    }
}
