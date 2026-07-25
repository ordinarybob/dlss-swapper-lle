using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

internal sealed class GameCoverHydrationQueue
{
    readonly record struct WorkItem(Game Game, bool RefreshFromSource);

    static readonly Lazy<GameCoverHydrationQueue> _instance = new(() => new GameCoverHydrationQueue());

    public static GameCoverHydrationQueue Instance => _instance.Value;

    readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>(
        new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
        });

    readonly ConcurrentDictionary<(string GameId, bool RefreshFromSource), byte> _queuedWork = new();

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
        var workKey = (game.ID, refreshFromSource);
        if (_queuedWork.TryAdd(workKey, 0) == false)
        {
            return;
        }

        if (_queue.Writer.TryWrite(new WorkItem(game, refreshFromSource)) == false)
        {
            _queuedWork.TryRemove(workKey, out _);
            throw new InvalidOperationException("Unable to queue game artwork.");
        }
    }

    async Task RunWorkerAsync()
    {
        await foreach (var workItem in _queue.Reader.ReadAllAsync())
        {
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
                Logger.Error(err, $"Unable to load artwork for {workItem.Game.Title}.");
            }
            finally
            {
                _queuedWork.TryRemove((workItem.Game.ID, workItem.RefreshFromSource), out _);
            }
        }
    }
}
