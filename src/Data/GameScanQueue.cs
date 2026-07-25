using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

internal sealed class GameScanQueue
{
    static readonly Lazy<GameScanQueue> _instance = new(() => new GameScanQueue());

    public static GameScanQueue Instance => _instance.Value;

    readonly Channel<Func<Task>> _queue = Channel.CreateUnbounded<Func<Task>>(
        new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
        });

    readonly object _idleLock = new();
    TaskCompletionSource _idleCompletion = CompletedCompletion();
    int _pendingCount;

    GameScanQueue()
    {
        var workerCount = Settings.Instance.RecursiveScanConcurrency;
        for (var i = 0; i < workerCount; i++)
        {
            _ = RunWorkerAsync();
        }
    }

    public void Enqueue(Func<Task> scan)
    {
        lock (_idleLock)
        {
            if (_pendingCount == 0)
            {
                _idleCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _pendingCount++;
        }

        if (_queue.Writer.TryWrite(scan) == false)
        {
            MarkCompleted();
            throw new InvalidOperationException("Unable to queue game scan.");
        }
    }

    public Task WhenIdleAsync()
    {
        lock (_idleLock)
        {
            return _idleCompletion.Task;
        }
    }

    async Task RunWorkerAsync()
    {
        await foreach (var scan in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await scan().ConfigureAwait(false);
            }
            catch (Exception err)
            {
                Logger.Error(err);
            }
            finally
            {
                MarkCompleted();
            }
        }
    }

    void MarkCompleted()
    {
        TaskCompletionSource? idleCompletion = null;
        lock (_idleLock)
        {
            _pendingCount--;
            if (_pendingCount == 0)
            {
                idleCompletion = _idleCompletion;
            }
        }

        idleCompletion?.TrySetResult();
    }

    static TaskCompletionSource CompletedCompletion()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        completion.SetResult();
        return completion;
    }
}
