using System;
using DlssSwapper.Shared;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading;

namespace DLSS_Swapper.Data;

internal static class GameAssetCandidatePathIndex
{
    const int EnumerationBufferSize = 64 * 1024;
    const string Wildcard = "*";

    static readonly string[] _directoryPatterns = ScanPatternRules.BuiltInPatterns
        .Select(pattern => pattern.Replace('/', Path.DirectorySeparatorChar)).ToArray();

    static readonly object _patternLock = new();
    static string[] _cachedCustomPatterns = [];
    static PatternNode _patternRoot = CreatePatternGraph(_directoryPatterns);

    internal static IReadOnlyList<string> GetBuiltInDirectoryPatterns()
    {
        return _directoryPatterns;
    }

    internal static string[] NormalizeCustomDirectoryPatterns(IEnumerable<string> patterns)
    {
        var builtInPatterns = _directoryPatterns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return RemoveSubsumedPatterns(_directoryPatterns.Concat(patterns))
            .Where(pattern => builtInPatterns.Contains(pattern) == false)
            .OrderBy(static pattern => pattern, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool TryCreateAdaptiveDirectoryPattern(
        string installPath,
        string assetPath,
        out string pattern)
    {
        pattern = string.Empty;
        var assetDirectory = Path.GetDirectoryName(Path.GetFullPath(assetPath));
        if (assetDirectory is null)
        {
            return false;
        }

        var relativeDirectory = Path.GetRelativePath(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(installPath)),
            assetDirectory);
        return ScanPatternRules.TryCreateAdaptivePattern(relativeDirectory, Path.DirectorySeparatorChar, false, out pattern);
    }

    internal static int AddAdaptiveDirectoryPatterns(IEnumerable<string> patterns)
    {
        lock (_patternLock)
        {
            var existingPatterns = Settings.Instance.CustomGameAssetDirectoryPatterns;
            var normalizedExistingPatterns = NormalizeCustomDirectoryPatterns(existingPatterns);
            var mergedPatterns = NormalizeCustomDirectoryPatterns(existingPatterns.Concat(patterns));
            var newPatternCount = mergedPatterns
                .Except(normalizedExistingPatterns, StringComparer.OrdinalIgnoreCase)
                .Count();

            if (existingPatterns.SequenceEqual(mergedPatterns, StringComparer.OrdinalIgnoreCase) == false)
            {
                Settings.Instance.CustomGameAssetDirectoryPatterns = mergedPatterns;
            }

            _cachedCustomPatterns = mergedPatterns;
            _patternRoot = CreatePatternGraph(_directoryPatterns.Concat(mergedPatterns));
            return newPatternCount;
        }
    }

    internal static IReadOnlyList<DiscoveredGameAsset> EnumerateCandidates(
        string installPath,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredGameAsset>();
        var discoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in EnumerateCandidateDirectories(installPath, cancellationToken))
            ProbeCandidateDirectory(directory, results, discoveredPaths);
        return results;
    }

