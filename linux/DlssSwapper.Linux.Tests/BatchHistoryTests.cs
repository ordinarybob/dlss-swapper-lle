using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class BatchHistoryTests
{
    internal static Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "lle-batch-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(root));
            library.UpdateState(state => state.CardSize = 7);
            var results = Enumerable.Range(0, 3).Select(index => new OperationResult(
                new SelectedGame(index.ToString(), Path.Combine(root, "game" + index), null), "DLSS", "nvngx_dlss.dll", index == 0,
                index == 0 ? "Updated." : "Cancelled.", index == 0 ? OperationOutcome.Completed : OperationOutcome.Cancelled)).ToArray();
            using (var held = new FileStream(Path.Combine(root, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                try { library.RecordOperationHistory(results, "Batch update result"); throw new Exception("Locked history save succeeded"); }
                catch (IOException) { }
                if (library.State.GameHistory.Count != 0) throw new Exception("Failed batch history left partial in-memory records");
            }
            if (new PersistentLibrary(new LibraryStateStore(root)).State.GameHistory.Count != 0) throw new Exception("Failed history persisted records");
            library.RecordOperationHistory(results, "Batch update result");
            var saved = new PersistentLibrary(new LibraryStateStore(root)).State;
            if (saved.GameHistory.Count != 3 || saved.CardSize != 7 || saved.GameHistory.Count(item => item.Version == "Cancelled.") != 2)
                throw new Exception("Batch history lost results or unrelated settings");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        return Task.CompletedTask;
    }
}
