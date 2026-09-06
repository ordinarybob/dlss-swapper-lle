using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    StreamlinePreviewSnapshot? _preview;
    StreamlinePreviewSnapshot? _confirmedPreview;
    string? _latestTag;
    string? _latestCheckError;
    readonly CancellationTokenSource _metadataCancellation = new();
    readonly HashSet<string> _untrustedTargets = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    bool _refreshingSelection;
    bool _confirmationOpen;
    bool _pendingAll = true;
    readonly ContentDialog _dialog;
    CancellationTokenSource? _downloadCancellation;
    bool _busy;
    bool _unloaded;
    bool _hasPendingRecovery;
    enum PendingOperation { Update, Restore, Recover }
    PendingOperation _pendingOperation;

    public StreamlineComponentsControl(Game game, ContentDialog dialog)
    {
        InitializeComponent();
        _game = game;
        _dialog = dialog;
        _dialog.PrimaryButtonText = "Apply selected";
        _dialog.SecondaryButtonText = "Apply all";
        _dialog.IsPrimaryButtonEnabled = false;
        _dialog.IsSecondaryButtonEnabled = false;
        _dialog.PrimaryButtonClick += ApplySelected_Click;
        _dialog.SecondaryButtonClick += ApplyAll_Click;
        _dialog.Closing += Dialog_Closing;
        _dialog.Opened += Dialog_Opened;
        _dialog.Closed += (_, _) =>
        {
            _unloaded = true;
            _metadataCancellation.Cancel();
            _downloadCancellation?.Cancel();
            _dialog.Closing -= Dialog_Closing;
            _dialog.PrimaryButtonClick -= ApplySelected_Click;
            _dialog.SecondaryButtonClick -= ApplyAll_Click;
            _metadataCancellation.Dispose();
        };
    }

    void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (!_busy)
        {
            return;
        }

        // Keep this form alive until its worker has stopped; reopening must not
        // start another mutation against the same files.
        args.Cancel = true;
        _downloadCancellation?.Cancel();
        StatusText.Text = _downloadCancellation is null
            ? "Please wait for the current operation to finish."
            : "Cancelling the download…";
    }

    async void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs e)
    {
        _dialog.Opened -= Dialog_Opened;
        // Native dialog templates may unload/reparent their content while opening.
        // Use the dialog lifetime, and never queue the network lookup behind disk work.
        await Task.WhenAll(CheckLatestVersionAsync(), RefreshInstalledAsync());
    }

    async Task CheckLatestVersionAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_metadataCancellation.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var release = await StreamlineReleaseManager.FetchLatestAsync(timeout.Token);
            if (_unloaded) return;
            _latestTag = release.Tag;
            _latestCheckError = null;
        }
        catch (Exception exception)
        {
            if (_unloaded) return;
            _latestCheckError = exception is OperationCanceledException ? "request timed out" : exception.Message;
        }
        RefreshPackageText();
        RefreshActionButtons();
    }

    void RefreshPackageText()
    {
        var latest = _latestTag is not null ? $"Latest SDK: {_latestTag}"
            : _latestCheckError is not null ? $"Latest SDK check failed: {_latestCheckError}"
            : "Checking latest SDK version…";
        PackageText.Text = latest + (_package is null ? string.Empty
            : $" · Comparison package: {_package.Tag} (cached).");
        var notesTag = _latestTag ?? _package?.Tag;
        ReleaseNotesLink.Visibility = notesTag is null ? Visibility.Collapsed : Visibility.Visible;
        if (notesTag is not null) ReleaseNotesLink.NavigateUri = new Uri(
            "https://github.com/NVIDIA-RTX/Streamline/releases/tag/" + Uri.EscapeDataString(notesTag));
        RefreshComponentRows();
    }

    void RefreshComponentRows()
    {
        var showComparison = !NeedsPackageDownload;
        var actionWidth = showComparison ? new GridLength(1.4, GridUnitType.Star) : new GridLength(0);
        var actionVisibility = showComparison ? Visibility.Visible : Visibility.Collapsed;
        ActionHeaderColumn.Width = actionWidth;
        ActionHeader.Visibility = actionVisibility;
        if (_preview is null) return;
        _refreshingSelection = true;
        try
        {
            _selectedPaths.IntersectWith(_preview.Components.Select(item => item.TargetPath));
            // Release metadata is an SDK label, never a fabricated DLL file version.
            ComponentList.ItemsSource = _preview.Components.Select(item => new
            {
                item.TargetPath, item.FileName, item.Description, item.RelativeDirectory,
                IsSelected = _selectedPaths.Contains(item.TargetPath),
                item.Installed, item.Package, item.InstalledVersion, item.OriginalVersion, item.RestoreText,
                PackageVersion = NeedsPackageDownload
                    ? _latestTag is not null ? $"{_latestTag} SDK" : _latestCheckError is not null ? "Version unavailable" : "Checking…"
                    : item.PackageVersion,
                item.UpdateText,
                ActionWidth = actionWidth,
                ActionVisibility = actionVisibility,
            }).ToArray();
        }
        finally { _refreshingSelection = false; }
    }

    bool NeedsPackageDownload => _package is null || (_latestTag is not null && _latestTag != _package.Tag);

    async Task RefreshInstalledAsync(bool reloadPackage = true)
    {
        SetBusy(true, "Checking the game installation…");
        try
        {
            if (reloadPackage)
            {
                var cached = await Task.Run(StreamlineReleaseManager.FindNewestCached);
                _package = cached;
            }
            _installedFiles = await Task.Run(() =>
                StreamlineComponentSet.FindInstalled(_game.InstallPath));
            _hasPendingRecovery = await Task.Run(() => StreamlineComponentSet.HasPendingRecovery(_game.InstallPath));
            _preview = await Task.Run(() => StreamlineDecisionPreview.Create(
                _game.InstallPath, _installedFiles, _package?.DirectoryPath));
            var allowUntrusted = Settings.Instance.AllowUntrusted;
            var unsignedFiles = _package is null || allowUntrusted ? [] : await Task.Run(() =>
                _preview.Components.Where(item => item.Package.State == StreamlineFileState.Available)
                    .Select(item => item.Package.Path).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(path => !WinTrust.VerifyEmbeddedSignature(path))
                    .Select(Path.GetFileName).ToArray());
            _untrustedTargets.Clear();
            foreach (var item in _preview.Components)
                if (unsignedFiles.Contains(Path.GetFileName(item.Package.Path), StringComparer.OrdinalIgnoreCase))
                    _untrustedTargets.Add(item.TargetPath);
            TrustText.Text = _package is null ? string.Empty : allowUntrusted
                ? "Signature checks are disabled by your Allow Untrusted setting."
                : unsignedFiles.Length > 0
                    ? $"Cannot verify package signatures: {string.Join(", ", unsignedFiles)}. Your trust setting prevents applying these files."
                    : _preview.Components.Any(item => item.Package.State != StreamlineFileState.Available)
                        ? "Some package files are unavailable; signatures could not all be checked."
                        : _preview.Components.Count > 0 ? "Package component signatures verified." : string.Empty;
            EmptyComponentsText.Visibility = _installedFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DetectedText.Visibility = _installedFiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            DetectedText.Text = _installedFiles.Count == 0
                ? "No Streamline components found."
                : _package is null ? $"{_installedFiles.Count} installed components"
                    : $"{_installedFiles.Count} components · {DescribeChanges(_preview, false)}";
            RefreshPackageText();
            StatusText.Text = _hasPendingRecovery
                ? "An interrupted operation needs recovery before another update or restore."
                : string.Empty;
        }
        catch (Exception exception)
        {
            _preview = null;
            ComponentList.ItemsSource = null;
            StatusText.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    async void PrepareButton_Click(object sender, RoutedEventArgs e)
        => await DownloadLatestPackageAsync();

    async Task<bool> DownloadLatestPackageAsync()
    {
        if (_busy) return false;
        using var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;
        SetBusy(true, "Checking NVIDIA's latest Streamline release…", showProgress: true);
        try
        {
            _package = await StreamlineReleaseManager.PrepareLatestAsync(
                (downloaded, total, percent) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_unloaded || cancellation.IsCancellationRequested || _downloadCancellation != cancellation) return;
                        DownloadProgress.IsIndeterminate = total <= 0;
                        DownloadProgress.Value = percent;
                        StatusText.Text = total > 0
                            ? $"Downloading package: {percent:N0}%"
                            : $"Downloading package: {downloaded:N0} bytes";
                    });
                }, cancellation.Token);
            _latestTag = _package.Tag;
            _latestCheckError = null;
            await RefreshInstalledAsync(reloadPackage: false);
            return _preview is not null;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Download cancelled. No game files were changed.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Package download failed: {exception.Message}";
        }
        finally
        {
            _downloadCancellation = null;
            SetBusy(false);
        }
        return false;
    }

    async void ApplySelected_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        await ApplyComponentsAsync(applyAll: false);
    }

    async void ApplyAll_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        await ApplyComponentsAsync(applyAll: true);
    }

    async Task ApplyComponentsAsync(bool applyAll)
    {
        if (_busy || _installedFiles.Count == 0)
        {
            return;
        }

        var selected = _selectedPaths.ToArray();
        if (!applyAll && selected.Length == 0) return;
        if (NeedsPackageDownload && !await DownloadLatestPackageAsync()) return;
        await RefreshInstalledAsync(reloadPackage: false);
        try
        {
            var scope = applyAll ? _preview : _preview?.SelectTargets(selected);
            if (scope?.CanUpdate != true || !IsTrusted(scope) || _hasPendingRecovery) return;
            if (scope.Components.Count < _preview!.Components.Count)
            {
                ShowConfirmation(PendingOperation.Update, MixedVersionWarning +
                    $" Update only {scope.Components.Count} selected components? Close the game first.", scope, applyAll);
                return;
            }
            _pendingOperation = PendingOperation.Update;
            _confirmedPreview = scope;
            _pendingAll = applyAll;
            await ExecuteOperationAsync();
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    async void RestoreButton_Click(object sender, RoutedEventArgs e)
        => await PrepareRestoreAsync(applyAll: true);

    async void RestoreSelectedButton_Click(object sender, RoutedEventArgs e)
        => await PrepareRestoreAsync(applyAll: false);

    const string MixedVersionWarning = "Updating or restoring only part of Streamline is not recommended. " +
        "It may leave mixed versions that do not work together, causing crashes or broken graphics features. " +
        "Updating or restoring the complete set is recommended.";

    async Task PrepareRestoreAsync(bool applyAll)
    {
        if (_busy) return;
        var selected = _selectedPaths.ToArray();
        if (!applyAll && selected.Length == 0) return;
        await RefreshInstalledAsync(reloadPackage: false);
        try
        {
            var scope = applyAll ? _preview : _preview?.SelectTargets(selected);
            if (scope?.CanRestore != true || _hasPendingRecovery) return;
            var partial = scope.Components.Count < _preview!.Components.Count ||
                scope.Components.Any(item => item.Original.State == StreamlineFileState.Missing);
            ShowConfirmation(PendingOperation.Restore, (partial ? MixedVersionWarning + " " : string.Empty) +
                $"Restore {(applyAll ? "all" : "selected")} originals: {DescribeChanges(scope, true)}. " +
                "Files without originals are left unchanged. Close the game before continuing.", scope, applyAll);
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    void RecoverButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !_hasPendingRecovery) return;
        ShowConfirmation(PendingOperation.Recover,
            "Recover the interrupted operation using its retained copies? Files changed outside the app will be left untouched. Close the game before continuing.");
    }

    void ShowConfirmation(PendingOperation operation, string message, StreamlinePreviewSnapshot? scope = null, bool applyAll = true)
    {
        _pendingOperation = operation;
        _confirmedPreview = scope ?? _preview;
        _pendingAll = applyAll;
        _confirmationOpen = true;
        ConfirmationText.Text = message;
        ConfirmButton.Content = operation switch
        {
            PendingOperation.Restore => "Confirm restore",
            PendingOperation.Recover => "Confirm recovery",
            _ => "Confirm package changes",
        };
        ConfirmationPanel.Visibility = Visibility.Visible;
        RefreshActionButtons();
    }

    void CancelConfirmation_Click(object sender, RoutedEventArgs e)
    {
        ConfirmationPanel.Visibility = Visibility.Collapsed;
        _confirmationOpen = false;
        SetBusy(false);
    }

    async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        => await ExecuteOperationAsync();

    async Task ExecuteOperationAsync()
    {
        if (_busy) return;
        var operation = _pendingOperation;
        var package = _package;
        var confirmed = _confirmedPreview;
        var applyAll = _pendingAll;
        var allowUntrusted = Settings.Instance.AllowUntrusted;
        if (operation == PendingOperation.Update && package is null) return;
        ConfirmationPanel.Visibility = Visibility.Collapsed;
        _confirmationOpen = false;
        SetBusy(true, "Applying the component operation…");
        try
        {
            var result = await Task.Run(() =>
            {
                if (operation == PendingOperation.Recover) return StreamlineComponentSet.RecoverInterrupted(_game.InstallPath);
                var currentFiles = applyAll ? StreamlineComponentSet.FindInstalled(_game.InstallPath)
                    : confirmed?.Components.Select(item => item.TargetPath).ToArray() ?? [];
                var current = StreamlineDecisionPreview.Create(_game.InstallPath, currentFiles, package?.DirectoryPath);
                if (confirmed is null || !confirmed.IsEquivalentTo(current, operation == PendingOperation.Restore))
                    return new StreamlineComponentOperationResult(false, 0,
                        "Files changed since the comparison. Nothing was applied; review the refreshed versions and confirm again.");
                var targets = confirmed.Components.Select(item => item.TargetPath).ToArray();
                if (operation == PendingOperation.Restore) return StreamlineComponentSet.RestoreOriginals(targets, expectedPreview: confirmed);
                if (!allowUntrusted)
                {
                    foreach (var path in confirmed.Components.Select(item => item.Package.Path).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        if (!WinTrust.VerifyEmbeddedSignature(path))
                            return new StreamlineComponentOperationResult(false, 0,
                                $"Cannot verify the signature of {Path.GetFileName(path)}. No game files were changed. " +
                                "The existing Allow Untrusted setting controls whether unsigned components can be used.");
                    }
                }
                return StreamlineComponentSet.UpdateExisting(package!.DirectoryPath, targets, expectedPreview: confirmed);
            });
            var historyWarning = operation == PendingOperation.Recover
                ? null
                : await StreamlineHistory.TryRecordAsync(_game, result, operation == PendingOperation.Restore);
            await RefreshInstalledAsync();
            StatusText.Text = historyWarning is null ? result.Message : $"{result.Message}\n{historyWarning}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"The operation could not finish: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    void SetBusy(bool busy, string? status = null, bool showProgress = false)
    {
        _busy = busy;
        RefreshActionButtons();
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

    bool IsTrusted(StreamlinePreviewSnapshot scope) => scope.Components.All(item => !_untrustedTargets.Contains(item.TargetPath));

    void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _confirmationOpen || _preview is null) return;
        var selectAll = _selectedPaths.Count != _preview.Components.Count;
        _selectedPaths.Clear();
        if (selectAll) _selectedPaths.UnionWith(_preview.Components.Select(item => item.TargetPath));
        _refreshingSelection = true;
        try { UpdateVisibleSelection(ComponentList); }
        finally { _refreshingSelection = false; }
        RefreshActionButtons();
    }

    void UpdateVisibleSelection(DependencyObject parent)
    {
        if (parent is CheckBox checkBox && checkBox.Tag is string path)
            checkBox.IsChecked = _selectedPaths.Contains(path);
        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
            UpdateVisibleSelection(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index));
    }

    void ComponentCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshingSelection || sender is not CheckBox checkBox || checkBox.Tag is not string path) return;
        if (checkBox.IsChecked == true) _selectedPaths.Add(path);
        else _selectedPaths.Remove(path);
        RefreshActionButtons();
    }

    void RefreshActionButtons()
    {
        // Selection events may fire while InitializeComponent is constructing the control.
        if (_dialog is null || SelectionText is null) return;
        var available = !_busy && !_confirmationOpen;
        var selected = _preview?.SelectTargets(_selectedPaths.Intersect(
            _preview.Components.Select(item => item.TargetPath), StringComparer.OrdinalIgnoreCase));
        PrepareButton.IsEnabled = available;
        ComponentList.IsEnabled = available;
        SelectAllCheckBox.IsEnabled = available && _preview?.Components.Count > 0;
        SelectAllCheckBox.IsChecked = _selectedPaths.Count == 0 ? false
            : _selectedPaths.Count == _preview?.Components.Count ? true : (bool?)null;
        _dialog.IsPrimaryButtonEnabled = available && !_hasPendingRecovery &&
            (NeedsPackageDownload ? selected?.Components.Count > 0 : selected?.CanUpdate == true && IsTrusted(selected));
        _dialog.IsSecondaryButtonEnabled = available && !_hasPendingRecovery &&
            (NeedsPackageDownload ? _preview?.Components.Count > 0 : _preview?.CanUpdate == true && IsTrusted(_preview));
        RestoreButton.IsEnabled = available && !_hasPendingRecovery && _preview?.CanRestore == true;
        RestoreSelectedButton.IsEnabled = available && !_hasPendingRecovery && selected?.CanRestore == true;
        var changeCount = selected?.Components.Count(item => item.UpdateChanges) ?? 0;
        SelectionText.Text = NeedsPackageDownload
            ? $"{_selectedPaths.Count} selected · Apply downloads and updates the files. Close the game first."
            : $"{_selectedPaths.Count} selected · {changeCount} {(changeCount == 1 ? "change" : "changes")}";
        RecoverButton.Visibility = _hasPendingRecovery ? Visibility.Visible : Visibility.Collapsed;
        RecoverButton.IsEnabled = available && _hasPendingRecovery;
    }

    static string DescribeChanges(StreamlinePreviewSnapshot preview, bool restore)
    {
        var changes = preview.Components
            .Where(item => !restore || item.Original.State != StreamlineFileState.Missing)
            .Select(item => restore ? item.Restore : item.Update).ToArray();
        var labels = new List<string>();
        foreach (var (kind, singular, plural) in new[]
        {
            (StreamlineChangeKind.Upgrade, "upgrade", "upgrades"),
            (StreamlineChangeKind.Downgrade, "downgrade", "downgrades"),
            (StreamlineChangeKind.SameVersionDifferentBytes, "same-version replacement", "same-version replacements"),
            (StreamlineChangeKind.UnknownVersion, "replacement with unknown version order", "replacements with unknown version order"),
            (StreamlineChangeKind.Identical, "unchanged", "unchanged"),
            (StreamlineChangeKind.Unavailable, "unavailable", "unavailable"),
        })
        {
            var count = changes.Count(item => item.Kind == kind);
            if (count > 0) labels.Add($"{count} {(count == 1 ? singular : plural)}");
        }
        return labels.Count == 0 ? "nothing to replace" : string.Join(" · ", labels);
    }
}
