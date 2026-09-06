namespace DlssSwapper.Linux.Cli.Core;

public sealed record LibraryStartupResult(DllCatalog? Catalog, PersistentLibrary? Library, string? Error)
{
    public bool Succeeded => Catalog is not null && Library is not null && Error is null;
}

public static class LibraryStartup
{
    public static async Task<DllCatalog?> RefreshForVersionAsync(HttpClient http, PersistentLibrary library,
        string version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (library.State.LastLaunchVersion == version) return null;
        var path = Path.Combine(library.StateDirectory, "manifest.json");
        // A fresh installation already loaded its bundled catalog. Existing installations
        // refresh from the catalog source, never by comparing embedded/cached byte lengths.
        var catalog = File.Exists(path)
            ? await DllCatalogRefresh.FetchAsync(http, path, cancellationToken).ConfigureAwait(false)
            : null;
        cancellationToken.ThrowIfCancellationRequested();
        library.UpdateState(state => state.LastLaunchVersion = version);
        return catalog;
    }

    // Loading must not create defaults on disk or replace unreadable user data.
    public static LibraryStartupResult Load(string catalogPath, LibraryStateStore stateStore, string? fallbackCatalogPath = null)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        DllCatalog catalog;
        try { catalog = DllCatalog.Load(catalogPath); }
        catch (Exception error)
        {
            if (fallbackCatalogPath is null)
                return new(null, null,
                    $"The DLL catalog could not be loaded. Check this file and then retry:\n{catalogPath}\n\n{error.Message}");
            try { catalog = DllCatalog.Load(fallbackCatalogPath); }
            catch (Exception fallbackError)
            {
                return new(null, null,
                    $"Neither DLL catalog could be loaded. No saved data was replaced. Check these files and retry:\n{catalogPath}: {error.Message}\n{fallbackCatalogPath}: {fallbackError.Message}");
            }
        }
        try { return new(catalog, new PersistentLibrary(stateStore), null); }
        catch (Exception error)
        {
            return new(catalog, null,
                $"Your saved library could not be loaded. No saved data was replaced. Check the file or restore a known-good backup, then retry:\n{stateStore.StatePath}\n\n{error.Message}");
        }
    }
}
