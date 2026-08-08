using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;

namespace DLSS_Swapper.Data;

internal static class GameAssetCandidatePathIndex
{
    const int EnumerationBufferSize = 64 * 1024;
    const string Wildcard = "*";

    // Relative directory shapes derived from confirmed game-asset paths. The first
    // component represents a game/project directory. Additional wildcards are kept
    // only for shapes shared by more than one game in the source data.
    static readonly string[] _directoryPatterns =
    [
        "",
        "*",
        @"*\*",
        @"*\.trex",
        @"*\seekeroffire_2",
        @"*\x64",
        @"*\x64_dx12",
        @"*\*\win64",
        @"*\binaries\*",
        @"*\binaries\win64",
        @"*\windows\x64",
        @"*\x64\dxr",
        @"*\x64\dxr2",
        @"*\x64\slinfo",
        @"*\binaries\win64r\amd_fidelityfx",
        @"*\binaries\win64r\streamline",
        @"*\binaries\win64r\xess",
        @"*\tscgame\binaries\win64",
        @"*\intel-xess-feature-test\bin\x64\xess_1_0",
        @"*\intel-xess-feature-test\bin\x64\xess_1_1",
        @"*\intel-xess-feature-test\bin\x64\xess_1_2",
        @"*\intel-xess-feature-test\bin\x64\xess_1_3",
        @"*\nvidia-dlss-test\nvidia-dlss-test-1\bin\x64",
        @"*\nvidia-dlss-test\nvidia-dlss-test-2\bin\x64",
        @"*\nvidia-dlss-test\nvidia-dlss-test-3\bin\x64",
        @"*\nvidia-dlss-test\nvidia-dlss-test-4\bin\x64",
        @"*\*\dlss\binaries\thirdparty\win64",
        @"*\*\streamline\binaries\thirdparty\win64",
        @"*\*\thirdparty\nvidia\ngx\win64",
        @"*\*\xess\binaries\thirdparty\win64",
        @"*\binaries\*\nvidia\ngx\win64",
        @"*\binaries\thirdparty\*\ngx\win64",
        @"*\binaries\thirdparty\nvidia\*\win64",
        @"*\binaries\thirdparty\nvidia\ngx\*",
        @"*\binaries\thirdparty\nvidia\ngx\win64",
        @"*\plugins\*\binaries\thirdparty\win64",
        @"*\plugins\dlss\*\thirdparty\win64",
        @"*\plugins\dlss\binaries\*\win64",
        @"*\plugins\dlss\binaries\thirdparty\*",
        @"*\plugins\dlss\binaries\thirdparty\win64",
        @"*\plugins\intel\binaries\thirdparty\win64",
        @"*\plugins\intel-xess\binaries\thirdparty\win64",
        @"*\plugins\streamline\*\thirdparty\win64",
        @"*\plugins\streamline\binaries\*\win64",
        @"*\plugins\streamline\binaries\thirdparty\*",
        @"*\plugins\streamline\binaries\thirdparty\win64",
        @"*\plugins\streamlinecore\binaries\thirdparty\win64",
        @"*\plugins\xess\*\thirdparty\win64",
        @"*\plugins\xess\binaries\*\win64",
        @"*\plugins\xess\binaries\thirdparty\*",
        @"*\plugins\xess\binaries\thirdparty\win64",
        @"*\plugins\xess_ue5.6_plugin_v2.1.1.1\binaries\thirdparty\win64",
        @"*\*\marketplace\dlss\binaries\thirdparty\win64",
        @"*\*\marketplace\xess\binaries\thirdparty\win64",
        @"*\*\nvidia\dlss\binaries\thirdparty\win64",
        @"*\plugins\*\dlss\binaries\thirdparty\win64",
        @"*\plugins\*\streamline\binaries\thirdparty\win64",
        @"*\plugins\*\xess\binaries\thirdparty\win64",
        @"*\plugins\dlss\binaries\thirdparty\win64\development",
        @"*\plugins\dlssplugin\dlss\binaries\thirdparty\win64",
        @"*\plugins\dlssplugin\streamlinecore\binaries\thirdparty\win64",
        @"*\plugins\frogwaresplugins\dlss\binaries\thirdparty\win64",
        @"*\plugins\marketplace\*\binaries\thirdparty\win64",
        @"*\plugins\marketplace\dlss\*\thirdparty\win64",
        @"*\plugins\marketplace\dlss\binaries\*\win64",
        @"*\plugins\marketplace\dlss\binaries\thirdparty\*",
        @"*\plugins\marketplace\dlss\binaries\thirdparty\win64",
        @"*\plugins\marketplace\streamline\binaries\thirdparty\win64",
        @"*\plugins\marketplace\xess\*\thirdparty\win64",
        @"*\plugins\marketplace\xess\binaries\*\win64",
        @"*\plugins\marketplace\xess\binaries\thirdparty\*",
        @"*\plugins\marketplace\xess\binaries\thirdparty\win64",
        @"*\plugins\nvidia\*\binaries\thirdparty\win64",
        @"*\plugins\nvidia\dlss\*\thirdparty\win64",
        @"*\plugins\nvidia\dlss\binaries\*\win64",
        @"*\plugins\nvidia\dlss\binaries\thirdparty\*",
        @"*\plugins\nvidia\dlss\binaries\thirdparty\win64",
        @"*\plugins\nvidia\dlss-plugin-4.26.1\binaries\thirdparty\win64",
        @"*\plugins\nvidia\streamline\binaries\thirdparty\win64",
        @"*\plugins\nvidiadlss\dlss\binaries\thirdparty\win64",
        @"*\plugins\shared\dlss\binaries\thirdparty\win64",
        @"*\plugins\shared\streamline\binaries\thirdparty\win64",
        @"*\plugins\shared\xess\binaries\thirdparty\win64",
        @"*\plugins\thirdparty\xess\binaries\thirdparty\win64",
        @"*\plugins\upscaling\dlss\binaries\thirdparty\win64",
        @"*\*\runtime\intel\xess\binaries\thirdparty\win64",
        @"*\*\runtime\nvidia\dlss\binaries\thirdparty\win64",
        @"*\*\runtime\nvidia\streamline\binaries\thirdparty\win64",
        @"*\hmdproject\plugins\nvidia\dlss\binaries\thirdparty\win64",
        @"*\plugins\*\intel\xess\binaries\thirdparty\win64",
        @"*\plugins\*\nvidia\dlss\binaries\thirdparty\win64",
        @"*\plugins\*\nvidia\streamline\binaries\thirdparty\win64",
        @"*\plugins\marketplace\intel\xess\binaries\thirdparty\win64",
        @"*\plugins\marketplace\nvidia\dlss\binaries\thirdparty\win64",
        @"*\plugins\marketplace\nvidia\streamline\binaries\thirdparty\win64",
        @"*\plugins\runtime\*\dlss\binaries\thirdparty\win64",
        @"*\plugins\runtime\*\streamline\binaries\thirdparty\win64",
        @"*\plugins\runtime\*\xess\binaries\thirdparty\win64",
        @"*\plugins\runtime\intel\*\binaries\thirdparty\win64",
        @"*\plugins\runtime\intel\xess\*\thirdparty\win64",
        @"*\plugins\runtime\intel\xess\binaries\*\win64",
        @"*\plugins\runtime\intel\xess\binaries\thirdparty\*",
        @"*\plugins\runtime\intel\xess\binaries\thirdparty\win64",
        @"*\plugins\runtime\nvidia\*\binaries\thirdparty\win64",
        @"*\plugins\runtime\nvidia\dlss\*\thirdparty\win64",
        @"*\plugins\runtime\nvidia\dlss\binaries\*\win64",
        @"*\plugins\runtime\nvidia\dlss\binaries\thirdparty\*",
        @"*\plugins\runtime\nvidia\dlss\binaries\thirdparty\win64",
        @"*\plugins\runtime\nvidia\streamline\*\thirdparty\win64",
        @"*\plugins\runtime\nvidia\streamline\binaries\*\win64",
        @"*\plugins\runtime\nvidia\streamline\binaries\thirdparty\*",
        @"*\plugins\runtime\nvidia\streamline\binaries\thirdparty\win64",
        @"*\plugins\runtime\nvidia\streamlinecore\binaries\thirdparty\win64",
        @"*\plugins\thirdparty\nvidia\dlss\binaries\thirdparty\win64",
        @"*\plugins\thirdparty\nvidia\streamline\binaries\thirdparty\win64",
        @"*\engine\plugins\*\nvidia\dlss\binaries\thirdparty\win64",
        @"*\engine\plugins\marketplace\*\dlss\binaries\thirdparty\win64",
        @"*\engine\plugins\marketplace\*\streamline\binaries\thirdparty\win64",
        @"*\engine\plugins\marketplace\dlss\dlss\binaries\thirdparty\win64",
        @"*\engine\plugins\marketplace\dlss\streamline\binaries\thirdparty\win64",
        @"*\engine\plugins\marketplace\nvidia\dlss\binaries\thirdparty\win64",
        @"*\engine\plugins\marketplace\nvidia\streamline\binaries\thirdparty\win64",
        @"*\engine\plugins\runtime\nvidia\dlss\binaries\thirdparty\win64",
    ];

