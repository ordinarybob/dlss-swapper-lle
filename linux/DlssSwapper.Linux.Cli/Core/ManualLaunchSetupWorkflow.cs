namespace DlssSwapper.Linux.Cli.Core;

public static class ManualLaunchSetupWorkflow
{
    public static void Save(PersistentLibrary library, string gameRoot, ManualGameLaunch launch)
    {
        var validated = launch.Validate();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        library.UpdateState(state =>
        {
            var game = state.ManualGames.SingleOrDefault(item => comparer.Equals(item.RootPath, gameRoot))
                ?? throw new IOException("This game is no longer in the manual library.");
            game.Launch = validated;
        });
    }

    public static void RememberOffer(PersistentLibrary library, bool setup) => library.UpdateState(state =>
    {
        state.DontShowManualLaunchPrompt = true;
        state.SetupManualLaunchOnImport = setup;
    });
}
