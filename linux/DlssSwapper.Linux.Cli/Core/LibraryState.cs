using System.Text.Json;
using System.Text.Json.Serialization;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record PerformanceLimits(
    int ScanConcurrency,
    int ArtworkConcurrency)
{
    public static PerformanceLimits Standard { get; } = new(15, 38);

    public static PerformanceLimits Hdd { get; } = new(2, 1);
}

public sealed class ManualGameState
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public ManualGameLaunch? Launch { get; set; }
    public string Name { get; set; } = string.Empty;

    public string RootPath { get; set; } = string.Empty;

    public string? SteamAppId { get; set; }

    public string? ArtworkPath { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GamePreferenceState
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public bool IsHidden { get; set; }

    public string? Notes { get; set; }

    public string? CustomArtworkPath { get; set; }
}

public sealed class GameHistoryState
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public DateTimeOffset EventTimeUtc { get; set; } = DateTimeOffset.UtcNow;

    public string EventType { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;
}

public sealed partial class LinuxLibraryState
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public const int CurrentSchemaVersion = 2;
    public const string DefaultMediaWikiApiEndpoint = "https://en.wikipedia.org/w/api.php";
    public const string DefaultMediaWikiImageHost = "upload.wikimedia.org";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public ProxySettings? Proxy { get; set; }
    public string Language { get; set; } = "en-US";
    public SavedWindowPlacement? WindowPlacement { get; set; }
    public string? LastLaunchVersion { get; set; }
    public string ApplicationLoggingLevel { get; set; } = "Error";

    public List<ManualGameState> ManualGames { get; set; } = [];

    public List<DllCatalogEntry> ImportedDlls { get; set; } = [];

    public List<string> ExcludedSteamAppIds { get; set; } = [];
    public List<ProviderGameIdentity> ExcludedProviderGames { get; set; } = [];

    public List<string> AdditionalSteamRoots { get; set; } = [];
    public List<string> ProviderWinePrefixes { get; set; } = [];
    public Dictionary<string, string> ProviderWineRunners { get; set; } = new(StringComparer.Ordinal);
    public List<string> LegendaryConfigDirectories { get; set; } = [];
    public List<string> HeroicConfigDirectories { get; set; } = [];
    public string? HeroicExecutable { get; set; }

    public List<string> CustomScanPatterns { get; set; } = [];
    public List<string> IgnoredPaths { get; set; } = [];

    public List<GamePreferenceState> GamePreferences { get; set; } = [];

    public List<GameHistoryState> GameHistory { get; set; } = [];
    public DiscoverySnapshot? DiscoverySnapshot { get; set; }

    public bool HasCompletedInitialDeepScan { get; set; }

    public bool HasSelectedStorageProfile { get; set; }

    public bool HddMode { get; set; }
    public string? AppTheme { get; set; }
    public int? ScanConcurrency { get; set; }
    public int? ArtworkConcurrency { get; set; }
    public int BatchSwapConcurrency { get; set; } = 15;
    private int _uiCollectionBatchSize = 550;
    public int UiCollectionBatchSize
    {
        get => _uiCollectionBatchSize;
        set => _uiCollectionBatchSize = Math.Clamp(value, 10, 1000);
    }

    public bool AllowDebugDlls { get; set; }

    public bool AllowUntrustedDlls { get; set; }
    public bool OnlyShowDownloadedDlls { get; set; }

    public int CardSize { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int GridColumns { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int GridRows { get; set; }

    public bool GridView { get; set; } = true;
    public bool HideNonSwappableGames { get; set; } = true;
    public bool GroupGameLibrariesTogether { get; set; } = true;
    public bool ShowHiddenGames { get; set; }
    public int GameSortMode { get; set; }
    public List<LibrarySelectionEntry> LibrarySelection { get; set; } = [];

    public bool SuppressSingleFolderNotice { get; set; }

    public bool SuppressMultipleFoldersNotice { get; set; }

    public bool SuppressMultiGameDirectoryNotice { get; set; }

    public bool DontShowManualLaunchPrompt { get; set; }

    public bool SetupManualLaunchOnImport { get; set; }

    public string MediaWikiApiEndpoint { get; set; } = DefaultMediaWikiApiEndpoint;

    public string MediaWikiImageHost { get; set; } = DefaultMediaWikiImageHost;

    [JsonIgnore]
    public PerformanceLimits Performance => new(
        Math.Clamp(ScanConcurrency ?? (HddMode ? PerformanceLimits.Hdd : PerformanceLimits.Standard).ScanConcurrency, 1, 26),
        Math.Clamp(ArtworkConcurrency ?? (HddMode ? PerformanceLimits.Hdd : PerformanceLimits.Standard).ArtworkConcurrency, 1, 64));

    public void ApplyStorageProfile(bool hddMode)
    {
        HddMode = hddMode;
        ScanConcurrency = null;
        ArtworkConcurrency = null;
        BatchSwapConcurrency = 15;
        UiCollectionBatchSize = 550;
        HasSelectedStorageProfile = true;
    }
}

public sealed class LibraryStateStore
{
    private byte[]? _loadedBytes;
    private bool _loadFailed;
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
        _loadFailed = true;
        if (!File.Exists(StatePath))
        {
            _loadedBytes = null;
            _loadFailed = false;
            return Normalize(new LinuxLibraryState());
        }

        try
        {
            var bytes = File.ReadAllBytes(StatePath);
            var state = JsonSerializer.Deserialize<LinuxLibraryState>(bytes, SerializerOptions)
                ?? throw new InvalidDataException("The Linux library state is empty.");
            var normalized = Normalize(state);
            _loadedBytes = bytes;
            _loadFailed = false;
            return normalized;
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
        if (_loadFailed)
        {
            throw new InvalidOperationException("Saved library data could not be read. It has not been overwritten.");
        }
        using var coordination = AcquireCoordinationLock();
        Directory.CreateDirectory(StateDirectory);

        // Keep the lock file: deleting it can let two processes lock different files.
        // The lock serializes this application's check-and-replace, not external editors.
        using var writeLock = AcquireWriteLock();
        var currentBytes = File.Exists(StatePath) ? File.ReadAllBytes(StatePath) : null;
        if ((_loadedBytes is null) != (currentBytes is null)
            || (_loadedBytes is not null && !currentBytes!.AsSpan().SequenceEqual(_loadedBytes)))
        {
            throw new IOException("Saved library data changed in another instance. Reopen the app or retry the CLI command before saving; no changes were overwritten.");
        }

        var temporaryPath = Path.Combine(
            StateDirectory,
            $".{Path.GetFileName(StatePath)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalized, SerializerOptions);
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
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    temporaryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(temporaryPath, StatePath, overwrite: true);
            _loadedBytes = bytes;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal FileStream AcquireWriteLock()
    {
        try
        {
            return new FileStream(Path.Combine(StateDirectory, ".state.write.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException error)
        {
            throw new IOException("Another instance may be saving library data. Retry after it finishes. No changes were overwritten.", error);
        }
    }

    // Unlike a lock file inside the state directory, this survives a directory reset.
    internal IDisposable AcquireCoordinationLock(int millisecondsTimeout = 0)
    {
        var identity = Path.TrimEndingDirectorySeparator(StateDirectory);
        if (OperatingSystem.IsWindows()) identity = identity.ToUpperInvariant();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(identity)));
        var mutex = new Mutex(false, "LLE.LibraryState." + hash);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(millisecondsTimeout); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
                throw new IOException("Another instance is saving or resetting library data. Retry after it finishes.");
            return new CoordinationLease(mutex);
        }
        catch
        {
            if (acquired) mutex.ReleaseMutex();
            mutex.Dispose();
            throw;
        }
    }

    private sealed class CoordinationLease(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
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
        state.IgnoredPaths = GameViewPolicy.NormalizeIgnoredPaths(state.IgnoredPaths ?? []);
        state.GamePreferences ??= [];
        state.GameHistory ??= [];

        ValidateSavedRecords(state.ManualGames, item => item.RootPath, "manual game", true);
        _ = LibrarySelection.Read(state);
        ValidateSavedRecords(state.GamePreferences, item => item.RootPath, "game preference", true);
        ValidateSavedRecords(state.GameHistory, item => item.RootPath, "history", false);
        if (state.GameHistory.Any(item => string.IsNullOrWhiteSpace(item.EventType)))
            throw new InvalidDataException("Saved history contains an entry without an event type. The saved file has not been changed.");

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
        state.ProviderWinePrefixes = NormalizePaths(state.ProviderWinePrefixes ?? []);
        state.ProviderWineRunners ??= new(StringComparer.Ordinal);
        state.LegendaryConfigDirectories = NormalizePaths(state.LegendaryConfigDirectories ?? []);
        state.HeroicConfigDirectories = NormalizePaths(state.HeroicConfigDirectories ?? []);
        state.CustomScanPatterns = FastScanPatternIndex.NormalizeCustomPatterns(
            state.CustomScanPatterns).ToList();
        var preferencePaths = new HashSet<string>(PathComparers.FileSystemPath);
        state.GamePreferences = state.GamePreferences
            .Where(preference => preference is not null
                && !string.IsNullOrWhiteSpace(preference.RootPath))
            .Select(preference =>
            {
                preference.RootPath = NormalizeStatePath(preference.RootPath);
                // Notes are user text: retain whitespace, empty strings and line endings.
                preference.CustomArtworkPath = NormalizeOptionalPath(
                    preference.CustomArtworkPath);
                return preference;
            })
            .Where(preference => preference.IsFavorite
                || preference.IsHidden
                || preference.Notes is not null
                || preference.CustomArtworkPath is not null
                || preference.AdditionalData?.Count > 0)
            .Where(preference => preferencePaths.Add(preference.RootPath))
            .OrderBy(preference => preference.RootPath, PathComparers.FileSystemPath)
            .ToList();
        state.GameHistory = state.GameHistory
            .Where(history => history is not null
                && !string.IsNullOrWhiteSpace(history.RootPath)
                && !string.IsNullOrWhiteSpace(history.EventType))
            .Select(history =>
            {
                history.RootPath = NormalizeStatePath(history.RootPath);
                history.EventType = history.EventType.Trim();
                history.AssetType = history.AssetType?.Trim() ?? string.Empty;
                history.Version = history.Version?.Trim() ?? string.Empty;
                history.Detail ??= string.Empty;
                return history;
            })
            .OrderByDescending(history => history.EventTimeUtc)
            // Saving one game must not silently discard another game's history.
            .ToList();
        if (state.CardSize is < ResponsiveGridLayout.MinimumCardSize
            or > ResponsiveGridLayout.MaximumCardSize)
        {
            state.CardSize = ResponsiveGridLayout.ConvertLegacyColumnsToCardSize(
                state.GridColumns);
        }
        state.GridColumns = 0;
        state.GridRows = 0;
        state.MediaWikiApiEndpoint = NormalizeMediaWikiEndpoint(state.MediaWikiApiEndpoint);
        state.MediaWikiImageHost = NormalizeImageHost(state.MediaWikiImageHost);
        return state;
    }

    private static void ValidateSavedRecords<T>(IEnumerable<T> records, Func<T, string?> getRoot, string label, bool uniqueRoot)
        where T : class
    {
        var byRoot = new Dictionary<string, T>(PathComparers.FileSystemPath);
        foreach (var item in records)
        {
            if (item is null || string.IsNullOrWhiteSpace(getRoot(item)))
                throw new InvalidDataException($"Saved {label} data contains an entry without a game path. The saved file has not been changed.");
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(getRoot(item)!.Trim()));
            if (!uniqueRoot) continue;
            if (byRoot.TryGetValue(root, out var prior)
                && JsonSerializer.Serialize(prior, SerializerOptions) != JsonSerializer.Serialize(item, SerializerOptions))
                throw new InvalidDataException($"Saved {label} data contains conflicting entries for '{root}'. The saved file has not been changed.");
            byRoot.TryAdd(root, item);
        }
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

    private static string NormalizeStatePath(string value) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(value.Trim()));

    private static string? NormalizeOptionalPath(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Path.GetFullPath(value.Trim());

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
    private readonly object _stateLock = new();
    private byte[] _committedState;

    public PersistentLibrary(LibraryStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        State = store.Load();
        _committedState = JsonSerializer.SerializeToUtf8Bytes(State);
    }

    public LinuxLibraryState State { get; private set; }

    public string StateDirectory => _store.StateDirectory;

    public TResult UpdateState<TResult>(Func<LinuxLibraryState, TResult> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (_stateLock)
        {
            try
            {
                var result = update(State);
                Save();
                return result;
            }
            catch
            {
                RestoreCommittedState();
                throw;
            }
        }
    }

    public void UpdateState(Action<LinuxLibraryState> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        UpdateState(state =>
        {
            update(state);
            return true;
        });
    }

    public GamePreferenceState UpdateGamePreference(
        string rootPath,
        Action<GamePreferenceState> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return UpdateState(state =>
        {
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            var preference = state.GamePreferences.FirstOrDefault(item =>
                PathComparers.FileSystemPath.Equals(item.RootPath, normalized));
            if (preference is null)
            {
                preference = new GamePreferenceState { RootPath = normalized };
                state.GamePreferences.Add(preference);
            }

            update(preference);
            return preference;
        });
    }

    public IReadOnlyList<SelectedGame> Merge(SteamDiscoveryResult discovery) => Merge(discovery, []);

    public IReadOnlyList<SelectedGame> Merge(SteamDiscoveryResult discovery, IReadOnlyList<ProviderGame> providerGames)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        lock (_stateLock)
        {
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

            var providerExclusions = (State.ExcludedProviderGames ?? []).ToHashSet();
            // Expand old app-ID exclusions through catalog aliases, regardless of source order.
            bool expanded;
            do
            {
                expanded = false;
                foreach (var providerGame in providerGames)
                {
                    var identities = providerGame.IdentityAliases.Append(providerGame.Identity).ToArray();
                    if (identities.Any(providerExclusions.Contains))
                        foreach (var identity in identities) expanded |= providerExclusions.Add(identity);
                }
            } while (expanded);
            foreach (var providerGame in providerGames)
            {
                if (!Enum.IsDefined(providerGame.Identity.Provider) || string.IsNullOrWhiteSpace(providerGame.Identity.Id)
                    || !Path.IsPathFullyQualified(providerGame.InstallDirectory))
                    throw new InvalidDataException("Discovered provider game has invalid identity or installation path.");
                if (providerExclusions.Contains(providerGame.Identity)) continue;
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(providerGame.InstallDirectory));
                if (games.TryGetValue(root, out var existing))
                {
                    // Steam/manual ownership remains unchanged; retain every provider route for provider-owned rows.
                    if (existing.ProviderIdentity is null) continue;
                    var choices = existing.ProviderLaunchChoices.Concat(providerGame.Launch is { } launch ? [launch] : [])
                        .Distinct().ToArray();
                    games[root] = existing with
                    {
                        ProviderLaunch = choices.FirstOrDefault(),
                        BattleNet = existing.BattleNet ?? providerGame.BattleNet,
                        CoverUrl = existing.CoverUrl ?? providerGame.CoverUrl,
                        LocalIconPath = existing.LocalIconPath ?? providerGame.LocalIconPath,
                        LocalIconIndex = existing.LocalIconPath is not null ? existing.LocalIconIndex : providerGame.LocalIconIndex,
                        ProviderLaunchChoices = choices,
                        ProviderIdentityAliases = existing.ProviderIdentityAliases.Concat(providerGame.IdentityAliases)
                            .Append(providerGame.Identity).Append(existing.ProviderIdentity).Distinct().ToArray(),
                    };
                    continue;
                }
                games.Add(root, new SelectedGame(providerGame.Name, root, null)
                { ProviderIdentity = providerGame.Identity, WinePrefix = providerGame.WinePrefix, ProviderLaunch = providerGame.Launch, BattleNet = providerGame.BattleNet,
                    ProviderLaunchChoices = providerGame.Launch is { } initialLaunch ? [initialLaunch] : [],
                    ProviderIdentityAliases = providerGame.IdentityAliases, LocalIconPath = providerGame.LocalIconPath, CoverUrl = providerGame.CoverUrl,
                    LocalIconIndex = providerGame.LocalIconIndex });
            }

            return games.Values
                .Where(game => !GameViewPolicy.IsIgnored(State, game.RootPath))
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.RootPath, PathComparers.FileSystemPath)
                .ToArray();
        }
    }

    public int AddManualGames(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        lock (_stateLock)
        {
            var existing = State.ManualGames
                .Select(game => game.RootPath)
                .ToHashSet(PathComparers.FileSystemPath);
            var added = 0;
            // Finish validating and enumerating before changing the library.
            foreach (var path in paths.Select(ValidateGameDirectory).ToArray())
            {
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
        lock (_stateLock)
        {
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            var removed = State.ManualGames.RemoveAll(game =>
                PathComparers.FileSystemPath.Equals(game.RootPath, normalized)) > 0;
            SaveWhenChanged(removed);
            return removed;
        }
    }

    public bool ExcludeSteamGame(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        lock (_stateLock)
        {
            if (State.ExcludedSteamAppIds.Contains(appId, StringComparer.Ordinal))
            {
                return false;
            }

            State.ExcludedSteamAppIds.Add(appId.Trim());
            SaveWhenChanged(changed: true);
            return true;
        }
    }

    public int RestoreSteamGames()
    {
        lock (_stateLock)
        {
            var restored = State.ExcludedSteamAppIds.Count;
            State.ExcludedSteamAppIds.Clear();
            SaveWhenChanged(restored > 0);
            return restored;
        }
    }

    public GamePreferenceState? FindGamePreference(string rootPath)
    {
        lock (_stateLock)
        {
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            return State.GamePreferences.FirstOrDefault(item =>
                PathComparers.FileSystemPath.Equals(item.RootPath, normalized));
        }
    }

    public IReadOnlyList<GameHistoryState> GetGameHistory(string rootPath)
    {
        lock (_stateLock)
        {
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            return State.GameHistory
                .Where(item => PathComparers.FileSystemPath.Equals(item.RootPath, normalized))
                .OrderByDescending(item => item.EventTimeUtc)
                .ToArray();
        }
    }

    public void RecordHistory(
        string rootPath,
        string eventType,
        string assetType = "",
        string version = "",
        string detail = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(assetType);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(detail);
        lock (_stateLock)
        {
            State.GameHistory.Add(new GameHistoryState
            {
                RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)),
                EventTimeUtc = DateTimeOffset.UtcNow,
                EventType = eventType.Trim(),
                AssetType = assetType.Trim(),
                Version = version.Trim(),
                Detail = detail,
            });
            Save();
        }
    }

    public void RecordOperationHistory(IReadOnlyList<OperationResult> results, string eventType)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (results.Count == 0) return;
        var entries = results.Select(result => new GameHistoryState
        {
            RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(result.Game.RootPath)),
            EventTimeUtc = DateTimeOffset.UtcNow,
            EventType = eventType.Trim(),
            AssetType = result.Family.Trim(),
            Version = result.Message.Trim(),
            Detail = result.Target,
        }).ToArray();
        UpdateState(state => state.GameHistory.AddRange(entries));
    }

    public void RemoveGameState(string rootPath)
    {
        lock (_stateLock)
        {
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            var changed = State.GamePreferences.RemoveAll(item =>
                    PathComparers.FileSystemPath.Equals(item.RootPath, normalized)) > 0;
            changed |= State.GameHistory.RemoveAll(item =>
                    PathComparers.FileSystemPath.Equals(item.RootPath, normalized)) > 0;
            SaveWhenChanged(changed);
        }
    }

    public void Save()
    {
        lock (_stateLock)
        {
            try
            {
                var candidate = JsonSerializer.Deserialize<LinuxLibraryState>(
                    JsonSerializer.SerializeToUtf8Bytes(State))!;
                LibraryStateStore.Normalize(candidate);
                var committed = JsonSerializer.SerializeToUtf8Bytes(candidate);
                _store.Save(candidate);
                State = candidate;
                _committedState = committed;
            }
            catch
            {
                RestoreCommittedState();
                throw;
            }
        }
    }

    private void RestoreCommittedState() =>
        State = JsonSerializer.Deserialize<LinuxLibraryState>(_committedState)!;

    public void ResetLocalData(string? cacheDirectory = null)
    {
        lock (_stateLock)
        {
            new LocalDataResetService(_store, cacheDirectory).Reset();
            State = _store.Load();
            _committedState = JsonSerializer.SerializeToUtf8Bytes(State);
        }
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
