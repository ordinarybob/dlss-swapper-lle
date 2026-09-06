namespace DlssSwapper.Linux.Cli.Core;

public static class GameViewPolicy
{
    public static List<string> NormalizeIgnoredPaths(IEnumerable<string> paths) => paths.Select(path =>
    {
        if (!Path.IsPathFullyQualified(path)) throw new IOException("Ignored folders must use absolute paths.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }).Distinct(PathComparers.FileSystemPath).ToList();

    public static bool IsIgnored(LinuxLibraryState state, string gameRoot)
    {
        if (state.IgnoredPaths.Count == 0) return false;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        return state.IgnoredPaths.Any(path =>
        {
            var ignored = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return PathComparers.FileSystemPath.Equals(root, ignored)
                || root.StartsWith(Path.EndsInDirectorySeparator(ignored) ? ignored : ignored + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        });
    }

    public static string LibraryName(SelectedGame game, bool isManual) => isManual ? "Manually Added"
        : game.ProviderIdentity?.Provider switch
        {
            Platform.GameProvider.Epic => "Epic Games Store",
            Platform.GameProvider.Gog => "GOG",
            Platform.GameProvider.Ubisoft => "Ubisoft Connect",
            Platform.GameProvider.Ea => "EA App",
            Platform.GameProvider.BattleNet => "Battle.net",
            Platform.GameProvider.Xbox => "Xbox App",
            _ => game.SteamAppId is not null ? "Steam" : "Other games",
        };

    public static bool MatchesHidden(bool isHidden, bool hiddenOnly, bool includeHidden) =>
        hiddenOnly ? isHidden : includeHidden || !isHidden;

    public static bool IsSteam(SelectedGame game, bool isManual) =>
        !isManual && !string.IsNullOrWhiteSpace(game.SteamAppId) && game.ProviderIdentity is null;

    public static bool ShouldSelectAll(IEnumerable<bool> visibleSelection)
    {
        var values = visibleSelection.ToArray();
        return values.Length > 0 && values.Any(selected => !selected);
    }
}
