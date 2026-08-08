using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

internal readonly record struct DiscoveredGameAsset(string Path, GameAssetType AssetType);
internal readonly record struct GameAssetFileDefinition(string FileName, GameAssetType AssetType);

internal static class GameAssetPathIndex
{
    const int EnumerationBufferSize = 64 * 1024;

    static readonly GameAssetFileDefinition[] _assetFiles =
    [
        new("nvngx_dlss.dll", GameAssetType.DLSS),
        new("nvngx_dlssg.dll", GameAssetType.DLSS_G),
        new("nvngx_dlssd.dll", GameAssetType.DLSS_D),
        new("amd_fidelityfx_dx12.dll", GameAssetType.FSR_31_DX12),
        new("amd_fidelityfx_vk.dll", GameAssetType.FSR_31_VK),
        new("libxess.dll", GameAssetType.XeSS),
        new("libxess_dx11.dll", GameAssetType.XeSS_DX11),
        new("libxell.dll", GameAssetType.XeLL),
        new("libxess_fg.dll", GameAssetType.XeSS_FG),
    ];

    static readonly object _sessionLock = new();
    static ScanSession? _currentSession;

    internal static ScanBatch BeginBatch(bool exhaustCandidateRoots = false)
    {
        lock (_sessionLock)
        {
            if (_currentSession is not null)
            {
                throw new InvalidOperationException("A game asset scan batch is already active.");
            }

            _currentSession = new ScanSession(exhaustCandidateRoots);
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

        var request = session?.Register(installPath);
        return new PreparedAssetScan(
            installPath,
            request?.CandidateCompletion.Task,
            request?.Completion.Task,
            request is null ? null : request.CompleteCandidatePublication,
            request is null ? null : request.CompleteCandidateProcessing);
    }

    internal static bool TryGetAssetType(ReadOnlySpan<char> fileName, out GameAssetType assetType)
    {
        foreach (var assetFile in _assetFiles)
        {
            if (fileName.Equals(assetFile.FileName, StringComparison.OrdinalIgnoreCase))
            {
                assetType = assetFile.AssetType;
                return true;
            }
        }

        assetType = GameAssetType.Unknown;
        return false;
    }

    internal static ReadOnlySpan<GameAssetFileDefinition> GetAssetFiles()
    {
        return _assetFiles;
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
        readonly Task<IReadOnlyList<DiscoveredGameAsset>>? _candidateBatchResult;
        readonly Task<IReadOnlyList<DiscoveredGameAsset>>? _batchResult;
        readonly Action<bool>? _completeCandidatePublication;
        readonly Action<bool>? _completeCandidateProcessing;

        internal PreparedAssetScan(
            string installPath,
            Task<IReadOnlyList<DiscoveredGameAsset>>? candidateBatchResult,
            Task<IReadOnlyList<DiscoveredGameAsset>>? batchResult,
            Action<bool>? completeCandidatePublication,
            Action<bool>? completeCandidateProcessing)
        {
            _installPath = installPath;
            _candidateBatchResult = candidateBatchResult;
            _batchResult = batchResult;
            _completeCandidatePublication = completeCandidatePublication;
            _completeCandidateProcessing = completeCandidateProcessing;
        }

        internal Task<IReadOnlyList<DiscoveredGameAsset>> ExecuteCandidatesAsync()
        {
            if (_candidateBatchResult is not null)
            {
                return _candidateBatchResult;
            }

            return Task.Run<IReadOnlyList<DiscoveredGameAsset>>(
                () => GameAssetCandidatePathIndex.EnumerateCandidates(_installPath));
        }

        internal Task<IReadOnlyList<DiscoveredGameAsset>> ExecuteAsync()
        {
            if (_batchResult is not null)
            {
                return _batchResult;
            }

            return Task.Run<IReadOnlyList<DiscoveredGameAsset>>(() => EnumerateTree(_installPath));
        }

        internal void CompleteCandidateProcessing(bool succeeded)
        {
            _completeCandidateProcessing?.Invoke(succeeded);
        }

        internal void CompleteCandidatePublication(bool succeeded)
        {
            _completeCandidatePublication?.Invoke(succeeded);
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

        internal Task WhenCandidateLibraryReadyAsync()
        {
            var session = _session ?? throw new ObjectDisposedException(nameof(ScanBatch));
            return session.CandidateLibraryReady.Task;
        }

        internal void ReleaseExhaustiveScan()
        {
            var session = _session ?? throw new ObjectDisposedException(nameof(ScanBatch));
            session.ContinueExhaustiveScan.TrySetResult();
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
        readonly CancellationTokenSource _cancellation = new();
        readonly bool _exhaustCandidateRoots;
        bool _registrationComplete;
        bool _disposed;

        internal ScanSession(bool exhaustCandidateRoots)
        {
            _exhaustCandidateRoots = exhaustCandidateRoots;
        }

        internal TaskCompletionSource CandidateLibraryReady { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ContinueExhaustiveScan { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ScanRequest? Register(string installPath)
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
                    existing.RegisterConsumer();
                    return existing;
                }

                var request = new ScanRequest(normalizedPath);
                _requests.Add(normalizedPath, request);
                return request;
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

            try
            {
                await ScanRootsAsync(
                    requests,
                    _exhaustCandidateRoots,
                    CandidateLibraryReady,
                    ContinueExhaustiveScan.Task,
                    _cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                CandidateLibraryReady.TrySetResult();
            }
        }

        static async Task ScanRootsAsync(
            ScanRequest[] requests,
            bool exhaustCandidateRoots,
            TaskCompletionSource candidateLibraryReady,
            Task continueExhaustiveScan,
            CancellationToken cancellationToken)
        {
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
            var candidateStartedAt = System.Diagnostics.Stopwatch.StartNew();
            var candidateAssetCount = 0;
            await Parallel.ForEachAsync(
                roots,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = workerCount,
                },
                (root, _) =>
                {
                    try
                    {
                        var assets = Directory.Exists(root.InstallPath)
                            ? GameAssetCandidatePathIndex.EnumerateCandidates(root.InstallPath, cancellationToken)
                            : [];
                        Interlocked.Add(ref candidateAssetCount, assets.Count);
                        root.CompleteCandidates(assets);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        root.CompleteCandidates([]);
                    }
                    catch (Exception err)
                    {
                        root.CompleteCandidates([]);
                        Logger.Error(err, $"Unable to scan candidate paths for game root {root.InstallPath}.");
                    }

                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);
            Logger.Info(
                $"Candidate path scan found {candidateAssetCount:N0} asset(s) across {roots.Length:N0} game root(s) " +
                $"in {candidateStartedAt.Elapsed.TotalSeconds:N2} seconds.");

            RootScan[] exhaustiveRoots;
            if (exhaustCandidateRoots == false)
            {
                var candidateRoots = roots
                    .Where(static root => root.CandidateAssets.Count > 0)
                    .ToArray();
                var candidatePublicationResults = await Task.WhenAll(
                    candidateRoots.Select(static root => root.WhenCandidatePublicationCompleteAsync()))
                    .ConfigureAwait(false);
                Logger.Info(
                    $"Published {candidatePublicationResults.Count(static result => result):N0} " +
                    $"of {candidateRoots.Length:N0} candidate-positive game root(s) before metadata hydration.");
                candidateLibraryReady.TrySetResult();

                var candidateResults = await Task.WhenAll(
                    candidateRoots.Select(static async root => new
                    {
                        Root = root,
                        Succeeded = await root.WhenCandidateProcessingCompleteAsync().ConfigureAwait(false),
                    })).ConfigureAwait(false);
                foreach (var candidateResult in candidateResults.Where(static result => result.Succeeded))
                {
                    candidateResult.Root.Complete(candidateResult.Root.CandidateAssets);
                }
                Logger.Info(
                    $"Published and hydrated {candidateResults.Count(static result => result.Succeeded):N0} " +
                    $"of {candidateRoots.Length:N0} candidate-positive game root(s) " +
                    "before exhaustive fallback.");

                var failedCandidateRoots = candidateResults
                    .Where(static result => result.Succeeded == false)
                    .Select(static result => result.Root)
                    .ToHashSet();
                exhaustiveRoots = roots
                    .Where(root => root.CandidateAssets.Count == 0 || failedCandidateRoots.Contains(root))
                    .ToArray();
            }
            else
            {
                exhaustiveRoots = roots;
                candidateLibraryReady.TrySetResult();
            }

            await continueExhaustiveScan.WaitAsync(cancellationToken).ConfigureAwait(false);
            var recursiveStartedAt = System.Diagnostics.Stopwatch.StartNew();
            await Parallel.ForEachAsync(
                exhaustiveRoots,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = workerCount,
                },
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
                $"Scanned {exhaustiveRoots.Length:N0} game root(s) exhaustively in {recursiveStartedAt.Elapsed.TotalSeconds:N2} seconds; " +
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
                _cancellation.Cancel();
                requests = _requests.Values.ToArray();
            }

            foreach (var request in requests)
            {
                request.CandidateCompletion.TrySetCanceled();
                request.Completion.TrySetCanceled();
                request.PublicationCompletion.TrySetCanceled();
                request.ProcessingCompletion.TrySetCanceled();
            }
            CandidateLibraryReady.TrySetCanceled();
            ContinueExhaustiveScan.TrySetCanceled();
        }
    }

    internal sealed class ScanRequest
    {
        int _consumerCount = 1;
        int _publishedConsumerCount;
        int _failedPublicationCount;
        int _completedConsumerCount;
        int _failedConsumerCount;

        internal string InstallPath { get; }
        internal TaskCompletionSource<IReadOnlyList<DiscoveredGameAsset>> CandidateCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<IReadOnlyList<DiscoveredGameAsset>> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> PublicationCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> ProcessingCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ScanRequest(string installPath)
        {
            InstallPath = installPath;
        }

        internal void RegisterConsumer()
        {
            _consumerCount++;
        }

        internal void CompleteCandidateProcessing(bool succeeded)
        {
            if (succeeded == false)
            {
                Interlocked.Increment(ref _failedConsumerCount);
            }

            if (Interlocked.Increment(ref _completedConsumerCount) == _consumerCount)
            {
                ProcessingCompletion.TrySetResult(Volatile.Read(ref _failedConsumerCount) == 0);
            }
        }

        internal void CompleteCandidatePublication(bool succeeded)
        {
            if (succeeded == false)
            {
                Interlocked.Increment(ref _failedPublicationCount);
            }

            if (Interlocked.Increment(ref _publishedConsumerCount) == _consumerCount)
            {
                PublicationCompletion.TrySetResult(Volatile.Read(ref _failedPublicationCount) == 0);
            }
        }
    }

    sealed class RootScan
    {
        readonly ScanRequest[] _requests;

        internal string InstallPath { get; }
        internal IReadOnlyList<DiscoveredGameAsset> CandidateAssets { get; private set; } = [];

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

        internal void CompleteCandidates(IReadOnlyList<DiscoveredGameAsset> assets)
        {
            var orderedAssets = assets
                .OrderBy(static asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            CandidateAssets = orderedAssets;
            foreach (var request in _requests)
            {
                request.CandidateCompletion.TrySetResult(orderedAssets);
            }
        }

        internal async Task<bool> WhenCandidateProcessingCompleteAsync()
        {
            var results = await Task.WhenAll(
                _requests.Select(static request => request.ProcessingCompletion.Task))
                .ConfigureAwait(false);
            return results.All(static succeeded => succeeded);
        }

        internal async Task<bool> WhenCandidatePublicationCompleteAsync()
        {
            var results = await Task.WhenAll(
                _requests.Select(static request => request.PublicationCompletion.Task))
                .ConfigureAwait(false);
            return results.All(static succeeded => succeeded);
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
