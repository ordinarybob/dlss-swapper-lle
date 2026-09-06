using System.Text.Json;

namespace DlssSwapper.Linux.Cli.Platform;

public sealed record ProviderDiscoveryResult(IReadOnlyList<ProviderGame> Games, IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<DiscoverySourceOutcome> Sources { get; init; } = [];
}

public static class EpicDiscovery
{
    public static ProviderDiscoveryResult Discover(IEnumerable<string> winePrefixes, CancellationToken token)
    {
        var games = new List<ProviderGame>(); var warnings = new List<string>();
        var sources = new List<DiscoverySourceOutcome>();
        foreach (var prefix in winePrefixes.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var manifests = ResolveWinePath(prefix, @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests");
                if (!Directory.Exists(manifests)) { sources.Add(new("EpicPrefix", prefix, DiscoverySourceState.Unavailable, "Manifest directory is missing or inaccessible.")); continue; }
                var complete = true;
                foreach (var file in Directory.EnumerateFiles(manifests).Where(path => path.EndsWith(".item", StringComparison.OrdinalIgnoreCase)))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (new FileInfo(file).Length > 4 * 1024 * 1024) throw new IOException("Manifest exceeds 4 MiB.");
                        using var input = File.OpenRead(file); using var document = JsonDocument.Parse(input);
                        var item = document.RootElement;
                        if (!item.TryGetProperty("AppCategories", out var categories)
                            || categories.ValueKind != JsonValueKind.Array || !categories.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String && value.GetString() == "games")) continue;
                        if (item.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) continue;
                        var id = Required(item, "CatalogItemId");
                        var name = Required(item, "DisplayName");
                        var install = ResolveWinePath(prefix, Required(item, "InstallLocation"));
                        if (!Directory.Exists(install)) warnings.Add($"Epic game {name} is unavailable at {install}");
                        var aliases = item.TryGetProperty("AppName", out var app) && app.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(app.GetString())
                            ? new[] { new ProviderGameIdentity(GameProvider.Epic, "app:" + app.GetString()) } : [];
                        games.Add(new(new(GameProvider.Epic, id), name, install, file, Path.GetFullPath(prefix))
                        { Source = new("EpicPrefix", prefix), IdentityAliases = aliases, Launch = ProviderLaunch.ForEpic(prefix, id, Required(item, "InstallLocation")) });
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
                    { complete = false; warnings.Add($"Could not read Epic manifest {file}: {error.Message}"); }
                }
                sources.Add(new("EpicPrefix", prefix, complete ? DiscoverySourceState.Complete : DiscoverySourceState.Partial));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { warnings.Add($"Could not scan Epic prefix {prefix}: {error.Message}"); sources.Add(new("EpicPrefix", prefix, DiscoverySourceState.Unavailable, error.Message)); }
        }
        return new(games, warnings) { Sources = sources };
    }

    public static string ResolveWinePath(string prefix, string windowsPath)
    {
        if (!Path.IsPathFullyQualified(prefix)) throw new IOException("Wine prefix must be an absolute path.");
        var normalized = windowsPath.Replace('\\', '/');
        if (normalized.Length < 3 || !char.IsAsciiLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '/')
            throw new IOException("Expected an absolute Windows drive path.");
        var drive = char.ToLowerInvariant(normalized[0]);
        var root = drive == 'c' ? Path.Combine(prefix, "drive_c") : Path.Combine(prefix, "dosdevices", drive + ":");
        if (drive != 'c') root = new DirectoryInfo(root).ResolveLinkTarget(true)?.FullName
            ?? throw new IOException($"Wine drive {drive}: has no readable mapping.");
        foreach (var part in normalized[3..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is "." or ".." || part.Contains(':')) throw new IOException("Unsupported path component in Epic manifest.");
            var exact = Path.Combine(root, part);
            if (Directory.Exists(exact) || File.Exists(exact) || !Directory.Exists(root)) { root = exact; continue; }
            var matches = Directory.EnumerateFileSystemEntries(root)
                .Where(path => Path.GetFileName(path).Equals(part, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (matches.Length > 1) throw new IOException("Ambiguous Windows path casing in Wine prefix.");
            root = matches.Length == 1 ? matches[0] : exact;
        }
        return Path.GetFullPath(root);
    }

    private static string Required(JsonElement item, string name) => item.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
        ? value.GetString()! : throw new IOException($"Missing {name}.");
}
