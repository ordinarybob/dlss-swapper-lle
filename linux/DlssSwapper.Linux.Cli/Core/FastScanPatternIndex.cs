using DlssSwapper.Shared;

namespace DlssSwapper.Linux.Cli.Core;
public sealed record CandidateFileResult(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Warnings);

public static class FastScanPatternIndex
{
    private const string Wildcard = "*";

    private static readonly IReadOnlyList<string> DirectoryPatterns = ScanPatternRules.BuiltInPatterns;

    public static IReadOnlyList<string> BuiltInPatterns => DirectoryPatterns;

    public static IReadOnlyList<string> NormalizeCustomPatterns(
        IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var builtIns = DirectoryPatterns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return RemoveSubsumedPatterns(DirectoryPatterns.Concat(patterns))
            .Where(pattern => !builtIns.Contains(pattern))
            .OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryNormalizePattern(string? pattern, out string normalized)
    {
        var value = NormalizePattern(pattern);
        normalized = value ?? string.Empty;
        return value is not null;
    }

    public static CandidateFileResult EnumerateFastCandidates(
        string gameRoot,
        IEnumerable<string>? customPatterns = null,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateRoot(gameRoot);
        var warnings = new List<string>();
        var files = new HashSet<string>(PathComparers.FileSystemPath);
        var graph = CreatePatternGraph(
            DirectoryPatterns.Concat(customPatterns ?? []));

        if (graph.IsCandidate)
        {
            ProbeCandidateDirectory(root, files, warnings);
        }

        var pending = new Stack<PendingDirectory>();
        pending.Push(new PendingDirectory(root, [graph]));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var directory in EnumerateDirectories(current.Path, warnings))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nextStates = GetNextStates(current.States, Path.GetFileName(directory));
                if (nextStates.Count == 0)
                {
                    continue;
                }

                if (nextStates.Any(node => node.IsCandidate))
                {
                    ProbeCandidateDirectory(directory, files, warnings);
                }

                if (nextStates.Any(node => node.HasChildren))
                {
                    pending.Push(new PendingDirectory(directory, nextStates));
                }
            }
        }

        return new CandidateFileResult(
            files.OrderBy(path => path, PathComparers.FileSystemPath).ToArray(),
            warnings);
    }

    public static CandidateFileResult EnumerateDeepCandidates(
        string gameRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateRoot(gameRoot);
        var warnings = new List<string>();
        var files = new HashSet<string>(PathComparers.FileSystemPath);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProbeCandidateDirectory(current, files, warnings);
            foreach (var directory in EnumerateDirectories(current, warnings))
            {
                pending.Push(directory);
            }
        }

        return new CandidateFileResult(
            files.OrderBy(path => path, PathComparers.FileSystemPath).ToArray(),
            warnings);
    }

    public static bool TryCreateAdaptivePattern(
        string gameRoot,
        string assetPath,
        out string pattern)
    {
        var root = ValidateRoot(gameRoot);
        var directory = Path.GetDirectoryName(Path.GetFullPath(assetPath));
        pattern = string.Empty;
        if (directory is null)
        {
            return false;
        }

        var relative = Path.GetRelativePath(root, directory);
        return ScanPatternRules.TryCreateAdaptivePattern(relative, '/', true, out pattern);
    }

    private static string ValidateRoot(string gameRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Game directory does not exist: {root}");
        }

        return root;
    }

    private static IEnumerable<string> EnumerateDirectories(
        string path,
        List<string> warnings)
    {
        string[] directories;
        try
        {
            directories = Directory.GetDirectories(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not enumerate '{path}': {exception.Message}");
            yield break;
        }

        foreach (var directory in directories)
        {
            bool isLink;
            try
            {
                isLink = PathSafety.IsSymbolicLink(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not inspect '{directory}': {exception.Message}");
                continue;
            }

            if (isLink)
            {
                warnings.Add($"Skipped symbolic link '{directory}'.");
                continue;
            }

            yield return directory;
        }
    }

    private static void ProbeCandidateDirectory(
        string directory,
        HashSet<string> files,
        List<string> warnings)
    {
        string[] candidates;
        try
        {
            candidates = Directory.GetFiles(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not inspect candidate directory '{directory}': {exception.Message}");
            return;
        }

        foreach (var path in candidates)
        {
            if (!DllTypes.TryFromFileName(path, out _)
                && !DLSS_Swapper.Data.Streamline.StreamlineComponentSet.FileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (PathSafety.IsSymbolicLink(path))
                {
                    warnings.Add($"Skipped symbolic link '{path}'.");
                    continue;
                }

                files.Add(Path.GetFullPath(path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not inspect '{path}': {exception.Message}");
            }
        }
    }

    private static List<PatternNode> GetNextStates(
        IReadOnlyList<PatternNode> states,
        string directoryName)
    {
        var result = new List<PatternNode>();
        foreach (var state in states)
        {
            if (state.LiteralChildren.TryGetValue(directoryName, out var literal))
            {
                AddDistinct(result, literal);
            }

            if (state.WildcardChild is not null)
            {
                AddDistinct(result, state.WildcardChild);
            }
        }

        return result;
    }

    private static void AddDistinct(List<PatternNode> nodes, PatternNode node)
    {
        if (!nodes.Contains(node))
        {
            nodes.Add(node);
        }
    }

    private static string[] RemoveSubsumedPatterns(IEnumerable<string> patterns) =>
        ScanPatternRules.RemoveSubsumed(patterns, NormalizePattern, '/');

    private static string? NormalizePattern(string? pattern)
    {
        if (pattern is null)
        {
            return null;
        }

        var normalized = pattern.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var components = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (components.Any(component => component is "." or ".."
                || component.Contains(':', StringComparison.Ordinal)))
        {
            return null;
        }

        return string.Join('/', components).ToLowerInvariant();
    }

    private static PatternNode CreatePatternGraph(IEnumerable<string> patterns)
    {
        var root = new PatternNode();
        foreach (var pattern in RemoveSubsumedPatterns(patterns))
        {
            var current = root;
            foreach (var component in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current = component == Wildcard
                    ? current.WildcardChild ??= new PatternNode()
                    : current.GetOrAddLiteral(component);
            }

            current.IsCandidate = true;
        }

        return root;
    }

    private readonly record struct PendingDirectory(
        string Path,
        IReadOnlyList<PatternNode> States);

    private sealed class PatternNode
    {
        internal Dictionary<string, PatternNode> LiteralChildren { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal PatternNode? WildcardChild { get; set; }

        internal bool IsCandidate { get; set; }

        internal bool HasChildren => LiteralChildren.Count > 0 || WildcardChild is not null;

        internal PatternNode GetOrAddLiteral(string component)
        {
            if (!LiteralChildren.TryGetValue(component, out var node))
            {
                node = new PatternNode();
                LiteralChildren.Add(component, node);
            }

            return node;
        }
    }
}
