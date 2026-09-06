using System.Text.Json;
using System.Text.Json.Serialization;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Cli.Core;

public sealed class DiscoverySnapshot
{
    public sealed record SavedScan(ScanResult Result, DateTimeOffset ObservedAtUtc);
    public List<SavedScan> Scans { get; set; } = [];
    public List<SteamGame> SteamGames { get; set; } = [];
    public List<ProviderGame> ProviderGames { get; set; } = [];
    public DateTimeOffset SavedAtUtc { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public static void Save(PersistentLibrary library, SteamDiscoveryResult steam, ProviderDiscoveryResult providers)
    {
        library.UpdateState(state =>
        {
            var snapshot = state.DiscoverySnapshot ??= new DiscoverySnapshot();
            var mergedSteam = DiscoveryCacheReconciliation.Steam(snapshot.SteamGames ?? [], steam);
            var mergedProviders = DiscoveryCacheReconciliation.Providers(snapshot.ProviderGames ?? [], providers);
            snapshot.SteamGames = mergedSteam.Games.ToList();
            snapshot.ProviderGames = mergedProviders.Games.ToList();
            snapshot.SavedAtUtc = DateTimeOffset.UtcNow;
        });
    }

    public static void SaveScans(PersistentLibrary library, IReadOnlyList<ScanResult> scans)
    {
        library.UpdateState(state =>
        {
            var snapshot = state.DiscoverySnapshot ??= new DiscoverySnapshot();
            var saved = (snapshot.Scans ?? []).ToDictionary(item => item.Result.Game.RootPath, PathComparers.FileSystemPath);
            foreach (var scan in scans.Where(scan => scan.CachedAtUtc is null && scan.Warnings.Count == 0))
                saved[scan.Game.RootPath] = new(scan, DateTimeOffset.UtcNow);
            snapshot.Scans = saved.Values.ToList();
        });
    }

    public static IReadOnlyList<ScanResult> ReadScans(PersistentLibrary library)
    {
        if (library.State.DiscoverySnapshot is not { } snapshot) return [];
        var saved = (snapshot.Scans ?? []).ToDictionary(item => item.Result.Game.RootPath, PathComparers.FileSystemPath);
        return library.Merge(new(snapshot.SteamGames ?? [], []), snapshot.ProviderGames ?? [])
            .Where(game => LibrarySelection.Includes(library.State, game))
            .Where(game => saved.ContainsKey(game.RootPath))
            .Select(game => saved[game.RootPath].Result with { Game = game, CachedAtUtc = saved[game.RootPath].ObservedAtUtc }).ToArray();
    }

    public static IReadOnlyList<ScanResult> ReconcileScans(
        IReadOnlyList<ScanResult> cached, IReadOnlyList<ScanResult> fresh)
    {
        var previous = cached.ToDictionary(scan => scan.Game.RootPath, PathComparers.FileSystemPath);
        // Membership comes from the current discovery, not from old scan records.
        return fresh.Select(scan => scan.Warnings.Count > 0
            && previous.TryGetValue(scan.Game.RootPath, out var old) && old.CachedAtUtc is not null
                ? old with { Game = scan.Game, Warnings = scan.Warnings }
                : scan).ToArray();
    }
}
