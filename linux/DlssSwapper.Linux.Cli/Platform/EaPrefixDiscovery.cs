using System.Xml;
using System.Xml.Linq;

namespace DlssSwapper.Linux.Cli.Platform;

public static class EaPrefixDiscovery
{
    private static readonly string[] LauncherKeys =
        [@"Software\Electronic Arts\EA Desktop", @"Software\Wow6432Node\Electronic Arts\EA Desktop"];
    private static readonly string[] UninstallRoots =
        [@"Software\Microsoft\Windows\CurrentVersion\Uninstall\", @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\"];

    public static ProviderDiscoveryResult Discover(IEnumerable<string> prefixes, CancellationToken token)
    {
        var games = new List<ProviderGame>(); var warnings = new List<string>();
        var sources = new List<DiscoverySourceOutcome>();
        foreach (var prefix in prefixes.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(prefix)) { warnings.Add($"EA Wine prefix must be absolute: {prefix}"); continue; }
            var sourcePath = Path.Combine(prefix, "system.reg");
            if (!File.Exists(sourcePath)) { sources.Add(new("EA Wine", sourcePath, DiscoverySourceState.Unavailable)); continue; }
            var system = WineRegistryReader.Read(sourcePath, Include, token);
            var sourceState = system.Warnings.Count == 0 ? DiscoverySourceState.Complete : DiscoverySourceState.Partial;
            warnings.AddRange(system.Warnings);
            var launcherPresent = false;
            foreach (var section in system.Sections.Where(section => LauncherKeys.Contains(section.Key, StringComparer.OrdinalIgnoreCase)))
            {
                try
                {
                    if (section.Values.TryGetValue("InstallLocation", out var location)
                        && Directory.Exists(EpicDiscovery.ResolveWinePath(prefix, location))) launcherPresent = true;
                    else sourceState = DiscoverySourceState.Unavailable;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                { sourceState = DiscoverySourceState.Partial; warnings.Add($"Invalid EA launcher location in {prefix}: {error.Message}"); }
            }
            // Match Windows' EA app availability check before enumerating its games.
            if (!launcherPresent) { sources.Add(new("EA Wine", sourcePath, sourceState)); continue; }
            var user = WineRegistryReader.Read(Path.Combine(prefix, "user.reg"), Include, token);
            warnings.AddRange(user.Warnings);
            if (user.Warnings.Count > 0 || !File.Exists(Path.Combine(prefix, "user.reg"))) sourceState = DiscoverySourceState.Partial;
            var seen = new HashSet<(string Id, string Path)>();
            foreach (var section in system.Sections.Concat(user.Sections))
            {
                token.ThrowIfCancellationRequested();
                var values = section.Values;
                if (!values.TryGetValue("UninstallString", out var uninstall)
                    || !uninstall.Contains("EAInstaller", StringComparison.OrdinalIgnoreCase)
                    || !uninstall.Contains("Cleanup.exe", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    string Required(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                        ? value : throw new IOException($"Missing {key}.");
                    var location = Required("InstallLocation"); var name = Required("DisplayName");
                    var root = EpicDiscovery.ResolveWinePath(prefix, location);
                    if (!Directory.Exists(root)) { sourceState = DiscoverySourceState.Partial; warnings.Add($"EA game {name} is unavailable at {root}"); continue; }
                    var metadata = EpicDiscovery.ResolveWinePath(prefix, location.TrimEnd('\\', '/') + @"\__Installer\installerdata.xml");
                    using var reader = XmlReader.Create(metadata, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersInDocument = 4 * 1024 * 1024,
                    });
                    var id = XDocument.Load(reader).Descendants("contentID").FirstOrDefault()?.Value;
                    if (string.IsNullOrWhiteSpace(id)) throw new IOException("Installer metadata has no contentID.");
                    string? icon = null; var iconIndex = 0;
                    if (values.TryGetValue("DisplayIcon", out var displayIcon) && !string.IsNullOrWhiteSpace(displayIcon))
                    {
                        try
                        {
                            var reference = DlssSwapper.Shared.WindowsIconReference.Parse(displayIcon);
                            icon = EpicDiscovery.ResolveWinePath(prefix, reference.Path); iconIndex = reference.Index;
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or FormatException)
                        { warnings.Add($"Could not resolve EA icon for {name}: {error.Message}"); }
                    }
                    if (seen.Add((id, root))) games.Add(new(new(GameProvider.Ea, id), name, root, metadata, Path.GetFullPath(prefix))
                    { Source = new("EA Wine", sourcePath), Launch = ProviderLaunch.ForEa(prefix, id), LocalIconPath = icon, LocalIconIndex = iconIndex });
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or XmlException)
                { sourceState = DiscoverySourceState.Partial; warnings.Add($"Could not read EA game {section.Key} in {prefix}: {error.Message}"); }
            }
            sources.Add(new("EA Wine", sourcePath, sourceState));
        }
        return new(games, warnings) { Sources = sources };
    }

    private static bool Include(string key) => LauncherKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
        || UninstallRoots.Any(root => key.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && key.Length > root.Length && !key[root.Length..].Contains('\\'));
}
