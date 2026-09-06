namespace DlssSwapper.Linux.Cli.Core;

public static class LibraryRemovalWorkflow
{
    public static void Remove(PersistentLibrary library, SelectedGame game)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(game);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(game.RootPath));
        library.UpdateState(state =>
        {
            if (state.ManualGames.RemoveAll(item => PathComparers.FileSystemPath.Equals(item.RootPath, root)) > 0)
            {
                state.GamePreferences.RemoveAll(item => PathComparers.FileSystemPath.Equals(item.RootPath, root));
                state.GameHistory.RemoveAll(item => PathComparers.FileSystemPath.Equals(item.RootPath, root));
            }
            else if (game.ProviderIdentity is { } identity)
            {
                state.ExcludedProviderGames ??= [];
                foreach (var alias in game.ProviderIdentityAliases.Append(identity))
                    if (!state.ExcludedProviderGames.Contains(alias)) state.ExcludedProviderGames.Add(alias);
            }
            else if (!string.IsNullOrWhiteSpace(game.SteamAppId))
            {
                var appId = game.SteamAppId.Trim();
                if (!state.ExcludedSteamAppIds.Contains(appId, StringComparer.Ordinal))
                    state.ExcludedSteamAppIds.Add(appId);
            }
            else
                throw new InvalidOperationException("The game has no manual registration or launcher identity to remove.");
        });
    }
}
