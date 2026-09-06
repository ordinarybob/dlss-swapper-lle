namespace DlssSwapper.Linux.Cli.Platform;

public static class GogPrefixDiscovery
{
    public static ProviderDiscoveryResult Discover(IEnumerable<string> prefixes, CancellationToken token)
    {
        var games = new List<ProviderGame>(); var warnings = new List<string>();
        var sources = new List<DiscoverySourceOutcome>();
        var roots = new[] { @"Software\GOG.com\Games\", @"Software\Wow6432Node\GOG.com\Games\" };
        foreach (var prefix in prefixes.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(prefix)) { warnings.Add($"GOG Wine prefix must be absolute: {prefix}"); continue; }
            var path = Path.Combine(prefix, "system.reg");
            if (!File.Exists(path))
            {
                sources.Add(new("GOG Wine", path, DiscoverySourceState.Unavailable, "Registry is unavailable."));
                continue;
            }
            var registry = WineRegistryReader.Read(path, key => roots.Any(root =>
                key.StartsWith(root, StringComparison.OrdinalIgnoreCase) && key.Length > root.Length
                && !key[root.Length..].Contains('\\')), token);
            warnings.AddRange(registry.Warnings);
            var complete = registry.Warnings.Count == 0;
            foreach (var section in registry.Sections)
            {
                token.ThrowIfCancellationRequested();
                var values = section.Values;
                if (!string.IsNullOrEmpty(values.GetValueOrDefault("dependsOn"))) continue;
                try
                {
                    string Required(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                        ? value : throw new IOException($"Missing GOG {key}.");
                    var id = Required("gameID"); var name = Required("gameName");
                    var root = EpicDiscovery.ResolveWinePath(prefix, Required("path"));
                    var available = Directory.Exists(root);
                    if (!available) warnings.Add($"GOG game {name} is unavailable at {root}");
                    games.Add(new(new(GameProvider.Gog, id), name, root, path, Path.GetFullPath(prefix))
                        { Source = new("GOG Wine", path), CoverUrl = available ? GogLocalArtwork.ReadCover(root, warnings, token) : null });
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                { complete = false; warnings.Add($"Invalid GOG registry entry in {path}: {error.Message}"); }
            }
            sources.Add(new("GOG Wine", path, complete ? DiscoverySourceState.Complete : DiscoverySourceState.Partial));
        }
        return new(games, warnings) { Sources = sources };
    }
}
