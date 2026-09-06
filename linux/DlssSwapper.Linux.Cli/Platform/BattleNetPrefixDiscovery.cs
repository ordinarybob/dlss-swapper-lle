using System.Text.Json;
using DLSS_Swapper.Data.BattleNet.Proto;

namespace DlssSwapper.Linux.Cli.Platform;

public sealed record BattleNetMetadata(string ProductCode, string? LauncherId, string? ClientPath, bool Playable, string? CoverUrl);

public static class BattleNetPrefixDiscovery
{

    public static ProviderDiscoveryResult Discover(IEnumerable<string> prefixes, CancellationToken token)
    {
        var games = new List<ProviderGame>(); var warnings = new List<string>();
        var sources = new List<DiscoverySourceOutcome>();
        foreach (var prefix in prefixes.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            string? file = null;
            var sourceState = DiscoverySourceState.Unavailable;
            try
            {
                file = EpicDiscovery.ResolveWinePath(prefix, @"C:\ProgramData\Battle.net\Agent\product.db");
                if (!File.Exists(file)) continue;
                using var input = File.OpenRead(file);
                if (input.Length > 64 * 1024 * 1024) throw new IOException("Battle.net database exceeds 64 MiB.");
                var bytes = new byte[checked((int)input.Length)]; input.ReadExactly(bytes);
                if (input.ReadByte() != -1) throw new IOException("Battle.net database changed while reading.");
                var db = ProductDb.Parser.ParseFrom(bytes);
                sourceState = DiscoverySourceState.Complete;
                var client = FindClient(prefix, warnings, token);
                var aggregates = ReadAggregates(Path.Combine(Path.GetDirectoryName(file)!, "aggregate.json"), warnings);
                var seen = new HashSet<(string, string)>();
                foreach (var product in db.ProductInstalls)
                {
                    token.ThrowIfCancellationRequested();
                    if (product.Uid.StartsWith("wow", StringComparison.OrdinalIgnoreCase)
                        || new[] { "agent", "agent_beta", "beta", "battle.net" }.Contains(product.Uid, StringComparer.OrdinalIgnoreCase)) continue;
                    if (product.CachedProductState?.BaseProductState is not { } state)
                    { sourceState = DiscoverySourceState.Partial; warnings.Add($"Battle.net entry {product.Uid} has no installation state in {file}"); continue; }
                    if (!state.Installed) continue;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(product.Uid) || string.IsNullOrWhiteSpace(product.Settings?.InstallPath))
                            throw new IOException("Missing game identity or installation path.");
                        var root = EpicDiscovery.ResolveWinePath(prefix, product.Settings.InstallPath);
                        if (!Directory.Exists(root)) warnings.Add($"Battle.net game {product.Uid} is unavailable at {root}");
                        var known = DlssSwapper.Shared.BattleNetGameCatalog.Games.GetValueOrDefault(product.Uid);
                        var aggregate = aggregates.GetValueOrDefault(product.ProductCode);
                        var title = known?.Name ?? aggregate.Title ?? Path.GetFileName(root);
                        if (seen.Add((product.Uid, root))) games.Add(new(new(GameProvider.BattleNet, product.Uid), title, root, file, Path.GetFullPath(prefix))
                        {
                            BattleNet = new(product.ProductCode, known?.LauncherId, client, state.Playable, aggregate.Cover),
                            Source = new("Battle.net Wine", file),
                            Launch = known is not null && client is not null ? new(ProviderLauncher.Wine, product.Uid, Path.GetFullPath(prefix))
                            { WindowsExecutable = client, WindowsArgument = "--exec=launch " + known.LauncherId, ClientName = "Battle.net" } : null,
                        });
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                    { sourceState = DiscoverySourceState.Partial; warnings.Add($"Invalid Battle.net entry {product.Uid} in {file}: {error.Message}"); }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { sourceState = DiscoverySourceState.Unavailable; warnings.Add($"Could not read Battle.net prefix {prefix}: {error.Message}"); }
            finally { if (file is not null) sources.Add(new("Battle.net Wine", file, sourceState)); }
        }
        return new(games, warnings) { Sources = sources };
    }

    private static string? FindClient(string prefix, List<string> warnings, CancellationToken token)
    {
        var keys = new[] { @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net", @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net" };
        var registry = WineRegistryReader.Read(Path.Combine(prefix, "system.reg"), key => keys.Contains(key, StringComparer.OrdinalIgnoreCase), token);
        warnings.AddRange(registry.Warnings);
        foreach (var section in registry.Sections)
        {
            if (!section.Values.TryGetValue("InstallLocation", out var location)) continue;
            try
            {
                var client = EpicDiscovery.ResolveWinePath(prefix, location.TrimEnd('\\', '/') + @"\Battle.net.exe");
                if (File.Exists(client)) return client;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { warnings.Add($"Invalid Battle.net client location: {error.Message}"); }
        }
        return null;
    }

    private static Dictionary<string, (string? Title, string? Cover)> ReadAggregates(string path, List<string> warnings)
    {
        var result = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;
        try
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new IOException("Aggregate metadata exceeds 16 MiB.");
            using var input = File.OpenRead(path); using var json = JsonDocument.Parse(input);
            if (!json.RootElement.TryGetProperty("installed", out var installed) || installed.ValueKind != JsonValueKind.Array)
                throw new IOException("Aggregate metadata has no installed array.");
            foreach (var item in installed.EnumerateArray())
            {
                string? Value(string key) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var value)
                    && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
                if (Value("product_id") is { } id) result[id] = (Value("name"), Value("logo_art_uri"));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { warnings.Add($"Could not read Battle.net aggregate {path}: {error.Message}"); }
        return result;
    }
}
