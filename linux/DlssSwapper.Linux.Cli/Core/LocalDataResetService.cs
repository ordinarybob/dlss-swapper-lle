namespace DlssSwapper.Linux.Cli.Core;

public sealed class LocalDataResetService
{
    private readonly LibraryStateStore _store;
    private readonly string _cacheDirectory;

    public LocalDataResetService(
        LibraryStateStore store,
        string? cacheDirectory = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cacheDirectory = Path.GetFullPath(
            cacheDirectory
            ?? Path.GetDirectoryName(ArtworkService.GetDefaultCacheRoot())
            ?? throw new InvalidOperationException("Could not locate the Linux cache directory."));
    }

    public void Reset()
    {
        // Validate every target before deleting any data.
        ValidateOwnedDirectory(_store.StateDirectory);
        ValidateOwnedDirectory(_cacheDirectory);
        using var coordination = _store.AcquireCoordinationLock();
        if (Directory.Exists(_store.StateDirectory))
        {
            // Check for an existing writer before removal. The coordination lease
            // remains held after releasing this file handle and through both deletes.
            using (_store.AcquireWriteLock()) { }
        }
        DeleteOwnedDirectory(_store.StateDirectory);
        if (!PathComparers.FileSystemPath.Equals(
            _store.StateDirectory,
            _cacheDirectory))
        {
            DeleteOwnedDirectory(_cacheDirectory);
        }
    }

    private static void DeleteOwnedDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        ValidateOwnedDirectory(path);
        Directory.Delete(path, recursive: true);
    }

    private static void ValidateOwnedDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath) ?? string.Empty);
        var leaf = Path.GetFileName(fullPath);
        if (PathComparers.FileSystemPath.Equals(fullPath, root)
            || (leaf != "dlss-swapper-lle" && leaf != "Linux")
            || PathSafety.IsSymbolicLink(fullPath))
        {
            throw new InvalidOperationException(
                $"Refusing to reset an unexpected or linked directory: {fullPath}");
        }
    }
}
