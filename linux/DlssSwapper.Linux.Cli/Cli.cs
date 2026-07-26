using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Cli;

internal sealed class UsageException : Exception
{
    internal UsageException(string message)
        : base(message)
    {
    }
}

internal sealed class CliOptions
{
    internal string Command { get; set; } = "help";
    internal List<string> AppIds { get; } = [];
    internal List<string> Paths { get; } = [];
    internal List<string> Roots { get; } = [];
    internal List<string> SteamRoots { get; } = [];
    internal List<string> Families { get; } = [];
    internal List<string> Versions { get; } = [];
    internal string? ManifestPath { get; set; }
    internal bool All { get; set; }
    internal bool DryRun { get; set; }
    internal bool Yes { get; set; }
    internal bool Help { get; set; }
}

internal static class CliParser
{
    internal static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        if (args.Length == 0)
        {
            options.Help = true;
            return options;
        }

        if (args[0] is "--help" or "-h" or "help")
        {
            options.Help = true;
            return options;
        }

        options.Command = args[0].ToLowerInvariant();
        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--app-id":
                    options.AppIds.Add(ReadValue(args, ref index, argument));
                    break;
                case "--path":
                    options.Paths.Add(ReadValue(args, ref index, argument));
                    break;
                case "--root":
                    options.Roots.Add(ReadValue(args, ref index, argument));
                    break;
                case "--steam-root":
                    options.SteamRoots.Add(ReadValue(args, ref index, argument));
                    break;
                case "--family":
                    options.Families.Add(ReadValue(args, ref index, argument));
                    break;
                case "--version":
                    options.Versions.Add(ReadValue(args, ref index, argument));
                    break;
                case "--manifest":
                    options.ManifestPath = ReadValue(args, ref index, argument);
                    break;
                case "--all":
                    options.All = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--yes":
                    options.Yes = true;
                    break;
                case "--help":
                case "-h":
                    options.Help = true;
                    break;
                default:
                    throw new UsageException($"Unknown option '{argument}'.");
            }
        }

        Validate(options);
        return options;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new UsageException($"Option '{option}' requires a value.");
        }

        return args[index];
    }

    private static void Validate(CliOptions options)
    {
        if (options.Help)
        {
            return;
        }

        if (options.Command is not ("discover" or "scan" or "update" or "restore"))
        {
            throw new UsageException($"Unknown command '{options.Command}'.");
        }

        var hasSelectors = options.AppIds.Count > 0
            || options.Paths.Count > 0
            || options.Roots.Count > 0;
        if (options.All && hasSelectors)
        {
            throw new UsageException("--all cannot be combined with other game selectors.");
        }

        if (options.Command == "discover")
        {
            if (hasSelectors
                || options.All
                || options.DryRun
                || options.Yes
                || options.Families.Count > 0
                || options.Versions.Count > 0
                || options.ManifestPath is not null)
            {
                throw new UsageException(
                    "discover accepts only repeated --steam-root options.");
            }
        }

        if (options.Command == "scan" && options.DryRun)
        {
            throw new UsageException("scan is already read-only; --dry-run is unnecessary.");
        }

        if (options.Command == "scan" && options.Yes)
        {
            throw new UsageException("scan is read-only and does not accept --yes.");
        }

        if (options.Command == "scan"
            && (options.Families.Count > 0 || options.Versions.Count > 0))
        {
            throw new UsageException("scan does not accept --family or --version.");
        }

        if (options.Command == "restore"
            && (options.Versions.Count > 0 || options.ManifestPath is not null))
        {
            throw new UsageException("restore does not accept --version or --manifest.");
        }

        if (options.Command is "update" or "restore"
            && !options.All
            && !hasSelectors)
        {
            throw new UsageException(
                $"{options.Command} requires --app-id, --path, --root, or explicit --all.");
        }

        if (options.Command is "update" or "restore"
            && !options.DryRun
            && !options.Yes)
        {
            throw new UsageException(
                $"{options.Command} without --dry-run requires explicit --yes confirmation.");
        }
    }
}