    static readonly object _patternLock = new();
    static string[] _cachedCustomPatterns = [];
    static PatternNode _patternRoot = CreatePatternTree(_directoryPatterns);

    internal static IReadOnlyList<string> GetBuiltInDirectoryPatterns()
    {
        return _directoryPatterns;
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
        if (relativeDirectory == ".")
        {
            return true;
        }

        var components = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0
            || components.Any(static component => component is "." or ".."))
        {
            return false;
        }

        // Game/project folder names vary, but the layout beneath that first
        // directory is reusable. This is the same generalization used by the
        // built-in index and avoids learning a one-game-only absolute shape.
        components[0] = Wildcard;
        pattern = string.Join(Path.DirectorySeparatorChar, components);
        return true;
    }

    internal static int AddAdaptiveDirectoryPatterns(IEnumerable<string> patterns)
    {
        var builtInPatterns = _directoryPatterns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_patternLock)
        {
            var existingPatterns = Settings.Instance.CustomGameAssetDirectoryPatterns;
            var existingPatternSet = existingPatterns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newPatterns = patterns
                .Where(pattern => builtInPatterns.Contains(pattern) == false)
                .Where(pattern => existingPatternSet.Contains(pattern) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (newPatterns.Length == 0)
            {
                return 0;
            }

            var mergedPatterns = existingPatterns
                .Concat(newPatterns)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static pattern => pattern, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Settings.Instance.CustomGameAssetDirectoryPatterns = mergedPatterns;
            _cachedCustomPatterns = mergedPatterns;
            _patternRoot = CreatePatternTree(_directoryPatterns.Concat(mergedPatterns));
            return newPatterns.Length;
        }
    }

    internal static IReadOnlyList<DiscoveredGameAsset> EnumerateCandidates(
        string installPath,
        CancellationToken cancellationToken = default)
    {
        var patternRoot = GetPatternRoot();
        var results = new List<DiscoveredGameAsset>();
        var discoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (patternRoot.IsCandidate)
        {
            ProbeCandidateDirectory(installPath, results, discoveredPaths);
        }

        var pending = new Stack<PendingDirectory>();
        pending.Push(new PendingDirectory(installPath, [patternRoot]));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var directory in EnumerateDirectories(current.Path))
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
                    ProbeCandidateDirectory(directory, results, discoveredPaths);
                }

                if (nextStates.Exists(static node => node.HasChildren))
                {
                    pending.Push(new PendingDirectory(directory, nextStates));
                }
            }
        }

        return results;
    }

    static IEnumerable<string> EnumerateDirectories(string path)
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
                nextStates.Add(literalChild);
            }

            if (state.WildcardChild is not null && nextStates.Contains(state.WildcardChild) == false)
            {
                nextStates.Add(state.WildcardChild);
            }
        }

        return nextStates;
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
        var customPatterns = Settings.Instance.CustomGameAssetDirectoryPatterns;
        lock (_patternLock)
        {
            if (_cachedCustomPatterns.SequenceEqual(customPatterns, StringComparer.OrdinalIgnoreCase))
            {
                return _patternRoot;
            }

            _cachedCustomPatterns = customPatterns.ToArray();
            _patternRoot = CreatePatternTree(_directoryPatterns.Concat(_cachedCustomPatterns));
            return _patternRoot;
        }
    }

    static PatternNode CreatePatternTree(IEnumerable<string> patterns)
    {
        var root = new PatternNode();
        foreach (var pattern in patterns)
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

        return root;
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
