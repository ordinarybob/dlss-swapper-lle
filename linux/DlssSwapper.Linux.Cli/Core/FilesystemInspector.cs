namespace DlssSwapper.Linux.Cli.Core;

public sealed record LibraryFilesystemInfo(
    string Path,
    string MountPoint,
    string Type,
    string? Warning)
{
    public bool IsBtrfs => Type.Equals("btrfs", StringComparison.OrdinalIgnoreCase);

    public bool IsExt4 => Type.Equals("ext4", StringComparison.OrdinalIgnoreCase);
}

public static class FilesystemInspector
{
    public static IReadOnlyList<LibraryFilesystemInfo> InspectPaths(
        IEnumerable<string> paths,
        string? mountInfoText = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var requested = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(PathComparers.FileSystemPath)
            .ToArray();
        if (requested.Length == 0)
        {
            return [];
        }

        if (mountInfoText is null && !OperatingSystem.IsLinux())
        {
            return requested.Select(InspectDrive).ToArray();
        }

        mountInfoText ??= File.ReadAllText("/proc/self/mountinfo");
        var mounts = ParseMountInfo(mountInfoText);
        return requested.Select(path => InspectLinuxPath(path, mounts)).ToArray();
    }

    private static LibraryFilesystemInfo InspectDrive(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath) ?? fullPath;
        var format = "unknown";
        try
        {
            format = new DriveInfo(root).DriveFormat;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            // Reporting must never prevent library discovery.
        }

        return new LibraryFilesystemInfo(fullPath, root, format, null);
    }

    private static LibraryFilesystemInfo InspectLinuxPath(
        string path,
        IReadOnlyList<MountEntry> mounts)
    {
        var normalized = NormalizeLinuxPath(path);
        var mount = mounts
            .Where(item => IsWithinMount(normalized, item.MountPoint))
            .OrderByDescending(item => item.MountPoint.Length)
            .FirstOrDefault();
        if (mount is null)
        {
            return new LibraryFilesystemInfo(
                normalized,
                "unknown",
                "unknown",
                "Filesystem type could not be determined.");
        }

        var type = mount.Type;
        var warning = IsNtfsOrFuse(type)
            ? $"{normalized} is on {type} at {mount.MountPoint}. NTFS/FUSE game libraries can have lower metadata performance and weaker Linux permission semantics than ext4 or Btrfs."
            : null;
        return new LibraryFilesystemInfo(normalized, mount.MountPoint, type, warning);
    }

    private static IReadOnlyList<MountEntry> ParseMountInfo(string text)
    {
        var mounts = new List<MountEntry>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var separator = Array.IndexOf(fields, "-");
            if (separator < 6 || separator + 1 >= fields.Length)
            {
                continue;
            }

            mounts.Add(new MountEntry(
                NormalizeLinuxPath(DecodeMountField(fields[4])),
                fields[separator + 1]));
        }

        return mounts;
    }

    private static string DecodeMountField(string value)
    {
        return value
            .Replace("\\040", " ", StringComparison.Ordinal)
            .Replace("\\011", "\t", StringComparison.Ordinal)
            .Replace("\\012", "\n", StringComparison.Ordinal)
            .Replace("\\134", "\\", StringComparison.Ordinal);
    }

    private static string NormalizeLinuxPath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = Path.GetFullPath(normalized).Replace('\\', '/');
        }

        return normalized.Length == 1 ? normalized : normalized.TrimEnd('/');
    }

    private static bool IsWithinMount(string path, string mountPoint) =>
        mountPoint == "/"
            ? path.StartsWith("/", StringComparison.Ordinal)
            : path.Equals(mountPoint, StringComparison.Ordinal)
                || path.StartsWith(mountPoint + "/", StringComparison.Ordinal);

    private static bool IsNtfsOrFuse(string type) =>
        type.Contains("ntfs", StringComparison.OrdinalIgnoreCase)
        || type.StartsWith("fuse", StringComparison.OrdinalIgnoreCase);

    private sealed record MountEntry(string MountPoint, string Type);
}
