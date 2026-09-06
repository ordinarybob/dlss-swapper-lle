using System.Diagnostics;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class StateProcessTests
{
    internal static int Worker(string directory, string mode)
    {
        var store = new LibraryStateStore(directory);
        var state = store.Load();
        using var heldLock = mode == "hold"
            ? new FileStream(Path.Combine(directory, ".state.write.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None)
            : null;
        Console.WriteLine("ready");
        if (Console.ReadLine() != "go") return 2;
        if (mode == "hold") return 0;
        state.HddMode = true;
        try
        {
            store.Save(state);
            return 0;
        }
        catch (IOException) { return 20; }
    }

    internal static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lle-state-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var children = new List<Process>();
        try
        {
            var first = Start(directory, "write", children);
            var stale = Start(directory, "write", children);
            await Ready(first);
            await Ready(stale);
            await Finish(first, 0);
            var committed = File.ReadAllBytes(Path.Combine(directory, "state.json"));
            await Finish(stale, 20);
            if (!committed.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(directory, "state.json"))))
                throw new Exception("Stale child overwrote committed state.");

            var holder = Start(directory, "hold", children);
            await Ready(holder);
            var contender = Start(directory, "write", children);
            await Ready(contender);
            await Finish(contender, 20);
            await Finish(holder, 0);
            var retry = Start(directory, "write", children);
            await Ready(retry);
            await Finish(retry, 0);

            var resetDirectory = Path.Combine(directory, "Linux");
            Directory.CreateDirectory(resetDirectory);
            var resetStore = new LibraryStateStore(resetDirectory);
            var resetLibrary = new PersistentLibrary(resetStore);
            resetLibrary.UpdateState(value => value.CardSize = 7);
            var resetStale = Start(resetDirectory, "write", children);
            await Ready(resetStale);
            new PersistentLibrary(new LibraryStateStore(resetDirectory)).ResetLocalData(resetDirectory);
            await Finish(resetStale, 20);
            if (File.Exists(resetStore.StatePath))
                throw new Exception("Stale writer resurrected reset state.");
        }
        finally
        {
            foreach (var child in children)
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                child.Dispose();
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Process Start(string directory, string mode, List<Process> children)
    {
        var executable = Environment.ProcessPath ?? throw new Exception("Missing test process path.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(StateProcessTests).Assembly.Location);
        start.ArgumentList.Add("--state-worker");
        start.ArgumentList.Add(directory);
        start.ArgumentList.Add(mode);
        var child = Process.Start(start) ?? throw new Exception("Could not start state fixture worker.");
        children.Add(child);
        return child;
    }

    private static async Task Ready(Process child)
    {
        if (await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)) != "ready")
            throw new Exception("State fixture worker failed to load.");
    }

    private static async Task Finish(Process child, int expected)
    {
        await child.StandardInput.WriteLineAsync("go");
        await child.StandardInput.FlushAsync();
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        if (child.ExitCode != expected)
            throw new Exception($"State worker returned {child.ExitCode}; expected {expected}.");
    }
}
