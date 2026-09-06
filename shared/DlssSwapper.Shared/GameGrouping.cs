namespace DlssSwapper.Shared;

public sealed record GameGroup<T>(string Name, IReadOnlyList<T> Items)
{
    public bool HasHeader => Name.Length > 0;
}

public static class GameGrouping
{
    public static IReadOnlyList<GameGroup<T>> Build<T>(IEnumerable<T> visible, Func<T, bool> favorite,
        Func<T, string> library, bool grouped, IReadOnlyList<string>? libraryOrder = null)
    {
        var rows = visible.ToArray();
        var result = new List<GameGroup<T>>();
        var favorites = rows.Where(favorite).ToArray();
        if (favorites.Length > 0) result.Add(new("Favourites", favorites));
        if (grouped)
            result.AddRange(rows.GroupBy(library).OrderBy(group => libraryOrder is null ? 0 :
                    libraryOrder.Contains(group.Key) ? Array.IndexOf(libraryOrder.ToArray(), group.Key) : int.MaxValue)
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new GameGroup<T>(group.Key, group.ToArray())));
        else if (rows.Length > 0) result.Add(new("", rows));
        return result;
    }
}
