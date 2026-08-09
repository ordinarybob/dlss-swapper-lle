namespace DlssSwapper.Linux.Cli.Core;

public sealed record CandidateFileResult(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Warnings);

public static class FastScanPatternIndex
{
    private const string Wildcard = "*";

    private static readonly string[] DirectoryPatterns =
    [
        "",
        "*",
        "*/*",
        "*/*/win64",
        "*/binaries/*",
        "*/windows/x64",
        "*/x64/dxr",
        "*/x64/dxr2",
        "*/x64/slinfo",
        "*/binaries/win64r/amd_fidelityfx",
        "*/binaries/win64r/streamline",
        "*/binaries/win64r/xess",
        "*/tscgame/binaries/win64",
        "*/intel-xess-feature-test/bin/x64/xess_1_0",
        "*/intel-xess-feature-test/bin/x64/xess_1_1",
        "*/intel-xess-feature-test/bin/x64/xess_1_2",
        "*/intel-xess-feature-test/bin/x64/xess_1_3",
        "*/nvidia-dlss-test/nvidia-dlss-test-1/bin/x64",
        "*/nvidia-dlss-test/nvidia-dlss-test-2/bin/x64",
        "*/nvidia-dlss-test/nvidia-dlss-test-3/bin/x64",
        "*/nvidia-dlss-test/nvidia-dlss-test-4/bin/x64",
        "*/*/dlss/binaries/thirdparty/win64",
        "*/*/streamline/binaries/thirdparty/win64",
        "*/*/thirdparty/nvidia/ngx/win64",
        "*/*/xess/binaries/thirdparty/win64",
        "*/binaries/*/nvidia/ngx/win64",
        "*/binaries/thirdparty/*/ngx/win64",
        "*/binaries/thirdparty/nvidia/*/win64",
        "*/binaries/thirdparty/nvidia/ngx/*",
        "*/plugins/*/binaries/thirdparty/win64",
        "*/plugins/dlss/*/thirdparty/win64",
        "*/plugins/dlss/binaries/*/win64",
        "*/plugins/dlss/binaries/thirdparty/*",
        "*/plugins/streamline/*/thirdparty/win64",
        "*/plugins/streamline/binaries/*/win64",
        "*/plugins/streamline/binaries/thirdparty/*",
        "*/plugins/xess/*/thirdparty/win64",
        "*/plugins/xess/binaries/*/win64",
        "*/plugins/xess/binaries/thirdparty/*",
        "*/*/marketplace/dlss/binaries/thirdparty/win64",
        "*/*/marketplace/xess/binaries/thirdparty/win64",
        "*/*/nvidia/dlss/binaries/thirdparty/win64",
        "*/plugins/*/dlss/binaries/thirdparty/win64",
        "*/plugins/*/streamline/binaries/thirdparty/win64",
        "*/plugins/*/xess/binaries/thirdparty/win64",
        "*/plugins/dlss/binaries/thirdparty/win64/development",
        "*/plugins/dlssplugin/streamlinecore/binaries/thirdparty/win64",
        "*/plugins/marketplace/*/binaries/thirdparty/win64",
        "*/plugins/marketplace/dlss/*/thirdparty/win64",
        "*/plugins/marketplace/dlss/binaries/*/win64",
        "*/plugins/marketplace/dlss/binaries/thirdparty/*",
        "*/plugins/marketplace/xess/*/thirdparty/win64",
        "*/plugins/marketplace/xess/binaries/*/win64",
        "*/plugins/marketplace/xess/binaries/thirdparty/*",
        "*/plugins/nvidia/*/binaries/thirdparty/win64",
        "*/plugins/nvidia/dlss/*/thirdparty/win64",
        "*/plugins/nvidia/dlss/binaries/*/win64",
        "*/plugins/nvidia/dlss/binaries/thirdparty/*",
        "*/*/runtime/intel/xess/binaries/thirdparty/win64",
        "*/*/runtime/nvidia/dlss/binaries/thirdparty/win64",
        "*/*/runtime/nvidia/streamline/binaries/thirdparty/win64",
        "*/hmdproject/plugins/nvidia/dlss/binaries/thirdparty/win64",
        "*/plugins/*/intel/xess/binaries/thirdparty/win64",
        "*/plugins/*/nvidia/dlss/binaries/thirdparty/win64",
        "*/plugins/*/nvidia/streamline/binaries/thirdparty/win64",
        "*/plugins/runtime/*/dlss/binaries/thirdparty/win64",
        "*/plugins/runtime/*/streamline/binaries/thirdparty/win64",
        "*/plugins/runtime/*/xess/binaries/thirdparty/win64",
        "*/plugins/runtime/intel/*/binaries/thirdparty/win64",
        "*/plugins/runtime/intel/xess/*/thirdparty/win64",
        "*/plugins/runtime/intel/xess/binaries/*/win64",
        "*/plugins/runtime/intel/xess/binaries/thirdparty/*",
        "*/plugins/runtime/nvidia/*/binaries/thirdparty/win64",
        "*/plugins/runtime/nvidia/dlss/*/thirdparty/win64",
        "*/plugins/runtime/nvidia/dlss/binaries/*/win64",
        "*/plugins/runtime/nvidia/dlss/binaries/thirdparty/*",
        "*/plugins/runtime/nvidia/streamline/*/thirdparty/win64",
        "*/plugins/runtime/nvidia/streamline/binaries/*/win64",
        "*/plugins/runtime/nvidia/streamline/binaries/thirdparty/*",
        "*/engine/plugins/*/nvidia/dlss/binaries/thirdparty/win64",
        "*/engine/plugins/marketplace/*/dlss/binaries/thirdparty/win64",
        "*/engine/plugins/marketplace/*/streamline/binaries/thirdparty/win64",
    ];

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
        if (relative == ".")
        {
            return true;
        }

        var components = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0 || components.Any(component => component is "." or ".."))
        {
            return false;
        }

        components[0] = Wildcard;
        pattern = string.Join('/', components).ToLowerInvariant();
        return true;
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
            if (!DllTypes.TryFromFileName(path, out _))
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

    private static string[] RemoveSubsumedPatterns(IEnumerable<string> patterns)
    {
        var normalized = patterns
            .Select(NormalizePattern)
            .Where(pattern => pattern is not null)
            .Select(pattern => pattern!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized
            .Where(pattern => !normalized.Any(other =>
                !other.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                && PatternSubsumes(other, pattern)))
            .ToArray();
    }

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

    private static bool PatternSubsumes(string broader, string narrower)
    {
        var broaderComponents = broader.Length == 0 ? [] : broader.Split('/');
        var narrowerComponents = narrower.Length == 0 ? [] : narrower.Split('/');
        return broaderComponents.Length == narrowerComponents.Length
            && broaderComponents.Zip(narrowerComponents).All(pair =>
                pair.First == Wildcard
                || pair.First.Equals(pair.Second, StringComparison.OrdinalIgnoreCase));
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
