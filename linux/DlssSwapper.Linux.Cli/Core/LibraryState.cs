using System.Text.Json;
using System.Text.Json.Serialization;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record PerformanceLimits(
    int ScanConcurrency,
    int ArtworkConcurrency,
    int UiDelayMilliseconds,
    int DatabaseDelayMilliseconds,
    int UpdateConcurrency)
{
    public static PerformanceLimits Standard { get; } = new(15, 38, 550, 550, 15);

    public static PerformanceLimits Hdd { get; } = new(2, 1, 550, 550, 15);
}

public sealed class ManualGameState
{
    public string Name { get; set; } = string.Empty;

    public string RootPath { get; set; } = string.Empty;

    public string? SteamAppId { get; set; }

    public string? ArtworkPath { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LinuxLibraryState
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultMediaWikiApiEndpoint = "https://en.wikipedia.org/w/api.php";
    public const string DefaultMediaWikiImageHost = "upload.wikimedia.org";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<ManualGameState> ManualGames { get; set; } = [];

    public List<string> ExcludedSteamAppIds { get; set; } = [];

    public List<string> AdditionalSteamRoots { get; set; } = [];

    public List<string> CustomScanPatterns { get; set; } = [];

    public bool HasCompletedInitialDeepScan { get; set; }

    public bool HddMode { get; set; }

    public int GridColumns { get; set; } = 6;

    public int GridRows { get; set; } = 5;

    public bool GridView { get; set; } = true;

    public bool SuppressSingleFolderNotice { get; set; }

    public bool SuppressMultipleFoldersNotice { get; set; }

    public bool SuppressMultiGameDirectoryNotice { get; set; }

    public string MediaWikiApiEndpoint { get; set; } = DefaultMediaWikiApiEndpoint;

    public string MediaWikiImageHost { get; set; } = DefaultMediaWikiImageHost;

    [JsonIgnore]
    public PerformanceLimits Performance => HddMode
        ? PerformanceLimits.Hdd
        : PerformanceLimits.Standard;
}

public sealed class LibraryStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public LibraryStateStore(string? stateDirectory = null)
    {
        StateDirectory = Path.GetFullPath(stateDirectory ?? GetDefaultStateDirectory());
        StatePath = Path.Combine(StateDirectory, "state.json");
    }

    public string StateDirectory { get; }

    public string StatePath { get; }

    public LinuxLibraryState Load()
    {
        if (!File.Exists(StatePath))
        {
            return Normalize(new LinuxLibraryState());
        }

        try
        {
            using var stream = new FileStream(
                StatePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            var state = JsonSerializer.Deserialize<LinuxLibraryState>(stream, SerializerOptions)
                ?? throw new InvalidDataException("The Linux library state is empty.");
            return Normalize(state);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The Linux library state is invalid JSON: {StatePath}",
                exception);
        }
    }

    public void Save(LinuxLibraryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var normalized = Normalize(state);
        Directory.CreateDirectory(StateDirectory);

        var temporaryPath = Path.Combine(
            StateDirectory,
            $".{Path.GetFileName(StatePath)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, normalized, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    temporaryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(temporaryPath, StatePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static LinuxLibraryState Normalize(LinuxLibraryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion is < 1 or > LinuxLibraryState.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported Linux library state schema {state.SchemaVersion}.");
        }

        state.SchemaVersion = LinuxLibraryState.CurrentSchemaVersion;
        state.ManualGames ??= [];
        state.ExcludedSteamAppIds ??= [];
        state.AdditionalSteamRoots ??= [];
        state.CustomScanPatterns ??= [];

        var manualPaths = new HashSet<string>(PathComparers.FileSystemPath);
        state.ManualGames = state.ManualGames
            .Where(game => game is not null && !string.IsNullOrWhiteSpace(game.RootPath))
            .Select(game =>
            {
                game.RootPath = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(game.RootPath.Trim()));
                game.Name = string.IsNullOrWhiteSpace(game.Name)
                    ? Path.GetFileName(game.RootPath)
                    : game.Name.Trim();
                game.SteamAppId = NormalizeOptional(game.SteamAppId);
                game.ArtworkPath = NormalizeOptional(game.ArtworkPath);
                return game;
            })
            .Where(game => manualPaths.Add(game.RootPath))
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(game => game.RootPath, PathComparers.FileSystemPath)
            .ToList();

        state.ExcludedSteamAppIds = NormalizeStrings(
            state.ExcludedSteamAppIds,
            StringComparer.Ordinal);
        state.AdditionalSteamRoots = NormalizePaths(state.AdditionalSteamRoots);
        state.CustomScanPatterns = FastScanPatternIndex.NormalizeCustomPatterns(
            state.CustomScanPatterns).ToList();
        state.GridColumns = Math.Clamp(state.GridColumns, 1, 24);
        state.GridRows = Math.Clamp(state.GridRows, 1, 24);
        state.MediaWikiApiEndpoint = NormalizeMediaWikiEndpoint(state.MediaWikiApiEndpoint);
        state.MediaWikiImageHost = NormalizeImageHost(state.MediaWikiImageHost);
        return state;
    }

    public static string GetDefaultStateDirectory()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome) && Path.IsPathRooted(xdgConfigHome))
        {
            return Path.Combine(xdgConfigHome, "dlss-swapper-lle");
        }

        if (!OperatingSystem.IsWindows())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                home = Environment.GetEnvironmentVariable("HOME");
            }

            if (!string.IsNullOrWhiteSpace(home))
            {
                return Path.Combine(home, ".config", "dlss-swapper-lle");
            }
        }

        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            throw new InvalidOperationException("Could not locate the user configuration directory.");
        }

        return Path.Combine(applicationData, "DLSS Swapper LLE", "Linux");
    }

    public static bool TryValidateArtworkSource(
        string? apiValue,
        string? hostValue,
        out string apiEndpoint,
        out string imageHost,
        out string error)
    {
        apiEndpoint = string.Empty;
        imageHost = string.Empty;
        error = string.Empty;
        if (!Uri.TryCreate(apiValue?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            error = "Enter an absolute HTTPS MediaWiki API URL without credentials, a query, or a fragment.";
            return false;
        }

        var host = hostValue?.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(host)
            || Uri.CheckHostName(host) != UriHostNameType.Dns)
        {
            error = "Enter one DNS host name for cover images, without a scheme or path.";
            return false;
        }

        apiEndpoint = uri.AbsoluteUri;
        imageHost = host.ToLowerInvariant();
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> NormalizePaths(IEnumerable<string> paths)
    {
        var normalized = new HashSet<string>(PathComparers.FileSystemPath);
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            normalized.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())));
        }

        return normalized.OrderBy(path => path, PathComparers.FileSystemPath).ToList();
    }

    private static List<string> NormalizeStrings(
        IEnumerable<string> values,
        StringComparer comparer) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(comparer)
            .OrderBy(value => value, comparer)
            .ToList();

    private static string NormalizeMediaWikiEndpoint(string? value)
    {
        return TryValidateArtworkSource(
            value,
            LinuxLibraryState.DefaultMediaWikiImageHost,
            out var endpoint,
            out _,
            out _)
            ? endpoint
            : LinuxLibraryState.DefaultMediaWikiApiEndpoint;
    }

    private static string NormalizeImageHost(string? value)
    {
        return TryValidateArtworkSource(
            LinuxLibraryState.DefaultMediaWikiApiEndpoint,
            value,
            out _,
            out var host,
            out _)
            ? host
            : LinuxLibraryState.DefaultMediaWikiImageHost;
    }
}

