namespace DlssSwapper.Linux.Cli.Platform;

public sealed record SteamGame(
    string AppId,
    string Name,
    string InstallDirectory,
    string LibraryRoot,
    string ManifestPath);

public sealed record SteamDiscoveryResult(
    IReadOnlyList<SteamGame> Games,
    IReadOnlyList<string> Warnings);

public sealed record SteamDiscoveryOptions
{
    public IReadOnlyList<string> AdditionalRoots { get; init; } = [];
    public bool IncludeDefaultRoots { get; init; } = true;
    public string? HomeDirectory { get; init; }
    public string? XdgDataHome { get; init; }
}

public sealed class SteamDiscovery
{
    private const long MaximumMetadataBytes = 4L * 1024 * 1024;

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public SteamDiscoveryResult Discover(IReadOnlyList<string> additionalRoots)
    {
        return Discover(new SteamDiscoveryOptions
        {
            AdditionalRoots = additionalRoots,
        });
    }

    public SteamDiscoveryResult Discover(SteamDiscoveryOptions options)
    {
        var warnings = new List<string>();
        var steamRoots = new HashSet<string>(PathComparer);
        var rootCandidates = options.IncludeDefaultRoots
            ? GetDefaultRoots(options).Concat(options.AdditionalRoots)
            : options.AdditionalRoots;
        foreach (var candidate in rootCandidates)
        {
            TryAddSteamRoot(candidate, steamRoots, warnings);
        }

        var libraries = new HashSet<string>(PathComparer);
        foreach (var steamRoot in steamRoots)
        {
            TryAddLibrary(steamRoot, libraries, warnings);
            AddConfiguredLibraries(steamRoot, libraries, warnings);
        }

        var games = new List<SteamGame>();
        var gamePaths = new HashSet<string>(PathComparer);
        foreach (var library in libraries)
        {
            AddGames(library, games, gamePaths, warnings);
        }

        games.Sort((left, right) =>
        {
            var byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return byName != 0 ? byName : StringComparer.Ordinal.Compare(left.AppId, right.AppId);
        });
        return new SteamDiscoveryResult(games, warnings);
    }

    private static IEnumerable<string> GetDefaultRoots(SteamDiscoveryOptions options)
    {
        var home = options.HomeDirectory;
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            yield break;
        }

