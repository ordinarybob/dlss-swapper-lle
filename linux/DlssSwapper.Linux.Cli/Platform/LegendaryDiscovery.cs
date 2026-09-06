using System.Text.Json;

namespace DlssSwapper.Linux.Cli.Platform;

public static class LegendaryDiscovery
{
    public static IEnumerable<string> DefaultConfigDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(config)) config = Path.Combine(home, ".config");
        var custom = Environment.GetEnvironmentVariable("LEGENDARY_CONFIG_PATH");
        yield return string.IsNullOrWhiteSpace(custom) ? Path.Combine(config, "legendary") : custom;
        yield return Path.Combine(config, "heroic", "legendaryConfig", "legendary");
        yield return Path.Combine(home, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic", "legendaryConfig", "legendary");
    }

    public static ProviderDiscoveryResult Discover(IEnumerable<string> configDirectories, CancellationToken token)
    {
        var games = new List<ProviderGame>(); var warnings = new List<string>();
        var sources = new List<DiscoverySourceOutcome>();
        foreach (var directory in configDirectories.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(directory)) { warnings.Add($"Legendary configuration path is not absolute: {directory}"); sources.Add(new("Legendary", directory, DiscoverySourceState.Unavailable, "Configuration path is not absolute.")); continue; }
            var path = Path.Combine(directory, "installed.json");
            if (!File.Exists(path)) { sources.Add(new("Legendary", path, DiscoverySourceState.Unavailable, "Metadata is missing or inaccessible.")); continue; }
            var complete = true;
            try
            {
                if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new IOException("Installed-game metadata exceeds 16 MiB.");
                using var input = File.OpenRead(path); using var json = JsonDocument.Parse(input);
                foreach (var property in json.RootElement.EnumerateObject())
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var game = property.Value;
                        if (Flag(game, "is_dlc") || Flag(game, "is_preloaded")) continue;
                        var app = Required(game, "app_name");
                        var title = Required(game, "title");
                        var install = Required(game, "install_path");
                        if (!Path.IsPathFullyQualified(install)) throw new IOException("Installation path is not absolute.");
                        if (!Directory.Exists(install)) warnings.Add($"Legendary game {title} is unavailable at {install}");
                        // Legendary app IDs are not Epic catalog IDs. Namespace them to avoid collisions.
                        var configuration = Path.GetFullPath(directory);
                        var heroic = string.Equals(new DirectoryInfo(configuration).Parent?.Name, "legendaryConfig", StringComparison.Ordinal)
                            && string.Equals(new DirectoryInfo(configuration).Parent?.Parent?.Name, "heroic", StringComparison.Ordinal)
                            && string.Equals(new DirectoryInfo(configuration).Name, "legendary", StringComparison.Ordinal);
                        var flatpak = heroic && configuration.EndsWith(Path.Combine(".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic", "legendaryConfig", "legendary"), StringComparison.Ordinal);
                        var appIdentity = new ProviderGameIdentity(GameProvider.Epic, "app:" + app);
                        var catalogId = ReadCatalogId(configuration, app, warnings);
                        games.Add(new(catalogId is null ? appIdentity : new(GameProvider.Epic, catalogId), title, Path.GetFullPath(install), path)
                        {
                            IdentityAliases = catalogId is null ? [] : [appIdentity],
                            Source = new("Legendary", path),
                            Launch = new(flatpak ? ProviderLauncher.HeroicFlatpak : heroic ? ProviderLauncher.Heroic : ProviderLauncher.Legendary, app, configuration),
                        });
                    }
                    catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException)
                    { complete = false; warnings.Add($"Invalid installed game {property.Name} in {path}: {error.Message}"); }
                }
                sources.Add(new("Legendary", path, complete ? DiscoverySourceState.Complete : DiscoverySourceState.Partial));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            { warnings.Add($"Could not read {path}: {error.Message}"); sources.Add(new("Legendary", path, DiscoverySourceState.Unavailable, error.Message)); }
        }
        return new(games, warnings) { Sources = sources };
    }

    private static bool Flag(JsonElement game, string key) => game.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    private static string? ReadCatalogId(string configuration, string app, List<string> warnings)
    {
        // Never use a launcher-supplied identifier to traverse outside metadata.
        if (app.IndexOfAny(['/', '\\', ':']) >= 0 || app.Any(char.IsControl) || app is "." or "..")
        { warnings.Add($"Cannot match Legendary catalog identity: invalid app name {app}."); return null; }
        var path = Path.Combine(configuration, "metadata", app + ".json");
        if (!File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException("Game metadata exceeds 4 MiB.");
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (Required(root, "app_name") != app) throw new IOException("Game metadata belongs to a different app.");
            return Required(root.GetProperty("metadata"), "id");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { warnings.Add($"Could not match Legendary catalog identity in {path}: {error.Message}"); return null; }
    }
    private static string Required(JsonElement game, string key) => game.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
        ? value.GetString()! : throw new IOException($"Missing {key}.");
}
