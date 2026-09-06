namespace DlssSwapper.Linux.Cli.Platform;

public enum DiscoverySourceState { Complete, Partial, Unavailable }
public sealed record DiscoverySourceKey(string Kind, string Path);

// Completeness describes the source enumeration, not availability of game directories.
public sealed record DiscoverySourceOutcome(string Kind, string Path, DiscoverySourceState State, string? Detail = null);
