using System.Text.Json;

namespace DlssSwapper.Linux.Cli.Platform;

public static class HeroicGogDiscovery
{
    public static ProviderDiscoveryResult Discover(IEnumerable<string> heroicDirectories, CancellationToken token)
    {
        var games = new List<ProviderGame>();
        var warnings = new List<string>();
        var sources = new List<DiscoverySourceOutcome>();
        foreach (var directory in heroicDirectories.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(directory)) { warnings.Add($"Heroic directory must be absolute: {directory}"); sources.Add(new("HeroicGog", directory, DiscoverySourceState.Unavailable, "Configuration path is not absolute.")); continue; }
            var path = Path.Combine(directory, "gog_store", "installed.json");
            if (!File.Exists(path)) { sources.Add(new("HeroicGog", path, DiscoverySourceState.Unavailable, "Installed metadata is missing or inaccessible.")); continue; }
            var complete = true;
            var titles = new Dictionary<string, string>(StringComparer.Ordinal);
            var titlePath = Path.Combine(directory, "store_cache", "gog_library.json");
            if (File.Exists(titlePath))
            {
                try
                {
                    using var catalog = Read(titlePath);
                    foreach (var game in catalog.RootElement.GetProperty("games").EnumerateArray())
                    {
                        token.ThrowIfCancellationRequested();
                        if (game.TryGetProperty("app_name", out var id) && id.ValueKind == JsonValueKind.String
                            && game.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(id.GetString()) && !string.IsNullOrWhiteSpace(title.GetString()))
                            titles[id.GetString()!] = title.GetString()!;
                    }
                }
                catch (Exception error) when (IsMetadataError(error)) { warnings.Add($"Could not read GOG titles in {titlePath}: {error.Message}"); }
            }
            try
            {
                using var installed = Read(path);
                foreach (var game in installed.RootElement.GetProperty("installed").EnumerateArray())
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (game.TryGetProperty("is_dlc", out var dlc) && dlc.ValueKind == JsonValueKind.True) continue;
                        var id = Required(game, "appName");
                        var root = Required(game, "install_path");
                        if (!Path.IsPathFullyQualified(root)) throw new IOException("Installation path is not absolute.");
                        if (!Directory.Exists(root)) warnings.Add($"GOG game {id} is unavailable at {root}");
                        var configuration = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
                        var flatpak = configuration.EndsWith(Path.Combine(".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic"), StringComparison.Ordinal);
                        games.Add(new(new(GameProvider.Gog, id), titles.GetValueOrDefault(id, $"GOG game {id}"), Path.GetFullPath(root), path)
                        {
                            CoverUrl = GogLocalArtwork.ReadCover(root, warnings, token),
                            Source = new("HeroicGog", path),
                            Launch = new(flatpak ? ProviderLauncher.HeroicFlatpak : ProviderLauncher.Heroic, id, configuration)
                            { HeroicRunner = "gog" },
                        });
                    }
                    catch (Exception error) when (IsMetadataError(error)) { complete = false; warnings.Add($"Invalid GOG installation in {path}: {error.Message}"); }
                }
                sources.Add(new("HeroicGog", path, complete ? DiscoverySourceState.Complete : DiscoverySourceState.Partial));
            }
            catch (Exception error) when (IsMetadataError(error)) { warnings.Add($"Could not read GOG installations in {path}: {error.Message}"); sources.Add(new("HeroicGog", path, DiscoverySourceState.Unavailable, error.Message)); }
        }
        return new(games, warnings) { Sources = sources };
    }

    private static JsonDocument Read(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new IOException("Metadata exceeds 16 MiB.");
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }
    private static string Required(JsonElement value, string key) => value.TryGetProperty(key, out var property)
        && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString())
        ? property.GetString()! : throw new IOException($"Missing {key}.");
    private static bool IsMetadataError(Exception error) => error is IOException or UnauthorizedAccessException
        or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException;
}
