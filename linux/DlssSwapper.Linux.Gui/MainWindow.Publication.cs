using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using DlssSwapper.Linux.Gui.ViewModels;
using DlssSwapper.Shared;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private Task _viewPublication = Task.CompletedTask;
    private Task _rowPreparation = Task.CompletedTask;
    private bool _preparingRows;
    private long _publicationGeneration;
    private int UiBatchSize => _library?.State.UiCollectionBatchSize ?? 550;

    private async Task RunUiBatchesAsync<T>(IEnumerable<T> items, Action<T> apply, long? generation = null)
    {
        var count = 0;
        var slice = Stopwatch.StartNew();
        foreach (var item in items)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            if (generation is not null && generation != _publicationGeneration) return;
            apply(item);
            if (++count >= UiBatchSize || slice.ElapsedMilliseconds >= 8)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                count = 0;
                slice.Restart();
            }
        }
    }

    private async Task PublishViewAsync(IReadOnlyList<GameRowViewModel> rows,
        IReadOnlyList<GameGroup<GameRowViewModel>> groups)
    {
        var generation = ++_publicationGeneration;
        _viewModel.IsPublishingView = true;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            if (generation != _publicationGeneration) return;
            _lifetime.Token.ThrowIfCancellationRequested();
            _viewModel.Games.Clear();
            _viewModel.GameGroups.Clear();
            await RunUiBatchesAsync(rows, row => _viewModel.Games.Add(row), generation);
            foreach (var group in groups)
            {
                if (generation != _publicationGeneration) return;
                _lifetime.Token.ThrowIfCancellationRequested();
                var items = new ObservableCollection<GameRowViewModel>();
                _viewModel.GameGroups.Add(new(group.Name, items));
                await RunUiBatchesAsync(group.Items, items.Add, generation);
            }
            if (generation == _publicationGeneration) UpdateSelectionSummary();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { AddAlert("Warning", LanguageAppearance.Format("Linux_GuiRemainingGameListFailed", "Could not update the game list: {0}", error.Message)); }
        finally
        {
            if (generation == _publicationGeneration) _viewModel.IsPublishingView = false;
        }
    }
}
