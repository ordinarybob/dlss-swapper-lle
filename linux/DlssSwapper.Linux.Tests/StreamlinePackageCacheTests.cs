using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlinePackageCacheTests
{
    internal static void Run()
    {
        foreach (var invalid in new[] { "v2.9.extracting-abc", "v2.9.previous-abc", "v2.9-rc1", "latest", "..", "v2.9+build", "v2.999999999999" })
        {
            if (StreamlinePackageCache.TryGetVersion(invalid, out _))
                throw new InvalidOperationException($"Cache accepted unfinished or unsupported tag {invalid}.");
        }

        if (!StreamlinePackageCache.TryGetVersion("v2.12", out var shortVersion)
            || !StreamlinePackageCache.TryGetVersion("2.12.0.0", out var longVersion)
            || shortVersion != longVersion)
            throw new InvalidOperationException("Equivalent release versions were not normalized.");

        var root = Path.Combine(Path.GetTempPath(), "dlss-streamline-cache-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (StreamlinePackageCache.FindNewest(root, _ => true) is not null)
                throw new InvalidOperationException("Missing cache returned a package.");
            foreach (var tag in new[] { "v2.9", "v2.12", "v2.99", "v3.0.extracting-abc", "v4.0.previous-abc" })
                Directory.CreateDirectory(Path.Combine(root, tag));

            var newest = StreamlinePackageCache.FindNewest(root, path => Path.GetFileName(path) != "v2.99");
            if (Path.GetFileName(newest) != "v2.12")
                throw new InvalidOperationException("Cache did not select the newest complete stable numeric version.");
            if (StreamlinePackageCache.FindNewest(root, _ => false) is not null)
                throw new InvalidOperationException("Incomplete cache returned a package.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
