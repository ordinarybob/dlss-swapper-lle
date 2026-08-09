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

        Directory.Delete(fullPath, recursive: true);
    }
}
