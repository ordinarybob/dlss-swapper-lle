using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Cli.Platform;

public static class ProviderDiscovery
{
    public static ProviderDiscoveryResult Discover(LinuxLibraryState state, CancellationToken token, bool includeDefaults = true, bool respectLibrarySelection = false)
    {
        bool Enabled(string id) => !respectLibrarySelection || LibrarySelection.Enabled(state, id);
        ProviderDiscoveryResult empty = new([], []);
        var prefixes = state.ManualGames.Select(game => game.Launch?.WinePrefix)
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix)).Select(prefix => prefix!)
            .Concat(state.ProviderWinePrefixes);
        var epic = Enabled("Epic Games Store") ? EpicDiscovery.Discover(prefixes, token) : empty;
        var gogPrefixes = Enabled("GOG") ? GogPrefixDiscovery.Discover(prefixes, token) : empty;
        var ubisoft = Enabled("Ubisoft Connect") ? UbisoftPrefixDiscovery.Discover(prefixes, token) : empty;
        var ea = Enabled("EA App") ? EaPrefixDiscovery.Discover(prefixes, token) : empty;
        var battleNet = Enabled("Battle.net") ? BattleNetPrefixDiscovery.Discover(prefixes, token) : empty;
        var configurations = (includeDefaults ? LegendaryDiscovery.DefaultConfigDirectories() : [])
            .Concat(state.LegendaryConfigDirectories)
            .Concat(state.HeroicConfigDirectories.Select(path => Path.Combine(path, "legendaryConfig", "legendary"))).ToArray();
        var legendary = Enabled("Epic Games Store") ? LegendaryDiscovery.Discover(configurations, token) : empty;
        var heroicDirectories = configurations.Where(Path.IsPathFullyQualified).Select(path => new DirectoryInfo(path))
            .Where(directory => directory.Name == "legendary" && directory.Parent?.Name == "legendaryConfig"
                && directory.Parent.Parent?.Name == "heroic").Select(directory => directory.Parent!.Parent!.FullName);
        var gog = Enabled("GOG") ? HeroicGogDiscovery.Discover(heroicDirectories, token) : empty;
        return new(epic.Games.Concat(legendary.Games).Concat(gog.Games).Concat(gogPrefixes.Games).Concat(ubisoft.Games).Concat(ea.Games).Concat(battleNet.Games).ToArray(),
            epic.Warnings.Concat(legendary.Warnings).Concat(gog.Warnings).Concat(gogPrefixes.Warnings).Concat(ubisoft.Warnings).Concat(ea.Warnings).Concat(battleNet.Warnings).ToArray())
        { Sources = new[] { epic, legendary, gog, gogPrefixes, ubisoft, ea, battleNet }.SelectMany(result => result.Sources).ToArray() };
    }
}
