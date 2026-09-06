using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;

namespace DlssSwapper.Linux.Tests;

internal static class GameViewPolicyTests
{
    public static Task RunAsync()
    {
        var game = new SelectedGame("Game", "fixture", "10");
        foreach (var pair in new[] { (GameProvider.Epic, "Epic Games Store"), (GameProvider.Gog, "GOG"),
            (GameProvider.Ubisoft, "Ubisoft Connect"), (GameProvider.Ea, "EA App"),
            (GameProvider.BattleNet, "Battle.net"), (GameProvider.Xbox, "Xbox App") })
            if (GameViewPolicy.LibraryName(game with { ProviderIdentity = new(pair.Item1, "id") }, false) != pair.Item2)
                throw new Exception("Incorrect provider display name.");
        if (GameViewPolicy.LibraryName(game, true) != "Manually Added" || GameViewPolicy.LibraryName(game, false) != "Steam")
            throw new Exception("Manual/Steam display ownership changed.");
        foreach (var hidden in new[] { false, true })
        foreach (var only in new[] { false, true })
        foreach (var include in new[] { false, true })
            if (GameViewPolicy.MatchesHidden(hidden, only, include) != (only ? hidden : !hidden || include))
                throw new Exception("Include-hidden and hidden-only choices were conflated.");
        if (!GameViewPolicy.IsSteam(game, false)
            || GameViewPolicy.IsSteam(game, true)
            || GameViewPolicy.IsSteam(game with { SteamAppId = null }, false)
            || GameViewPolicy.IsSteam(game with { ProviderIdentity = new(GameProvider.Gog, "20") }, false))
            throw new Exception("Steam filter mixed manual/provider ownership.");
        if (GameViewPolicy.ShouldSelectAll([]) || GameViewPolicy.ShouldSelectAll([true, true])
            || !GameViewPolicy.ShouldSelectAll([false, false]) || !GameViewPolicy.ShouldSelectAll([true, false]))
            throw new Exception("Visible select/deselect decision failed.");
        return Task.CompletedTask;
    }
}