    // Shared directory discovery for asset scanning and manual launch setup.
    internal static IEnumerable<string> EnumerateCandidateDirectories(
        string installPath, CancellationToken cancellationToken = default)
    {
        var patternRoot = GetPatternRoot();
        if (patternRoot.IsCandidate)
        {
            yield return installPath;
        }

        var pending = new Stack<PendingDirectory>();
        pending.Push(new PendingDirectory(installPath, [patternRoot]));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var directory in EnumerateNextDirectories(current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directoryName = Path.GetFileName(directory);
                var nextStates = GetNextStates(current.States, directoryName);
                if (nextStates.Count == 0)
                {
                    continue;
                }

                if (nextStates.Exists(static node => node.IsCandidate))
                {
                    yield return directory;
                }

                if (nextStates.Exists(static node => node.HasChildren))
                {
                    pending.Push(new PendingDirectory(directory, nextStates));
                }
            }
        }

    }

    static IEnumerable<string> EnumerateNextDirectories(PendingDirectory current)
    {
        if (current.States.Any(static state => state.WildcardChild is not null))
        {
            foreach (var directory in EnumerateDirectories(current.Path))
            {
                yield return directory;
            }

            yield break;
        }

        var literalDirectoryNames = current.States
            .SelectMany(static state => state.LiteralChildren.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var directoryName in literalDirectoryNames)
        {
            var directory = Path.Combine(current.Path, directoryName);
            var isCandidateDirectory = false;
            try
            {
                var attributes = File.GetAttributes(directory);
                isCandidateDirectory = (attributes & FileAttributes.Directory) != 0
                    && (attributes & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception err) when (err is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException)
            {
                // A missing or inaccessible literal path is simply not a candidate.
            }

            if (isCandidateDirectory)
            {
                yield return directory;
            }
        }
    }

    static FileSystemEnumerable<string> EnumerateDirectories(string path)
    {
        var directories = new FileSystemEnumerable<string>(
            path,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.ReparsePoint,
                BufferSize = EnumerationBufferSize,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => entry.IsDirectory,
        };

        return directories;
    }

    static List<PatternNode> GetNextStates(IReadOnlyList<PatternNode> states, string directoryName)
    {
        var nextStates = new List<PatternNode>();
        foreach (var state in states)
        {
            if (state.LiteralChildren.TryGetValue(directoryName, out var literalChild))
            {
                AddNextState(nextStates, literalChild);
            }

            if (state.WildcardChild is not null)
            {
                AddNextState(nextStates, state.WildcardChild);
            }
        }

        return nextStates;
    }

    static void AddNextState(List<PatternNode> states, PatternNode state)
    {
        if (states.Contains(state) == false)
        {
            states.Add(state);
        }
    }

    static void ProbeCandidateDirectory(
        string directory,
        List<DiscoveredGameAsset> results,
        HashSet<string> discoveredPaths)
    {
        foreach (var assetFile in GameAssetPathIndex.GetAssetFiles())
        {
            var path = Path.Combine(directory, assetFile.FileName);
            if (File.Exists(path)
                && discoveredPaths.Add(path))
            {
                results.Add(new DiscoveredGameAsset(path, assetFile.AssetType));
            }
        }
    }

    static PatternNode GetPatternRoot()
    {
        var configuredCustomPatterns = Settings.Instance.CustomGameAssetDirectoryPatterns;
        var customPatterns = NormalizeCustomDirectoryPatterns(configuredCustomPatterns);
        lock (_patternLock)
        {
            if (configuredCustomPatterns.SequenceEqual(customPatterns, StringComparer.OrdinalIgnoreCase) == false)
            {
                Settings.Instance.CustomGameAssetDirectoryPatterns = customPatterns;
            }

            if (_cachedCustomPatterns.SequenceEqual(customPatterns, StringComparer.OrdinalIgnoreCase))
            {
                return _patternRoot;
            }

            _cachedCustomPatterns = customPatterns.ToArray();
            _patternRoot = CreatePatternGraph(_directoryPatterns.Concat(_cachedCustomPatterns));
            return _patternRoot;
        }
    }

    static string[] RemoveSubsumedPatterns(IEnumerable<string> patterns) =>
        ScanPatternRules.RemoveSubsumed(patterns,
            static pattern => string.IsNullOrWhiteSpace(pattern) && pattern.Length != 0
                ? null
                : pattern.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                    .Trim(Path.DirectorySeparatorChar),
            Path.DirectorySeparatorChar);

    static PatternNode CreatePatternGraph(IEnumerable<string> patterns)
    {
        var root = new PatternNode();
        foreach (var pattern in RemoveSubsumedPatterns(patterns))
        {
            var current = root;
            if (pattern.Length > 0)
            {
                foreach (var component in pattern.Split(Path.DirectorySeparatorChar))
                {
                    current = component == Wildcard
                        ? current.WildcardChild ??= new PatternNode()
                        : current.GetOrAddLiteral(component);
                }
            }

            current.IsCandidate = true;
        }

        return CanonicalizePatternTree(root);
    }

    static PatternNode CanonicalizePatternTree(PatternNode root)
    {
        var canonicalNodes = new Dictionary<string, PatternNode>(StringComparer.Ordinal);
        var canonicalNodeIds = new Dictionary<PatternNode, int>();

        PatternNode Canonicalize(PatternNode node)
        {
            var wildcardChild = node.WildcardChild is null
                ? null
                : Canonicalize(node.WildcardChild);
            var literalChildren = node.LiteralChildren
                .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new KeyValuePair<string, PatternNode>(pair.Key, Canonicalize(pair.Value)))
                .ToArray();

            var signature = new StringBuilder()
                .Append(node.IsCandidate ? '1' : '0')
                .Append('|')
                .Append(wildcardChild is null ? -1 : canonicalNodeIds[wildcardChild])
                .Append('|')
                .Append(literalChildren.Length)
                .Append('|');
            foreach (var child in literalChildren)
            {
                var normalizedName = child.Key.ToUpperInvariant();
                signature.Append(normalizedName.Length)
                    .Append(':')
                    .Append(normalizedName)
                    .Append(':')
                    .Append(canonicalNodeIds[child.Value])
                    .Append('|');
            }

            var signatureText = signature.ToString();
            if (canonicalNodes.TryGetValue(signatureText, out var canonicalNode))
            {
                return canonicalNode;
            }

            node.WildcardChild = wildcardChild;
            node.LiteralChildren.Clear();
            foreach (var child in literalChildren)
            {
                node.LiteralChildren.Add(child.Key, child.Value);
            }

            canonicalNodes.Add(signatureText, node);
            canonicalNodeIds.Add(node, canonicalNodeIds.Count);
            return node;
        }

        return Canonicalize(root);
    }

    readonly record struct PendingDirectory(string Path, List<PatternNode> States);

    sealed class PatternNode
    {
        internal Dictionary<string, PatternNode> LiteralChildren { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        internal PatternNode? WildcardChild { get; set; }
        internal bool IsCandidate { get; set; }
        internal bool HasChildren => LiteralChildren.Count > 0 || WildcardChild is not null;

        internal PatternNode GetOrAddLiteral(string component)
        {
            if (LiteralChildren.TryGetValue(component, out var child) == false)
            {
                child = new PatternNode();
                LiteralChildren.Add(component, child);
            }

            return child;
        }
    }
}
