using DLSS_Swapper.Data;

internal static class DiscoveryMetadataTests
{
    public static void Run()
    {
        void Check(bool value)
        {
            if (!value) throw new Exception("Discovery metadata contract failed.");
        }
        Check(!DiscoveryMetadata.HasBattleNetInstallationState(null, "game"));
        Check(!DiscoveryMetadata.HasBattleNetInstallationState(true, null));
        Check(DiscoveryMetadata.HasBattleNetInstallationState(false, "game"));
        Check(DiscoveryMetadata.BattleNetTitle("Known", "Aggregate", "folder") == "Known");
        Check(DiscoveryMetadata.BattleNetTitle("", "Aggregate", "folder") == "Aggregate");
        Check(DiscoveryMetadata.BattleNetTitle(null, null, "folder") == "folder");
        Check(DiscoveryMetadata.UbisoftThumbnail(null, null) == "");
        Check(DiscoveryMetadata.UbisoftThumbnail("missing", null) == "");
        Check(DiscoveryMetadata.UbisoftThumbnail("cover.PNG", null).EndsWith("/cover.PNG"));
        var translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["default"] = new() { ["thumbnail"] = "cover.jpg" }
        };
        Check(DiscoveryMetadata.UbisoftThumbnail("thumbnail", translations).EndsWith("/cover.jpg"));
        var source = Path.GetFullPath(Path.Combine("fixtures", "steamapps"));
        var game = Path.Combine(source, "common", "Game");
        Check(!DiscoveryMetadata.CanRemoveSteamCache(game, false, [source]));
        Check(!DiscoveryMetadata.CanRemoveSteamCache(game, true, []));
        Check(!DiscoveryMetadata.CanRemoveSteamCache(game, true, [source + "-other"]));
        Check(!DiscoveryMetadata.CanRemoveSteamCache(Path.Combine(source, "common-other", "Game"), true, [source]));
        Check(DiscoveryMetadata.CanRemoveSteamCache(game, true, [source]));
        Console.WriteLine("Discovery metadata: missing state, title fallback, thumbnail lookup and per-library cache-removal policy passed.");
    }
}
