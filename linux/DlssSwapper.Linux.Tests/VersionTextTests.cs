using DlssSwapper.Shared;

namespace DlssSwapper.Linux.Tests;

internal static class VersionTextTests
{
    public static Task RunAsync()
    {
        var comparer = VersionTextComparer.Instance;
        foreach (var pair in new[] { ("3.10", "3.9"), ("310.9", "31.10"), ("2,12,0,0", "2.9.0.0"), ("99999999999999999999.1", "99.1"), ("v2.1", "unknown") })
            if (comparer.Compare(pair.Item1, pair.Item2) <= 0 || comparer.Compare(pair.Item2, pair.Item1) >= 0)
                throw new Exception("Incorrect numeric order: " + pair);
        if (comparer.Compare("v2.12", "2.12.0.0") != 0 || comparer.Compare(null, "1.0") >= 0)
            throw new Exception("Missing version or padding order failed.");
        return Task.CompletedTask;
    }
}
