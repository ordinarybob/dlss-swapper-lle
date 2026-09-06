using DLSS_Swapper.Data.ManuallyAdded;

namespace DlssSwapper.Linux.Cli.Core;

public static class LaunchSuggestions
{
    public static IReadOnlyList<ManualLaunchManifest.Candidate> Find(string root, string title, IEnumerable<string> patterns)
    {
        var scan = FastScanPatternIndex.EnumerateFastCandidates(root, patterns);
        var directories = scan.Files.Select(path => Path.GetDirectoryName(path)!).Distinct().ToArray();
        var windows = ManualLaunchManifest.FindCandidates(root, title, directories);
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 6,
            IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var native = Directory.EnumerateFiles(root, "*", options).Take(1000)
            .Concat(directories.SelectMany(directory => Directory.EnumerateFiles(directory, "*", new EnumerationOptions
            { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })))
            .Where(path => !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Where(ManualLaunchManifest.IsSuggestedExecutable).Where(IsNativeExecutable)
            .Select(path => new ManualLaunchManifest.Candidate(path, Path.GetRelativePath(root, path)));
        return ManualLaunchManifest.RankCandidates(windows.Concat(native).DistinctBy(item => item.Path), title);
    }

    public static string? FindWine(string? searchPath)
    {
        foreach (var directory in (searchPath ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Path.IsPathFullyQualified(directory)) continue;
            var file = Path.Combine(directory, "wine");
            if (File.Exists(file) && IsNativeExecutable(file)) return file;
        }
        return null;
    }

    private static bool IsNativeExecutable(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0) return false;
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            var count = stream.Read(header);
            return count >= 2 && header[0] == '#' && header[1] == '!' || count == 4 && header.SequenceEqual(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
