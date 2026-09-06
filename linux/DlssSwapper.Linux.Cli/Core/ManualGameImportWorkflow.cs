namespace DlssSwapper.Linux.Cli.Core;

public static class ManualGameImportWorkflow
{
    public static string Validate(string path, IEnumerable<string> existingPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The game folder is unavailable.");
        if (PathComparers.FileSystemPath.Equals(root, Path.GetPathRoot(root)))
            throw new IOException("Select a game folder, not a filesystem root.");
        if (existingPaths.Any(existing => PathComparers.FileSystemPath.Equals(root,
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(existing)))))
            throw new IOException("This game folder is already in the library.");
        return root;
    }

    public static async Task<ManualGameState> SaveAsync(PersistentLibrary library, string path, string name,
        byte[]? cover, IArtworkImageProcessor processor, Func<IEnumerable<string>> existingPaths,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new IOException("Enter a game name.");
        var root = Validate(path, existingPaths().Concat(library.State.ManualGames.Select(game => game.RootPath)));
        string? target = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cover is not null)
            {
                var directory = Path.Combine(library.StateDirectory, "custom-covers");
                Directory.CreateDirectory(directory);
                target = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".png");
                await processor.SavePortraitAsync(cover, target, 400, 600, cancellationToken);
                if (!File.Exists(target) || new FileInfo(target).Length == 0)
                    throw new IOException("The cover image could not be saved.");
            }
            return library.UpdateState(state =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Validate(root, existingPaths().Concat(state.ManualGames.Select(game => game.RootPath)));
                var game = new ManualGameState { Name = name, RootPath = root };
                state.ManualGames.Add(game);
                if (target is not null)
                {
                    var preference = state.GamePreferences.FirstOrDefault(item => PathComparers.FileSystemPath.Equals(item.RootPath, root));
                    if (preference is null)
                    {
                        preference = new GamePreferenceState { RootPath = root };
                        state.GamePreferences.Add(preference);
                    }
                    preference.CustomArtworkPath = target;
                }
                return game;
            });
        }
        catch
        {
            if (target is not null && File.Exists(target)) File.Delete(target);
            throw;
        }
    }
}
