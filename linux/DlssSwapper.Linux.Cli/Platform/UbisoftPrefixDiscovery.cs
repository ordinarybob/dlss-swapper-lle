using System.Globalization;
using System.Text;
using DLSS_Swapper.Data.UbisoftConnect;
using DlssSwapper.Shared;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DlssSwapper.Linux.Cli.Platform;

public static class UbisoftPrefixDiscovery
{
    public static string? ResolveCover(string? thumbnail, IReadOnlyDictionary<string, Dictionary<string, string>>? localizations)
    {
        if (string.IsNullOrWhiteSpace(thumbnail)) return null;
        if (!thumbnail.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            && !thumbnail.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            if (localizations is null || !localizations.TryGetValue("default", out var defaults)
                || defaults is null || !defaults.TryGetValue(thumbnail, out thumbnail)) return null;
        }
        if (string.IsNullOrWhiteSpace(thumbnail)) return null;
        return "https://ubistatic3-a.akamaihd.net/orbit/uplay_launcher_3_0/assets/" + thumbnail;
    }

    private static readonly string[] LauncherKeys = [@"Software\Ubisoft\Launcher", @"Software\Wow6432Node\Ubisoft\Launcher"];

    public static ProviderDiscoveryResult Discover(IEnumerable<string> prefixes, CancellationToken token)
    {
        var games = new List<ProviderGame>(); var warnings = new List<string>();
        var sources = new List<DiscoverySourceOutcome>();
        var yaml = new DeserializerBuilder().IgnoreUnmatchedProperties()
            .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        foreach (var prefix in prefixes.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(prefix)) { warnings.Add($"Ubisoft Wine prefix must be absolute: {prefix}"); continue; }
            var sourcePath = Path.Combine(prefix, "system.reg");
            var sourceState = DiscoverySourceState.Unavailable;
            try
            {
                if (!File.Exists(sourcePath)) continue;
                var registry = WineRegistryReader.Read(sourcePath, key => LauncherKeys.Any(root =>
                    key.Equals(root, StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith(root + @"\Installs\", StringComparison.OrdinalIgnoreCase)), token);
                warnings.AddRange(registry.Warnings);
                sourceState = registry.Warnings.Count == 0 ? DiscoverySourceState.Complete : DiscoverySourceState.Partial;
                var installs = new Dictionary<uint, string>(); var cachePaths = new List<string>();
                foreach (var section in registry.Sections)
                {
                    if (!section.Values.TryGetValue("InstallDir", out var directory) || string.IsNullOrWhiteSpace(directory))
                    { sourceState = DiscoverySourceState.Partial; continue; }
                    try
                    {
                        var root = EpicDiscovery.ResolveWinePath(prefix, directory);
                        if (LauncherKeys.Contains(section.Key, StringComparer.OrdinalIgnoreCase))
                            cachePaths.Add(Path.Combine(root, "cache", "configuration", "configurations"));
                        else if (LauncherKeys.Any(key => section.Key.StartsWith(key + @"\Installs\", StringComparison.OrdinalIgnoreCase)
                            && !section.Key[(key.Length + 10)..].Contains('\\'))
                            && uint.TryParse(section.Key[(section.Key.LastIndexOf('\\') + 1)..], NumberStyles.None,
                                CultureInfo.InvariantCulture, out var id))
                        {
                            installs[id] = root;
                            if (!Directory.Exists(root)) warnings.Add($"Ubisoft game {id} is unavailable at {root}");
                        }
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                    { sourceState = DiscoverySourceState.Partial; warnings.Add($"Invalid Ubisoft installation in {prefix}: {error.Message}"); }
                }
                if (installs.Count == 0) continue;
                var user = WineRegistryReader.Read(Path.Combine(prefix, "user.reg"), key =>
                    key.Equals(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", StringComparison.OrdinalIgnoreCase), token);
                warnings.AddRange(user.Warnings);
                if (user.Warnings.Count > 0) sourceState = DiscoverySourceState.Partial;
                foreach (var section in user.Sections)
                    if (section.Values.TryGetValue("Local AppData", out var directory))
                    {
                        try { cachePaths.Insert(0, EpicDiscovery.ResolveWinePath(prefix, directory.TrimEnd('\\', '/')
                            + @"\Ubisoft Game Launcher\cache\configuration\configurations")); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                        { sourceState = DiscoverySourceState.Partial; warnings.Add($"Invalid Ubisoft local cache path in {prefix}: {error.Message}"); }
                    }
                var cache = cachePaths.FirstOrDefault(File.Exists);
                if (cache is null) { sourceState = DiscoverySourceState.Unavailable; warnings.Add($"Ubisoft installations found, but their configuration cache is unavailable in {prefix}"); continue; }
                using var stream = File.OpenRead(cache);
                if (stream.Length > 64 * 1024 * 1024) throw new IOException("Ubisoft configuration cache exceeds 64 MiB.");
                var data = new byte[checked((int)stream.Length)];
                stream.ReadExactly(data);
                if (stream.ReadByte() != -1) throw new IOException("Ubisoft configuration cache changed while reading.");
                token.ThrowIfCancellationRequested();
                var records = UbisoftConfigurationReader.Read(data);
                warnings.AddRange(records.Warnings.Select(message => $"{cache}: {message}"));
                if (records.Warnings.Count > 0 || installs.Keys.Except(records.Records.Select(record => record.InstallId)).Any())
                    sourceState = DiscoverySourceState.Partial;
                var added = new HashSet<uint>();
                foreach (var record in records.Records)
                {
                    token.ThrowIfCancellationRequested();
                    if (!installs.TryGetValue(record.InstallId, out var root) || added.Contains(record.InstallId)) continue;
                    try
                    {
                        var item = yaml.Deserialize<UbisoftConnectConfigurationItem>(
                            new UTF8Encoding(false, true).GetString(data, record.Offset, record.Length));
                        // Match Windows discovery: exclude entitlement-only, DLC and pre-order records.
                        if (item?.Root?.Installer is null || item.Root.StartGame is null) continue;
                        var name = item.Root.Installer.GameIdentifier;
                        if (string.IsNullOrWhiteSpace(name)) throw new IOException("Missing game title.");
                        games.Add(new(new(GameProvider.Ubisoft, record.InstallId.ToString(CultureInfo.InvariantCulture)),
                            name, root, cache, Path.GetFullPath(prefix))
                            { Source = new("Ubisoft Wine", sourcePath), CoverUrl = ResolveCover(item.Root.ThumbImage, item.Localizations) });
                        added.Add(record.InstallId);
                    }
                    catch (Exception error) when (error is YamlException or IOException or ArgumentException)
                    { sourceState = DiscoverySourceState.Partial; warnings.Add($"Invalid Ubisoft metadata for {record.InstallId} in {cache}: {error.Message}"); }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { sourceState = DiscoverySourceState.Unavailable; warnings.Add($"Could not read Ubisoft prefix {prefix}: {error.Message}"); }
            finally { sources.Add(new("Ubisoft Wine", sourcePath, sourceState)); }
        }
        return new(games, warnings) { Sources = sources };
    }
}
