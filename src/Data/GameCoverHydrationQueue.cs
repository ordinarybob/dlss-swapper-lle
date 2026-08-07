using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

internal sealed class GameCoverHydrationQueue
{
    readonly record struct WorkItem(
        Game Game,
        bool RefreshFromSource,
        TaskCompletionSource Completion);

    static readonly Lazy<GameCoverHydrationQueue> _instance = new(() => new GameCoverHydrationQueue());

    public static GameCoverHydrationQueue Instance => _instance.Value;

    readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>(
        new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
        });

    readonly ConcurrentDictionary<(string GameId, bool RefreshFromSource), TaskCompletionSource> _queuedWork = new();

    GameCoverHydrationQueue()
    {
        var workerCount = Settings.Instance.CoverHydrationConcurrency;
        for (var i = 0; i < workerCount; i++)
        {
            _ = RunWorkerAsync();
        }
    }

    public void Enqueue(Game game, bool refreshFromSource = false)
    {
        _ = ObserveCompletionAsync(EnqueueAsync(game, refreshFromSource));
    }

    static async Task ObserveCompletionAsync(Task completion)
    {
        try
        {
            await completion.ConfigureAwait(false);
        }
        catch
        {
            // The worker already logs artwork failures for fire-and-forget callers.
        }
    }

    public Task EnqueueAsync(Game game, bool refreshFromSource = false)
    {
        var workKey = (game.ID, refreshFromSource);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuedCompletion = _queuedWork.GetOrAdd(workKey, completion);
        if (ReferenceEquals(queuedCompletion, completion) == false)
        {
            return queuedCompletion.Task;
        }

        if (_queue.Writer.TryWrite(new WorkItem(game, refreshFromSource, completion)) == false)
        {
            _queuedWork.TryRemove(workKey, out _);
            var error = new InvalidOperationException("Unable to queue game artwork.");
            completion.TrySetException(error);
            throw error;
        }

        return completion.Task;
    }

    async Task RunWorkerAsync()
    {
        await foreach (var workItem in _queue.Reader.ReadAllAsync())
        {
            Exception? failure = null;
            try
            {
                if (workItem.RefreshFromSource)
                {
                    await workItem.Game.RefreshCoverFromSourceAsync().ConfigureAwait(false);
                }
                else
                {
                    await workItem.Game.LoadCoverImageAsync().ConfigureAwait(false);
                }
            }
            catch (Exception err)
            {
                failure = err;
                Logger.Error(err, $"Unable to load artwork for {workItem.Game.Title}.");
            }
            finally
            {
                _queuedWork.TryRemove((workItem.Game.ID, workItem.RefreshFromSource), out _);
                if (failure is null)
                {
                    workItem.Completion.TrySetResult();
                }
                else
                {
                    workItem.Completion.TrySetException(failure);
                }
            }
        }
    }
}
