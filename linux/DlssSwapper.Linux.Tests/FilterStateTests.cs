using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class FilterStateTests
{
    public static Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "filter-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new LibraryStateStore(root);
            File.WriteAllText(store.StatePath, "{\"SchemaVersion\":2}");
            var library = new PersistentLibrary(store);
            if (!library.State.HideNonSwappableGames) throw new Exception("Legacy state lost default filtering.");
            if (!library.State.GroupGameLibrariesTogether) throw new Exception("Legacy state lost grouping default.");
            library.UpdateState(state => state.GroupGameLibrariesTogether = false);
            if (new LibraryStateStore(root).Load().GroupGameLibrariesTogether) throw new Exception("Grouping not persisted.");
            library.UpdateState(state => state.HideNonSwappableGames = false);
            if (new LibraryStateStore(root).Load().HideNonSwappableGames) throw new Exception("Filter not persisted.");
            using var held = new FileStream(Path.Combine(root, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var rejected = false;
            try { library.UpdateState(state => state.GroupGameLibrariesTogether = true); } catch (IOException) { rejected = true; }
            if (!rejected || library.State.GroupGameLibrariesTogether) throw new Exception("Failed grouping save changed choice.");
            rejected = false;
            try { library.UpdateState(state => state.HideNonSwappableGames = true); } catch (IOException) { rejected = true; }
            if (!rejected || library.State.HideNonSwappableGames || new LibraryStateStore(root).Load().HideNonSwappableGames)
                throw new Exception("Failed filter save changed committed choice.");
        }
        finally { Directory.Delete(root, true); }
        return Task.CompletedTask;
    }
}
