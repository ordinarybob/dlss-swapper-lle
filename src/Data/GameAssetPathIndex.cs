using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

internal readonly record struct DiscoveredGameAsset(string Path, GameAssetType AssetType);

internal static class GameAssetPathIndex
{
    const int EnumerationBufferSize = 64 * 1024;

    static readonly object _sessionLock = new();
    static ScanSession? _currentSession;

    internal static ScanBatch BeginBatch()
    {
        lock (_sessionLock)
        {
            if (_currentSession is not null)
            {
                throw new InvalidOperationException("A game asset scan batch is already active.");
            }

            _currentSession = new ScanSession();
            return new ScanBatch(_currentSession);
        }
    }

    internal static PreparedAssetScan PrepareFind(string installPath)
    {
        ScanSession? session;
        lock (_sessionLock)
        {
            session = _currentSession;
        }

        return new PreparedAssetScan(installPath, session?.Register(installPath));
    }

    internal static bool TryGetAssetType(ReadOnlySpan<char> fileName, out GameAssetType assetType)
    {
        if (fileName.Equals("nvngx_dlss.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.DLSS;
            return true;
        }
        if (fileName.Equals("nvngx_dlssg.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.DLSS_G;
            return true;
        }
        if (fileName.Equals("nvngx_dlssd.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.DLSS_D;
            return true;
        }
        if (fileName.Equals("amd_fidelityfx_dx12.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.FSR_31_DX12;
            return true;
        }
        if (fileName.Equals("amd_fidelityfx_vk.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.FSR_31_VK;
            return true;
        }
        if (fileName.Equals("libxess.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.XeSS;
            return true;
        }
        if (fileName.Equals("libxess_dx11.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.XeSS_DX11;
            return true;
        }
        if (fileName.Equals("libxell.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.XeLL;
            return true;
        }
        if (fileName.Equals("libxess_fg.dll", StringComparison.OrdinalIgnoreCase))
        {
            assetType = GameAssetType.XeSS_FG;
            return true;
        }

        assetType = GameAssetType.Unknown;
        return false;
    }

    static IReadOnlyList<DiscoveredGameAsset> EnumerateTree(string installPath)
    {
        var options = CreateEnumerationOptions(recurseSubdirectories: true);
        var files = new FileSystemEnumerable<DiscoveredGameAsset>(
            installPath,
            (ref FileSystemEntry entry) => new DiscoveredGameAsset(
                entry.ToFullPath(),
                GetAssetType(entry.FileName)),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                entry.IsDirectory == false && TryGetAssetType(entry.FileName, out _),
        };

        return files.ToArray();
    }

    static EnumerationOptions CreateEnumerationOptions(bool recurseSubdirectories)
    {
        return new EnumerationOptions
        {
            RecurseSubdirectories = recurseSubdirectories,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            BufferSize = EnumerationBufferSize,
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
        };
    }

    static GameAssetType GetAssetType(ReadOnlySpan<char> fileName)
    {
        if (TryGetAssetType(fileName, out var assetType))
        {
            return assetType;
        }

        throw new InvalidOperationException($"Unexpected game asset filename: {fileName.ToString()}");
    }

    static string NormalizePath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    static string GetVolumeKey(string path)
    {
        return Path.GetPathRoot(path) ?? path;
    }

    static bool IsAtOrUnder(string path, string root)
    {
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Length > root.Length
            && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && (root.EndsWith(Path.DirectorySeparatorChar)
                || path[root.Length] == Path.DirectorySeparatorChar
                || path[root.Length] == Path.AltDirectorySeparatorChar);
    }

    internal sealed class PreparedAssetScan
    {
        readonly string _installPath;
        readonly Task<IReadOnlyList<DiscoveredGameAsset>>? _batchResult;

        internal PreparedAssetScan(string installPath, Task<IReadOnlyList<DiscoveredGameAsset>>? batchResult)
        {
            _installPath = installPath;
            _batchResult = batchResult;
        }

        internal Task<IReadOnlyList<DiscoveredGameAsset>> ExecuteAsync()
        {
            if (_batchResult is not null)
            {
                return _batchResult;
            }

            return Task.FromResult(EnumerateTree(_installPath));
        }
    }

    internal sealed class ScanBatch : IDisposable
    {
        ScanSession? _session;

        internal ScanBatch(ScanSession session)
        {
            _session = session;
        }

        internal Task CompleteAsync()
        {
            var session = _session ?? throw new ObjectDisposedException(nameof(ScanBatch));
            return session.CompleteAsync();
        }

        public void Dispose()
        {
            var session = Interlocked.Exchange(ref _session, null);
            if (session is null)
            {
                return;
            }

            session.Dispose();
            lock (_sessionLock)
            {
                if (ReferenceEquals(_currentSession, session))
                {
                    _currentSession = null;
                }
            }
        }
    }

    internal sealed class ScanSession : IDisposable
    {
        readonly object _lock = new();
        readonly Dictionary<string, ScanRequest> _requests = new(StringComparer.OrdinalIgnoreCase);
        bool _registrationComplete;
        bool _disposed;

        internal Task<IReadOnlyList<DiscoveredGameAsset>>? Register(string installPath)
        {
            var normalizedPath = NormalizePath(installPath);
            lock (_lock)
            {
                if (_registrationComplete || _disposed)
                {
                    return null;
                }

                if (_requests.TryGetValue(normalizedPath, out var existing))
                {
                    return existing.Completion.Task;
                }

                var request = new ScanRequest(normalizedPath);
                _requests.Add(normalizedPath, request);
                return request.Completion.Task;
            }
        }

        internal async Task CompleteAsync()
        {
            ScanRequest[] requests;
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_registrationComplete)
                {
                    return;
                }

                _registrationComplete = true;
                requests = _requests.Values.ToArray();
            }

            var volumeTasks = requests
                .GroupBy(static request => GetVolumeKey(request.InstallPath), StringComparer.OrdinalIgnoreCase)
                .Select(group => ScanVolumeAsync(group.Key, group.ToArray()));
            await Task.WhenAll(volumeTasks).ConfigureAwait(false);
        }

        static async Task ScanVolumeAsync(string volumeRoot, ScanRequest[] requests)
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var roots = requests
                    .Select(static request => request.InstallPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(static path => path.Length)
                    .ToArray();
                var scanRoots = GetScanRoots(roots);
                var results = roots.ToDictionary(
                    static path => path,
                    static _ => new ConcurrentBag<DiscoveredGameAsset>(),
                    StringComparer.OrdinalIgnoreCase);
                var workerCount = GetWorkerCount();

                Logger.Info($"Scanning {roots.Length:N0} game root(s) from {scanRoots.Length:N0} shared tree root(s) on {volumeRoot} with {workerCount} directory worker(s).");
                var statistics = await WalkTreesAsync(scanRoots, workerCount, asset =>
                {
                    foreach (var root in roots)
                    {
                        if (IsAtOrUnder(asset.Path, root))
                        {
                            results[root].Add(asset);
                            break;
                        }
                    }
                }).ConfigureAwait(false);

                foreach (var request in requests)
                {
                    var assets = results[request.InstallPath]
                        .OrderBy(static asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    request.Completion.TrySetResult(assets);
                }

                Logger.Info(
                    $"Scanned {roots.Length:N0} game root(s) and {statistics.Directories:N0} directories on {volumeRoot} " +
                    $"in {startedAt.Elapsed.TotalSeconds:N2} seconds; peak directory workers: {statistics.PeakWorkers}/{workerCount}, " +
                    $"peak pending directories: {statistics.PeakPendingDirectories:N0}.");
            }
            catch (Exception err)
            {
                foreach (var request in requests)
                {
                    request.Completion.TrySetException(err);
                }

                Logger.Error($"Unable to scan game roots on {volumeRoot}. {err}");
            }
        }

        static string[] GetScanRoots(string[] gameRoots)
        {
            var groupedRoots = gameRoots
                .GroupBy(static root => Path.GetDirectoryName(root) ?? root, StringComparer.OrdinalIgnoreCase);
            var candidateRoots = new List<string>();
            foreach (var group in groupedRoots)
            {
                if (group.Count() > 1)
                {
                    candidateRoots.Add(group.Key);
                }
                else
                {
                    candidateRoots.Add(group.First());
                }
            }

            var candidates = candidateRoots
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path.Length)
                .ToArray();
            var scanRoots = new List<string>(candidates.Length);

            foreach (var candidate in candidates)
            {
                if (scanRoots.Any(root => IsAtOrUnder(candidate, root)) == false)
                {
                    scanRoots.Add(candidate);
                }
            }

            return scanRoots.ToArray();
        }

        static int GetWorkerCount()
        {
            return Settings.Instance.RecursiveScanConcurrency;
        }

        static async Task<WalkStatistics> WalkTreesAsync(
            string[] roots,
            int workerCount,
            Action<DiscoveredGameAsset> onAsset)
        {
            var directories = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = workerCount == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
            var pendingDirectoryCount = roots.Length;
            var peakPendingDirectories = roots.Length;
            var activeWorkerCount = 0;
            var peakWorkerCount = 0;
            long scannedDirectoryCount = 0;

            foreach (var root in roots)
            {
                if (Directory.Exists(root))
                {
                    directories.Writer.TryWrite(root);
                }
                else if (Interlocked.Decrement(ref pendingDirectoryCount) == 0)
                {
                    directories.Writer.TryComplete();
                }
            }

            var workers = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(async () =>
                {
                    await foreach (var directory in directories.Reader.ReadAllAsync().ConfigureAwait(false))
                    {
                        var activeWorkers = Interlocked.Increment(ref activeWorkerCount);
                        UpdateMaximum(ref peakWorkerCount, activeWorkers);
                        Interlocked.Increment(ref scannedDirectoryCount);
                        try
                        {
                            foreach (var entry in EnumerateDirectory(directory))
                            {
                                if (entry.IsDirectory)
                                {
                                    var pendingDirectories = Interlocked.Increment(ref pendingDirectoryCount);
                                    UpdateMaximum(ref peakPendingDirectories, pendingDirectories);
                                    if (directories.Writer.TryWrite(entry.Path) == false)
                                    {
                                        Interlocked.Decrement(ref pendingDirectoryCount);
                                    }
                                }
                                else
                                {
                                    onAsset(entry.Asset!.Value);
                                }
                            }
                        }
                        catch (Exception err) when (err is IOException or UnauthorizedAccessException)
                        {
                            Logger.Warning($"Unable to enumerate {directory}. {err.Message}");
                        }
                        finally
                        {
                            Interlocked.Decrement(ref activeWorkerCount);
                            if (Interlocked.Decrement(ref pendingDirectoryCount) == 0)
                            {
                                directories.Writer.TryComplete();
                            }
                        }
                    }
                }))
                .ToArray();

            await Task.WhenAll(workers).ConfigureAwait(false);
            return new WalkStatistics(scannedDirectoryCount, peakWorkerCount, peakPendingDirectories);
        }

        static void UpdateMaximum(ref int target, int candidate)
        {
            var current = Volatile.Read(ref target);
            while (candidate > current)
            {
                var observed = Interlocked.CompareExchange(ref target, candidate, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }

        static IEnumerable<WalkEntry> EnumerateDirectory(string directory)
        {
            var entries = new FileSystemEnumerable<WalkEntry>(
                directory,
                (ref FileSystemEntry entry) => entry.IsDirectory
                    ? new WalkEntry(entry.ToFullPath(), true, null)
                    : new WalkEntry(
                        entry.ToFullPath(),
                        false,
                        new DiscoveredGameAsset(entry.ToFullPath(), GetAssetType(entry.FileName))),
                CreateEnumerationOptions(recurseSubdirectories: false))
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                    entry.IsDirectory || TryGetAssetType(entry.FileName, out _),
            };

            return entries;
        }

        public void Dispose()
        {
            ScanRequest[] requests;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                requests = _requests.Values.ToArray();
            }

            foreach (var request in requests)
            {
                request.Completion.TrySetCanceled();
            }
        }
    }

    sealed class ScanRequest
    {
        internal string InstallPath { get; }
        internal TaskCompletionSource<IReadOnlyList<DiscoveredGameAsset>> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ScanRequest(string installPath)
        {
            InstallPath = installPath;
        }
    }

    readonly record struct WalkEntry(string Path, bool IsDirectory, DiscoveredGameAsset? Asset);
    readonly record struct WalkStatistics(long Directories, int PeakWorkers, int PeakPendingDirectories);
}
