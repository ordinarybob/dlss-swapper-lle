using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
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

            await ScanRootsAsync(requests).ConfigureAwait(false);
        }

        static async Task ScanRootsAsync(ScanRequest[] requests)
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();
            var roots = requests
                .GroupBy(static request => request.InstallPath, StringComparer.OrdinalIgnoreCase)
                .Select(static group => new RootScan(group.Key, group.ToArray()))
                .ToArray();
            var workerCount = Settings.Instance.RecursiveScanConcurrency;
            var activeWorkerCount = 0;
            var peakWorkerCount = 0;
            var volumes = string.Join(
                ", ",
                roots
                    .GroupBy(static root => GetVolumeKey(root.InstallPath), StringComparer.OrdinalIgnoreCase)
                    .Select(static group => $"{group.Key} ({group.Count():N0})"));

            Logger.Info($"Scanning {roots.Length:N0} game root(s) across {volumes} with {workerCount} worker(s).");
            await Parallel.ForEachAsync(
                roots,
                new ParallelOptions { MaxDegreeOfParallelism = workerCount },
                (root, _) =>
                {
                    var activeWorkers = Interlocked.Increment(ref activeWorkerCount);
                    UpdateMaximum(ref peakWorkerCount, activeWorkers);
                    try
                    {
                        if (Directory.Exists(root.InstallPath))
                        {
                            root.Complete(EnumerateTree(root.InstallPath));
                        }
                        else
                        {
                            root.Complete([]);
                        }
                    }
                    catch (Exception err)
                    {
                        root.Fail(err);
                        Logger.Error(err, $"Unable to scan game root {root.InstallPath}.");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref activeWorkerCount);
                    }

                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);

            Logger.Info(
                $"Scanned {roots.Length:N0} game root(s) in {startedAt.Elapsed.TotalSeconds:N2} seconds; " +
                $"peak workers: {peakWorkerCount}/{workerCount}.");
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

    sealed class RootScan
    {
        readonly ScanRequest[] _requests;

        internal string InstallPath { get; }

        internal RootScan(string installPath, ScanRequest[] requests)
        {
            InstallPath = installPath;
            _requests = requests;
        }

        internal void Complete(IReadOnlyList<DiscoveredGameAsset> assets)
        {
            var orderedAssets = assets
                .OrderBy(static asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var request in _requests)
            {
                request.Completion.TrySetResult(orderedAssets);
            }
        }

        internal void Fail(Exception error)
        {
            foreach (var request in _requests)
            {
                request.Completion.TrySetException(error);
            }
        }
    }
}
