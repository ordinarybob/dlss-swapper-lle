using System.Text.Json;
using System.Text.Json.Serialization;

namespace DlssSwapper.Linux.Cli.Platform;

public enum GameProvider { Epic, Gog, Ubisoft, Ea, BattleNet, Xbox }

public sealed record ProviderGameIdentity(GameProvider Provider, string Id)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public bool Equals(ProviderGameIdentity? other) => other is not null && Provider == other.Provider && Id == other.Id;
    public override int GetHashCode() => HashCode.Combine(Provider, Id);
}

public sealed record ProviderGame(ProviderGameIdentity Identity, string Name, string InstallDirectory,
    string ManifestPath, string? WinePrefix = null)
{
    public DiscoverySourceKey? Source { get; init; }
    public ProviderLaunch? Launch { get; init; }
    public BattleNetMetadata? BattleNet { get; init; }
    public string? LocalIconPath { get; init; }
    public string? CoverUrl { get; init; }
    public int LocalIconIndex { get; init; }
    public IReadOnlyList<ProviderGameIdentity> IdentityAliases { get; init; } = [];
}
