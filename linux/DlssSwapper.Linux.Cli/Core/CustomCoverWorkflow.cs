namespace DlssSwapper.Linux.Cli.Core;

public static class CustomCoverWorkflow
{
    public static string ValidateSelection(IReadOnlyList<string?> paths)
    {
        if (paths.Count != 1) throw new IOException("Choose exactly one cover image.");
        var path = paths[0];
        if (string.IsNullOrWhiteSpace(path)) throw new IOException("Choose an image stored on a local filesystem.");
        if (!new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new IOException("Use a PNG, JPEG, WebP or BMP cover image.");
        if (!File.Exists(path)) throw new IOException("The selected cover image is unavailable.");
        return Path.GetFullPath(path);
    }

    public static async Task<string> SaveAsync(PersistentLibrary library, string gameRoot, string source,
        IArtworkImageProcessor processor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previous = library.FindGamePreference(gameRoot)?.CustomArtworkPath;
        var directory = Path.Combine(library.StateDirectory, "custom-covers");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var bytes = await File.ReadAllBytesAsync(source, cancellationToken);
            await processor.SavePortraitAsync(bytes, target, 400, 600, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(target) || new FileInfo(target).Length == 0) throw new IOException("The cover image could not be saved.");
            library.UpdateGamePreference(gameRoot, preference =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (preference.CustomArtworkPath != previous)
                    throw new IOException("The custom cover changed while this image was being prepared. Try again.");
                preference.CustomArtworkPath = target;
            });
            return target;
        }
        catch
        {
            if (File.Exists(target)) File.Delete(target);
            throw;
        }
    }

    public static void Remove(PersistentLibrary library, string gameRoot)
        => Remove(library, gameRoot, library.FindGamePreference(gameRoot)?.CustomArtworkPath);

    public static void Remove(PersistentLibrary library, string gameRoot, string? confirmedPath)
    {
        // Retain image files for recovery; never delete a legacy external source.
        library.UpdateGamePreference(gameRoot, preference =>
        {
            if (preference.CustomArtworkPath != confirmedPath)
                throw new IOException("The custom cover changed while confirmation was open. Check the current cover and try again.");
            preference.CustomArtworkPath = null;
        });
    }
}
