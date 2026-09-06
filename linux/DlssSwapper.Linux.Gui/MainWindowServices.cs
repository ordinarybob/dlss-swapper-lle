using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Gui;

// Boundary injection keeps integration fixtures out of the user's libraries and network.
internal sealed record MainWindowServices(
    Func<LibraryStartupResult> Load,
    Func<CancellationToken, Task<SteamDiscoveryResult>> DiscoverSteam,
    Func<LinuxLibraryState, CancellationToken, ProviderDiscoveryResult> DiscoverProviders,
    Func<ArtworkService?> CreateArtwork);
