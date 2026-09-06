namespace DlssSwapper.Linux.Cli.Core;

public static class ManualGameTitle
{
    public static string Save(PersistentLibrary library, string rootPath, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        return library.UpdateState(state =>
        {
            var game = state.ManualGames.FirstOrDefault(item => PathComparers.FileSystemPath.Equals(item.RootPath, root))
                ?? throw new InvalidOperationException("Only manually added games can be renamed.");
            game.Name = title.Trim();
            return game.Name;
        });
    }
}
