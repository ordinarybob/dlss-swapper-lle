using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public class EasyContentDialog : ContentDialog
{
    // WinUI permits only one ContentDialog at a time. Hold the slot through
    // ShowAsync completion, not merely Closed (the closing animation may remain).
    static readonly SemaphoreSlim DisplayGate = new(1, 1);
    CancellationTokenSource? _pendingShow;
    bool _isShowing;

    public EasyContentDialog(XamlRoot xamlRoot) : base()
    {
        XamlRoot = xamlRoot;
        RequestedTheme = Settings.Instance.AppTheme;
    }

    public new Task<ContentDialogResult> ShowAsync()
    {
        if (_pendingShow is not null)
            throw new InvalidOperationException("This dialog is already showing or waiting to be shown.");
        var pending = new CancellationTokenSource();
        _pendingShow = pending;
        return ShowQueuedAsync(pending);
    }

    async Task<ContentDialogResult> ShowQueuedAsync(CancellationTokenSource pending)
    {
        var ownsSlot = false;
        try
        {
            // Keep the calling UI context for every native dialog operation.
            await DisplayGate.WaitAsync(pending.Token);
            ownsSlot = true;
            pending.Token.ThrowIfCancellationRequested();
            _isShowing = true;
            return await base.ShowAsync();
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested)
        {
            return ContentDialogResult.None;
        }
        finally
        {
            _isShowing = false;
            _pendingShow = null;
            pending.Dispose();
            if (ownsSlot) DisplayGate.Release();
        }
    }

    public new void Hide()
    {
        // Background work may finish before its progress dialog gets a slot.
        // Cancel that pending display instead of letting an obsolete dialog open.
        if (_pendingShow is not null && !_isShowing) _pendingShow.Cancel();
        else base.Hide();
    }
}
