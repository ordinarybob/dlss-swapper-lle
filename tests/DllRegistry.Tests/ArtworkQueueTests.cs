using DLSS_Swapper;
using DLSS_Swapper.Data;
using System.Threading.Channels;

internal static class ArtworkQueueTests
{
    public static async Task RunAsync()
    {
        var started = Channel.CreateUnbounded<Game>();
        var releases = Enumerable.Range(0, 5).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var count = 0;
        var games = Enumerable.Range(0, 5).Select(i => new Game(i.ToString(), async () =>
        {
            Interlocked.Increment(ref count);
            await started.Writer.WriteAsync(new Game(i.ToString(), () => Task.CompletedTask));
            await releases[i].Task;
        })).ToArray();
        var queue = GameCoverHydrationQueue.Instance;
        var tasks = games.Select(game => queue.EnqueueAsync(game)).ToArray();
        async Task<Game> Next() => await started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var first = await Next();
        var second = await Next();
        if (!ReferenceEquals(tasks[0], queue.EnqueueAsync(games[0]))) throw new Exception("Duplicate artwork was queued.");
        GameCoverHydrationQueue.SetConcurrency(1);
        releases[int.Parse(first.ID)].SetResult();
        await tasks[int.Parse(first.ID)].WaitAsync(TimeSpan.FromSeconds(5));
        if (Volatile.Read(ref count) != 2) throw new Exception("Reduced artwork limit was exceeded.");
        GameCoverHydrationQueue.SetConcurrency(3);
        await Next();
        await Next();
        if (Volatile.Read(ref count) != 4) throw new Exception("Increased artwork limit was not applied.");
        foreach (var release in releases) release.TrySetResult();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
        if (count != 5) throw new Exception("Artwork work was lost or repeated.");
        var failing = new Game("failure", () => Task.FromException(new IOException("fixture")));
        try { await queue.EnqueueAsync(failing); throw new Exception("Artwork failure was hidden."); }
        catch (IOException) { }
        await queue.EnqueueAsync(new Game("after-failure", () => Task.CompletedTask));
        Console.WriteLine("Artwork queue: live decrease/increase, deduplication, complete drain and failure recovery passed.");
    }
}

// Only artwork I/O and settings are doubled; the production queue runs unchanged.
namespace DLSS_Swapper
{
    internal sealed class Settings
    {
        public static Settings Instance { get; } = new();
        public static ProxySettings ProxySettings { get; set; } = new();
        public const int MaxCoverHydrationConcurrency = 64;
        public int CoverHydrationConcurrency => 2;
        public int RecursiveScanConcurrency => 2;
        public string[] CustomGameAssetDirectoryPatterns { get; set; } = [];
        public LoggingLevel LoggingLevel => LoggingLevel.Normal;
        public bool DontShowManualLaunchPrompt { get; set; }
        public bool SetupManualLaunchOnImport { get; set; }
        internal bool SaveResult { get; set; }
        internal int SaveCalls { get; set; }
        internal bool SaveJson() { SaveCalls++; return SaveResult; }
    }
    internal static class Logger
    {
        public static void Error(Exception error, string message) { }
        public static void Error(Exception error) { }
        public static void Verbose(string message) { }
        public static void Info(string message) { }
    }
}
namespace DLSS_Swapper.Data
{
    internal sealed class Game(string id, Func<Task> work)
    {
        internal event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        internal int ScanSubscribers => PropertyChanged?.GetInvocationList().Length ?? 0;
        internal bool Processing { get; private set; }
        internal bool NeedsProcessing { get; set; }
        internal Action<Game> StartScan { get; set; } = game => game.SetProcessing(true);
        internal void ProcessGame(bool forceNeedsProcessing) => StartScan(this);
        internal void SetProcessing(bool processing)
        {
            Processing = processing;
            PropertyChanged?.Invoke(this, new(nameof(Processing)));
        }
        internal Func<DLLRecord, Task<(bool Success, string Message, bool PromptToRelaunchAsAdmin)>> Update { get; set; } =
            _ => Task.FromResult((true, "", false));
        internal Task<(bool Success, string Message, bool PromptToRelaunchAsAdmin)> UpdateDllAsync(DLLRecord record) => Update(record);
        public string ID { get; } = id;
        public string Title { get; set; } = id;
        public bool IsFavourite { get; set; }
        public bool? IsHidden { get; set; }
        public bool SaveResult { get; set; }
        public string Notes { get; set; } = "";
        public bool DeleteResult { get; set; }
        public Task<bool> DeleteAsync() => Task.FromResult(DeleteResult);
        public bool? LastBypassBatch { get; private set; }
        public Task<bool> SaveToDatabaseAsync(bool bypassBatch = false)
        {
            LastBypassBatch = bypassBatch;
            return Task.FromResult(SaveResult);
        }
        public Task LoadCoverImageAsync() => work();
        public Task RefreshCoverFromSourceAsync() => work();
    }
}
