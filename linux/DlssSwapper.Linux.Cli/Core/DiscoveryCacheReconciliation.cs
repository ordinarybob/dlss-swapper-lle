using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record ReconciledDiscovery<T>(IReadOnlyList<T> Games, IReadOnlyList<T> UnverifiedGames);

public static class DiscoveryCacheReconciliation
{
    public static ReconciledDiscovery<SteamGame> Steam(IReadOnlyList<SteamGame> previous, SteamDiscoveryResult current) =>
        Reconcile(previous, current.Games, current.Sources, game => new("SteamLibrary", game.LibraryRoot),
            game => ("Steam", game.AppId, NormalizePath(game.InstallDirectory)));

    public static ReconciledDiscovery<ProviderGame> Providers(IReadOnlyList<ProviderGame> previous, ProviderDiscoveryResult current) =>
        Reconcile(previous, current.Games, current.Sources, game => game.Source,
            game => (game.Identity.Provider.ToString(), game.Identity.Id, NormalizePath(game.InstallDirectory)));

    private static ReconciledDiscovery<T> Reconcile<T>(IReadOnlyList<T> previous, IReadOnlyList<T> current,
        IReadOnlyList<DiscoverySourceOutcome> outcomes, Func<T, DiscoverySourceKey?> source,
        Func<T, (string Provider, string Id, string Root)> identity)
    {
        // Contradictory outcomes never authorize dropping cached games.
        var complete = outcomes.GroupBy(item => SourceKey(new(item.Kind, item.Path)))
            .Where(group => group.All(item => item.State == DiscoverySourceState.Complete))
            .Select(group => group.Key).ToHashSet();
        var games = new List<T>(); var unverified = new List<T>();
        var seen = new HashSet<((string, string, string) Identity, (string, string)? Source)>();
        foreach (var game in current)
            if (seen.Add((identity(game), SourceKey(source(game))))) games.Add(game);
        foreach (var game in previous)
        {
            var key = SourceKey(source(game));
            if (!seen.Add((identity(game), key))) continue;
            if (key is not null && complete.Contains(key)) continue;
            games.Add(game); unverified.Add(game);
        }
        return new(games, unverified);
    }

    private static (string, string)? SourceKey(DiscoverySourceKey? source) => source is null ? null : (source.Kind, NormalizePath(source.Path));
    private static string NormalizePath(string path)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return OperatingSystem.IsWindows() ? normalized.ToUpperInvariant() : normalized;
    }
}
