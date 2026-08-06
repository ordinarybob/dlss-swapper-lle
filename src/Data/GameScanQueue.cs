using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

internal sealed class GameScanQueue
{
    internal readonly record struct Progress(long Enqueued, long Completed);

    static readonly Lazy<GameScanQueue> _instance = new(() => new GameScanQueue());

    public static GameScanQueue Instance => _instance.Value;

    readonly Channel<Func<Task>> _queue = Channel.CreateUnbounded<Func<Task>>(
        new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
        });

    readonly object _idleLock = new();
    readonly object _workerLock = new();
    TaskCompletionSource _idleCompletion = CompletedCompletion();
    int _pendingCount;
    int _workerCount;
    int _desiredWorkerCount;
    long _totalEnqueued;
    long _totalCompleted;

    GameScanQueue()
    {
        SetConcurrency(Settings.Instance.RecursiveScanConcurrency);
    }

    public static void UpdateConcurrency(int workerCount)
    {
        if (_instance.IsValueCreated)
        {
            _instance.Value.SetConcurrency(workerCount);
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
        Interlocked.Increment(ref _totalEnqueued);

        if (_queue.Writer.TryWrite(scan) == false)
        {
            MarkCompleted();
            throw new InvalidOperationException("Unable to queue game scan.");
        }
    }

    public Progress GetProgress()
    {
        return new Progress(
            Interlocked.Read(ref _totalEnqueued),
            Interlocked.Read(ref _totalCompleted));
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
        while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            if (_queue.Reader.TryRead(out var scan) == false)
            {
                continue;
            }

            if (TryRetireWorker())
            {
                if (_queue.Writer.TryWrite(scan) == false)
                {
                    Interlocked.Increment(ref _totalCompleted);
                    MarkCompleted();
                }
                return;
            }

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
                Interlocked.Increment(ref _totalCompleted);
                MarkCompleted();
            }
        }
    }

    void SetConcurrency(int workerCount)
    {
        var workersToStart = 0;
        lock (_workerLock)
        {
            if (_desiredWorkerCount == workerCount)
            {
                return;
            }

            _desiredWorkerCount = workerCount;
            workersToStart = Math.Max(0, _desiredWorkerCount - _workerCount);
            _workerCount += workersToStart;
        }

        for (var i = 0; i < workersToStart; i++)
        {
            _ = RunWorkerAsync();
        }

        Logger.Info($"Game scan concurrency set to {workerCount} worker(s).");
    }

    bool TryRetireWorker()
    {
        lock (_workerLock)
        {
            if (_workerCount <= _desiredWorkerCount)
            {
                return false;
            }

            _workerCount--;
            return true;
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