public sealed class PersistentLibrary
{
    private readonly LibraryStateStore _store;

    public PersistentLibrary(LibraryStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        State = store.Load();
    }

    public LinuxLibraryState State { get; private set; }

    public IReadOnlyList<SelectedGame> Merge(SteamDiscoveryResult discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        var excluded = State.ExcludedSteamAppIds.ToHashSet(StringComparer.Ordinal);
        var games = new Dictionary<string, SelectedGame>(PathComparers.FileSystemPath);
        foreach (var steamGame in discovery.Games)
        {
            if (!excluded.Contains(steamGame.AppId))
            {
                games[steamGame.InstallDirectory] = new SelectedGame(
                    steamGame.Name,
                    steamGame.InstallDirectory,
                    steamGame.AppId);
            }
        }

        foreach (var manualGame in State.ManualGames)
        {
            games.TryAdd(
                manualGame.RootPath,
                new SelectedGame(
                    manualGame.Name,
                    manualGame.RootPath,
                    manualGame.SteamAppId));
        }

        return games.Values
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(game => game.RootPath, PathComparers.FileSystemPath)
            .ToArray();
    }

    public int AddManualGames(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var existing = State.ManualGames
            .Select(game => game.RootPath)
            .ToHashSet(PathComparers.FileSystemPath);
        var added = 0;
        foreach (var input in paths)
        {
            var path = ValidateGameDirectory(input);
            if (!existing.Add(path))
            {
                continue;
            }

            State.ManualGames.Add(new ManualGameState
            {
                Name = Path.GetFileName(path),
                RootPath = path,
            });
            added++;
        }

        SaveWhenChanged(added > 0);
        return added;
    }

    public int AddImmediateChildren(string parentPath)
    {
        var parent = ValidateGameDirectory(parentPath);
        var children = Directory.EnumerateDirectories(parent)
            .Where(path => !PathSafety.IsSymbolicLink(path))
            .OrderBy(path => path, PathComparers.FileSystemPath)
            .ToArray();
        return AddManualGames(children);
    }

    public bool RemoveManualGame(string rootPath)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var removed = State.ManualGames.RemoveAll(game =>
            PathComparers.FileSystemPath.Equals(game.RootPath, normalized)) > 0;
        SaveWhenChanged(removed);
        return removed;
    }

    public bool ExcludeSteamGame(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (State.ExcludedSteamAppIds.Contains(appId, StringComparer.Ordinal))
        {
            return false;
        }

        State.ExcludedSteamAppIds.Add(appId.Trim());
        SaveWhenChanged(changed: true);
        return true;
    }

    public int RestoreSteamGames()
    {
        var restored = State.ExcludedSteamAppIds.Count;
        State.ExcludedSteamAppIds.Clear();
        SaveWhenChanged(restored > 0);
        return restored;
    }

    public void Save()
    {
        _store.Save(State);
        State = _store.Load();
    }

    private void SaveWhenChanged(bool changed)
    {
        if (changed)
        {
            Save();
        }
    }

    private static string ValidateGameDirectory(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input.Trim()));
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Game directory does not exist: {path}");
        }

        if (PathComparers.FileSystemPath.Equals(Path.GetPathRoot(path), path))
        {
            throw new ArgumentException($"Filesystem roots cannot be selected: {path}");
        }

        return path;
    }
}

internal static class PathComparers
{
    internal static StringComparer FileSystemPath { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}

internal static class PathSafety
{
    internal static bool IsSymbolicLink(string path)
    {
        var info = Directory.Exists(path)
            ? (FileSystemInfo)new DirectoryInfo(path)
            : new FileInfo(path);
        return info.LinkTarget is not null
            || (info.Attributes & FileAttributes.ReparsePoint) != 0;
    }
}
