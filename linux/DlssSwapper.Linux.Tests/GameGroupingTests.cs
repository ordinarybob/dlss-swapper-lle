using DlssSwapper.Shared;

namespace DlssSwapper.Linux.Tests;

internal static class GameGroupingTests
{
    private sealed record Row(string Library, bool Favorite)
    {
        public bool Selected { get; set; }
    }
    public static Task RunAsync()
    {
        var favorite = new Row("Steam", true);
        var other = new Row("Gog", false);
        var groups = GameGrouping.Build(new[] { favorite, other }, row => row.Favorite, row => row.Library, true);
        if (groups.Count != 3 || groups[0].Name != "Favourites"
            || !ReferenceEquals(groups[0].Items[0], groups.Single(group => group.Name == "Steam").Items[0]))
            throw new Exception("Grouping cloned or lost the favourite row.");
        groups[0].Items[0].Selected = true;
        if (!groups.Single(group => group.Name == "Steam").Items[0].Selected
            || new[] { favorite, other }.Count(row => row.Selected) != 1)
            throw new Exception("Repeated favourite did not share selection or unique target count.");
        var flat = GameGrouping.Build(new[] { favorite, other }, row => row.Favorite, row => row.Library, false);
        if (flat.Count != 2 || flat[1].HasHeader || !flat[1].Items.SequenceEqual(new[] { favorite, other }))
            throw new Exception("Ungrouping lost input order or membership.");
        var filtered = GameGrouping.Build(new[] { other }, row => row.Favorite, row => row.Library, true);
        if (filtered.Count != 1 || filtered[0].Items.Count != 1)
            throw new Exception("Grouping resurrected filtered rows.");
        if (GameGrouping.Build(Array.Empty<Row>(), row => row.Favorite, row => row.Library, true).Count != 0)
            throw new Exception("Empty grouping created visible sections.");
        return Task.CompletedTask;
    }
}