        var xdgDataHome = options.XdgDataHome
            ?? Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(xdgDataHome) || !Path.IsPathRooted(xdgDataHome))
        {
            xdgDataHome = Path.Combine(home, ".local", "share");
        }

        yield return Path.Combine(xdgDataHome, "Steam");
        yield return Path.Combine(xdgDataHome, "steam");
        yield return Path.Combine(home, ".steam", "root");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".steam", "debian-installation");
        yield return Path.Combine(
            home,
            ".var",
            "app",
            "com.valvesoftware.Steam",
            ".local",
            "share",
            "Steam");
        yield return Path.Combine(
            home,
            ".var",
            "app",
            "com.valvesoftware.Steam",
            "data",
            "Steam");
    }

    private static void TryAddSteamRoot(
        string candidate,
        HashSet<string> roots,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        try
        {
            var normalized = NormalizeRootInput(candidate);
            if (Directory.Exists(Path.Combine(normalized, "steamapps")))
            {
                roots.Add(CanonicalizeDirectory(normalized));
            }
            else if (Directory.Exists(candidate))
            {
                warnings.Add($"Steam root has no steamapps directory: {candidate}");
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            warnings.Add($"Could not inspect Steam root '{candidate}': {exception.Message}");
        }
    }

    private static string NormalizeRootInput(string candidate)
    {
        var expanded = candidate.Trim();
        if (expanded == "~" || expanded.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = expanded.Length == 1
                ? home
                : Path.Combine(home, expanded[2..]);
        }

        var fullPath = Path.GetFullPath(expanded);
        if (File.Exists(fullPath)
            && string.Equals(
                Path.GetFileName(fullPath),
                "libraryfolders.vdf",
                StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(fullPath)?.Parent?.FullName ?? fullPath;
        }

        if (string.Equals(
                Path.GetFileName(fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)),
                "steamapps",
                StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(fullPath)?.FullName ?? fullPath;
        }

        return fullPath;
    }

    private static string CanonicalizeDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = new DirectoryInfo(fullPath);
        var resolved = directory.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo
            ?? directory;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolved.FullName));
    }

    private static void TryAddLibrary(
        string libraryRoot,
        HashSet<string> libraries,
        List<string> warnings)
    {
        try
        {
            var canonical = CanonicalizeDirectory(libraryRoot);
            if (Directory.Exists(Path.Combine(canonical, "steamapps")))
            {
                libraries.Add(canonical);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            warnings.Add($"Could not inspect Steam library '{libraryRoot}': {exception.Message}");
        }
    }

    private static void AddConfiguredLibraries(
        string steamRoot,
        HashSet<string> libraries,
        List<string> warnings)
    {
        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
        {
            return;
        }

        try
        {
            var root = ReadKeyValues(vdfPath);
            var libraryFolders = root.GetObject("libraryfolders");
            if (libraryFolders is null)
            {
                warnings.Add($"Steam library list has no libraryfolders object: {vdfPath}");
                return;
            }

            foreach (var entry in libraryFolders.Entries)
            {
                var path = entry.Children?.GetString("path") ?? entry.Value;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    TryAddLibrary(path, libraries, warnings);
                }
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or FormatException)
        {
            warnings.Add($"Could not read Steam library list '{vdfPath}': {exception.Message}");
        }
    }

    private static void AddGames(
        string libraryRoot,
        List<SteamGame> games,
        HashSet<string> gamePaths,
        List<string> warnings)
    {
        var steamApps = Path.Combine(libraryRoot, "steamapps");
        string[] manifests;
        try
        {
            manifests = Directory.EnumerateFiles(
                steamApps,
                "appmanifest_*.acf",
                SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not enumerate Steam manifests in '{steamApps}': {exception.Message}");
            return;
        }

        foreach (var manifest in manifests)
        {
            try
            {
                var root = ReadKeyValues(manifest);
                var appState = root.GetObject("AppState");
                var appId = appState?.GetString("appid");
                var name = appState?.GetString("name");
                var installDirName = appState?.GetString("installdir");
                var stateFlags = appState?.GetString("StateFlags");
                if (string.IsNullOrWhiteSpace(appId)
                    || string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(installDirName))
                {
                    warnings.Add($"Steam manifest is missing appid, name, or installdir: {manifest}");
                    continue;
                }

                var manifestAppId = Path.GetFileNameWithoutExtension(manifest)
                    .Replace("appmanifest_", string.Empty, StringComparison.OrdinalIgnoreCase);
                if (!ulong.TryParse(appId, out _)
                    || !appId.Equals(manifestAppId, StringComparison.Ordinal))
                {
                    warnings.Add($"Steam manifest app ID does not match its filename: {manifest}");
                    continue;
                }

                if (ulong.TryParse(stateFlags, out var parsedStateFlags)
                    && (parsedStateFlags & 4UL) == 0)
                {
                    continue;
                }

                var commonRoot = CanonicalizeDirectory(Path.Combine(steamApps, "common"));
                var requestedInstallDirectory = Path.GetFullPath(
                    Path.Combine(commonRoot, installDirName));
                if (!TryCanonicalizeUnderRoot(
                        commonRoot,
                        requestedInstallDirectory,
                        out var installDirectory))
                {
                    warnings.Add($"Steam install directory escapes its library: {manifest}");
                    continue;
                }

                if (!Directory.Exists(installDirectory))
                {
                    continue;
                }

                if (gamePaths.Add(installDirectory))
                {
                    games.Add(new SteamGame(
                        appId,
                        name,
                        installDirectory,
                        libraryRoot,
                        manifest));
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or FormatException
                or ArgumentException)
            {
                warnings.Add($"Could not read Steam manifest '{manifest}': {exception.Message}");
            }
        }
    }

    private static ValveKeyValueObject ReadKeyValues(string path)
    {
        var length = new FileInfo(path).Length;
        if (length <= 0 || length > MaximumMetadataBytes)
        {
            throw new InvalidDataException("Steam metadata file has an unexpected size.");
        }

        return ValveKeyValuesParser.Parse(File.ReadAllText(path));
    }

    private static bool IsUnderRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return !Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
    }

    private static bool TryCanonicalizeUnderRoot(
        string root,
        string candidate,
        out string canonicalPath)
    {
        canonicalPath = string.Empty;
        if (!IsUnderRoot(root, candidate))
        {
            return false;
        }

        var current = root;
        var relative = Path.GetRelativePath(root, candidate);
        foreach (var component in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = new DirectoryInfo(Path.Combine(current, component));
            current = Path.GetFullPath(
                (directory.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo
                 ?? directory).FullName);
            if (!IsUnderRoot(root, current))
            {
                return false;
            }
        }

        if (current.Equals(root, StringComparison.Ordinal))
        {
            return false;
        }

        canonicalPath = current;
        return true;
    }
}
