using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class IgnoredPathsTests
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("lle-ignored-paths-");
        try
        {
            var ignored = Path.Combine(root.FullName, "Games");
            var child = Path.Combine(ignored, "Game");
            var neighbor = Path.Combine(root.FullName, "GamesOther");
            var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root.FullName, "state")));
            library.UpdateState(state =>
            {
                state.IgnoredPaths = [ignored + Path.DirectorySeparatorChar, ignored];
                state.ManualGames = [new() { Name = "Ignored", RootPath = child }, new() { Name = "Visible", RootPath = neighbor }];
            });
            var reopened = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
            if (reopened.State.IgnoredPaths.Count != 1 || !GameViewPolicy.IsIgnored(reopened.State, ignored)
                || !GameViewPolicy.IsIgnored(reopened.State, child) || GameViewPolicy.IsIgnored(reopened.State, neighbor))
                throw new Exception("Ignored paths lost exact-root/descendant/separator boundaries.");
            if (reopened.Merge(new([], [])).Single().Name != "Visible" || reopened.State.ManualGames.Count != 2)
                throw new Exception("Ignoring did not filter discovery or deleted saved games.");
            // The excluded root does not exist: either scanner must skip it before filesystem validation.
            var service = new LibraryScanService(DllCatalog.Empty());
            var fast = await service.ScanFastAsync([new("Ignored", child, null)], reopened.State);
            var deep = await service.ScanDeepAsync([new("Ignored", child, null)], reopened);
            foreach (var scan in new[] { fast, deep })
                if (scan.Games.Single().Warnings.Single() != "Game folder is excluded by ignored paths.")
                    throw new Exception("Ignored folder reached filesystem scanning.");
            reopened.UpdateState(state => state.IgnoredPaths = []);
            if (reopened.Merge(new([], [])).Count != 2) throw new Exception("Removing exclusion did not restore games.");
            try { GameViewPolicy.NormalizeIgnoredPaths(["relative"]); throw new Exception("Relative exclusion accepted."); }
            catch (IOException) { }
        }
        finally { root.Delete(true); }
    }
}
