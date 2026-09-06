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
    internal List<string> Operands { get; } = [];
    internal string? ManifestPath { get; set; }
    internal string? PackageDirectory { get; set; }
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
                case "--package":
                    options.PackageDirectory = ReadValue(args, ref index, argument);
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
                    if (argument.StartsWith('-'))
                    {
                        throw new UsageException($"Unknown option '{argument}'.");
                    }

                    options.Operands.Add(argument);
                    break;
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

        if (options.Command is not ("discover" or "scan" or "update" or "restore"
            or "state" or "filesystems" or "reset" or "streamline"))
        {
            throw new UsageException($"Unknown command '{options.Command}'.");
        }

        var hasSelectors = options.AppIds.Count > 0
            || options.Paths.Count > 0
            || options.Roots.Count > 0;

        if (options.Command == "streamline")
        {
            var action = options.Operands.Count == 1 ? options.Operands[0] : "";
            if (action is not ("inspect" or "update" or "restore" or "recover")
                || options.Paths.Count != 1 || options.AppIds.Count > 0 || options.Roots.Count > 0
                || options.All || options.SteamRoots.Count > 0 || options.Families.Count > 0
                || options.Versions.Count > 0 || options.ManifestPath is not null
                || (action == "update") != (options.PackageDirectory is not null)
                || (action == "inspect" && (options.Yes || options.DryRun)))
                throw new UsageException("streamline requires inspect|update|restore|recover and exactly one --path; only update requires --package.");
            if (action != "inspect" && !options.DryRun && !options.Yes)
                throw new UsageException("Streamline writes require --yes; use --dry-run to inspect without changing files.");
            return;
        }
        if (options.PackageDirectory is not null)
            throw new UsageException("--package is only accepted by streamline update.");

        if (options.Command == "state")
        {
            ValidateState(options);
            return;
        }

        if (options.Command == "reset")
        {
            if (!options.Yes || options.Operands.Count > 0 || hasSelectors
                || options.SteamRoots.Count > 0 || options.DryRun
                || options.Families.Count > 0 || options.Versions.Count > 0
                || options.ManifestPath is not null || options.All)
            {
                throw new UsageException("reset accepts only the required --yes confirmation.");
            }

            return;
        }

        if (options.Command == "filesystems")
        {
            if (options.Operands.Count > 0 || options.AppIds.Count > 0
                || options.All || options.DryRun || options.Yes
                || options.Families.Count > 0 || options.Versions.Count > 0
                || options.ManifestPath is not null)
            {
                throw new UsageException(
                    "filesystems accepts only --path, --root, and --steam-root options.");
            }

            return;
        }

        if (options.Operands.Count > 0)
        {
            throw new UsageException($"Command '{options.Command}' does not accept positional values.");
        }
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

    private static void ValidateState(CliOptions options)
    {
        if (options.All || options.DryRun || options.Yes || options.AppIds.Count > 0
            || options.Paths.Count > 0 || options.Roots.Count > 0
            || options.SteamRoots.Count > 0 || options.Families.Count > 0
            || options.Versions.Count > 0 || options.ManifestPath is not null)
        {
            throw new UsageException("state accepts an action and optional value, without command options.");
        }

        if (options.Operands.Count == 0)
        {
            options.Operands.Add("show");
        }

        var action = options.Operands[0].ToLowerInvariant();
        var requiresValue = action is "add-game" or "remove-game"
            or "add-steam-root" or "remove-steam-root"
            or "add-pattern" or "remove-pattern"
            or "add-provider-prefix" or "remove-provider-prefix"
            or "add-heroic-config" or "remove-heroic-config"
            or "add-legendary-config" or "remove-legendary-config" or "set-heroic-executable";
        if (action is not ("show" or "restore-steam" or "restore-providers" or "clear-heroic-executable") && !requiresValue)
        {
            throw new UsageException($"Unknown state action '{action}'.");
        }

        var expected = requiresValue ? 2 : 1;
        if (options.Operands.Count != expected)
        {
            throw new UsageException(
                requiresValue
                    ? $"state {action} requires exactly one value."
                    : $"state {action} does not accept a value.");
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
        SteamDiscoveryResult discovery,
        IReadOnlyList<SelectedGame>? libraryGames = null)
    {
        var games = new Dictionary<string, SelectedGame>(PathComparer);
        if (options.All
            || (options.Command == "scan"
                && options.AppIds.Count == 0
                && options.Paths.Count == 0
                && options.Roots.Count == 0))
        {
            foreach (var game in libraryGames ?? discovery.Games.Select(steamGame => new SelectedGame(
                steamGame.Name, steamGame.InstallDirectory, steamGame.AppId)).ToArray())
            {
                Add(games, game);
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
            var separator = specification.IndexOf('=', StringComparison.Ordinal);
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
            DLSS Swapper LLE Linux CLI

            Commands:
              streamline inspect --path GAME
              streamline update --path GAME --package PRODUCTION_FOLDER (--dry-run|--yes)
              streamline (restore|recover) --path GAME (--dry-run|--yes)
              discover [--steam-root PATH ...]
              filesystems [--path PATH ...] [--root PATH ...] [--steam-root PATH ...]
              state [show]
              state (add-game|remove-game|add-steam-root|remove-steam-root) PATH
              state (add-pattern|remove-pattern) PATTERN
              state restore-steam
              reset --yes
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
              scan with no selectors and --all use the saved library: Steam, manual games,
              and supported launcher sources, respecting saved exclusions.
              Explicit --app-id and --path selections override library exclusions.
              update and restore require an explicit selector or --all.

            Update behavior:
              With no family/version options, every detected family uses its latest
              eligible non-development, signature-valid manifest release.
              --family selects that detected family at latest.
              --version selects that detected family at an exact manifest version.
              Always run update --dry-run first and close selected games before writing.
              A real update or restore requires explicit --yes confirmation.

            Local state:
              state commands manage the same persistent library used by the GUI.
              state add-provider-prefix PATH | remove-provider-prefix PATH
              state add-legendary-config PATH | remove-legendary-config PATH
              state add-heroic-config PATH | remove-heroic-config PATH
              state set-heroic-executable PATH | clear-heroic-executable
              state restore-providers clears launcher-game exclusions only.
              Provider paths must be absolute; disconnected paths can remain saved.
              filesystems reports the backing filesystem and warns on NTFS/FUSE.
              reset --yes removes only LLE-owned Linux config and application cache.
              Artwork cached beside a SteamLibrary is intentionally preserved.
            """);
    }
}