internal static class GameSelector
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static IReadOnlyList<SelectedGame> Resolve(
        CliOptions options,
        SteamDiscoveryResult discovery)
    {
        var games = new Dictionary<string, SelectedGame>(PathComparer);
        if (options.All
            || (options.Command == "scan"
                && options.AppIds.Count == 0
                && options.Paths.Count == 0
                && options.Roots.Count == 0))
        {
            foreach (var steamGame in discovery.Games)
            {
                Add(games, new SelectedGame(
                    steamGame.Name,
                    steamGame.InstallDirectory,
                    steamGame.AppId));
            }
        }

        foreach (var appId in options.AppIds)
        {
            var matches = discovery.Games
                .Where(game => game.AppId.Equals(appId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                throw new UsageException($"Steam app ID '{appId}' was not discovered.");
            }

            foreach (var match in matches)
            {
                Add(games, new SelectedGame(
                    match.Name,
                    match.InstallDirectory,
                    match.AppId));
            }
        }

        foreach (var path in options.Paths)
        {
            var normalized = NormalizeDirectory(path);
            Add(games, new SelectedGame(
                Path.GetFileName(normalized),
                normalized,
                null));
        }

        foreach (var root in options.Roots)
        {
            var normalizedRoot = NormalizeDirectory(root);
            string[] children;
            try
            {
                children = Directory.EnumerateDirectories(
                    normalizedRoot,
                    "*",
                    SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                throw new UsageException(
                    $"Could not enumerate root '{root}': {exception.Message}");
            }

            foreach (var child in children.OrderBy(path => path, PathComparer))
            {
                var info = new DirectoryInfo(child);
                if (info.LinkTarget is not null
                    || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var normalized = NormalizeDirectory(child);
                Add(games, new SelectedGame(
                    Path.GetFileName(normalized),
                    normalized,
                    null));
            }
        }

        if (games.Count == 0)
        {
            throw new UsageException("No games matched the requested selection.");
        }

        return games.Values
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(game => game.RootPath, PathComparer)
            .ToArray();
    }

    private static void Add(
        Dictionary<string, SelectedGame> games,
        SelectedGame game)
    {
        games.TryAdd(game.RootPath, game);
    }

    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new UsageException("Game paths cannot be empty.");
        }

        var fullPath = Path.GetFullPath(path);
        var fileSystemRoot = Path.GetPathRoot(fullPath);
        if (fileSystemRoot is not null && PathComparer.Equals(fullPath, fileSystemRoot))
        {
            throw new UsageException($"Filesystem roots cannot be selected: {path}");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new UsageException($"Directory does not exist: {path}");
        }

        var directory = new DirectoryInfo(fullPath);
        try
        {
            directory = directory.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo
                ?? directory;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            throw new UsageException($"Could not resolve directory '{path}': {exception.Message}");
        }

        var resolvedPath = directory.FullName.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var resolvedRoot = Path.GetPathRoot(directory.FullName);
        if (resolvedRoot is not null
            && PathComparer.Equals(
                directory.FullName.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                resolvedRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)))
        {
            throw new UsageException($"Filesystem roots cannot be selected: {path}");
        }

        return resolvedPath;
    }
}

internal static class CandidateSelector
{
    internal static IReadOnlyDictionary<DllType, DllCatalogEntry> Resolve(
        CliOptions options,
        DllCatalog catalog)
    {
        var explicitVersions = new Dictionary<DllType, string>();
        foreach (var specification in options.Versions)
        {
            var separator = specification.IndexOf('=');
            if (separator <= 0 || separator == specification.Length - 1)
            {
                throw new UsageException(
                    "--version must use family=version or family=version@MD5-prefix.");
            }

            var type = ParseFamily(specification[..separator]);
            if (!explicitVersions.TryAdd(type, specification[(separator + 1)..]))
            {
                throw new UsageException(
                    $"Multiple versions were supplied for {DllTypes.Get(type).ManifestKey}.");
            }
        }

        var requestedTypes = options.Families.Select(ParseFamily).ToHashSet();
        requestedTypes.UnionWith(explicitVersions.Keys);
        if (requestedTypes.Count == 0)
        {
            requestedTypes.UnionWith(DllTypes.All.Select(definition => definition.Type));
        }

        var candidates = new Dictionary<DllType, DllCatalogEntry>();
        foreach (var type in requestedTypes)
        {
            try
            {
                candidates.Add(
                    type,
                    explicitVersions.TryGetValue(type, out var selector)
                        ? catalog.Resolve(type, selector)
                        : catalog.GetLatest(type));
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException)
            {
                throw new UsageException(exception.Message);
            }
        }

        return candidates;
    }

    internal static IReadOnlySet<DllType> ResolveFamilyFilter(
        IReadOnlyList<string> families) =>
        families.Select(ParseFamily).ToHashSet();

    private static DllType ParseFamily(string value)
    {
        if (!DllTypes.TryFromManifestKey(value.Trim(), out var definition))
        {
            throw new UsageException(
                $"Unknown DLL family '{value}'. Use a manifest key such as dlss or xess.");
        }

        return definition.Type;
    }
}

internal static class CliHelp
{
    internal static void Write()
    {
        Console.WriteLine(
            """
            DLSS Swapper LLE Linux CLI MVP
            UNTESTED ON LINUX AS OF THIS RELEASE.

            Commands:
              discover [--steam-root PATH ...]
              scan [--app-id ID ...] [--path PATH ...] [--root PATH ...] [--all]
                   [--steam-root PATH ...] [--manifest PATH]
              update (--app-id ID ... | --path PATH ... | --root PATH ... | --all)
                     [--family KEY ...] [--version KEY=VERSION[@MD5PREFIX] ...]
                     [--steam-root PATH ...] [--manifest PATH] [--dry-run | --yes]
              restore (--app-id ID ... | --path PATH ... | --root PATH ... | --all)
                      [--family KEY ...] [--steam-root PATH ...] [--dry-run | --yes]

            Selection:
              --path selects one explicit game directory and may be repeated.
              --root selects only each immediate child directory and may be repeated.
              Filesystem roots such as / are always rejected.
              scan with no selectors scans all discovered Steam games.
              update and restore require an explicit selector or --all.

            Update behavior:
              With no family/version options, every detected family uses its latest
              eligible non-development, signature-valid manifest release.
              --family selects that detected family at latest.
              --version selects that detected family at an exact manifest version.
              Always run update --dry-run first and close selected games before writing.
              A real update or restore requires explicit --yes confirmation.
            """);
    }
}
