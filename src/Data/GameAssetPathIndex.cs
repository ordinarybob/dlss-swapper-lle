using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace DLSS_Swapper.Data;

internal readonly record struct DiscoveredGameAsset(string Path, GameAssetType AssetType);

internal static class GameAssetPathIndex
{
    const int EnumerationBufferSize = 64 * 1024;

    static readonly object _sessionLock = new();
    static ScanSession? _currentSession;

    internal static IDisposable BeginBatch()
    {
        lock (_sessionLock)
        {
            if (_currentSession is not null)
            {
                throw new InvalidOperationException("A game asset scan batch is already active.");
            }

            _currentSession = new ScanSession();
            return new BatchScope(_currentSession);
        }
    }

    internal static async Task<IReadOnlyList<DiscoveredGameAsset>> FindAsync(string installPath)
    {
        ScanSession? session;
        lock (_sessionLock)
        {
            session = _currentSession;
        }

        if (session is not null)
        {
            var indexedAssets = await session.TryFindIndexedAsync(installPath).ConfigureAwait(false);
            if (indexedAssets is not null)
            {
                return indexedAssets;
            }

            return await session.FindWithFallbackAsync(installPath).ConfigureAwait(false);
        }

        return EnumerateTree(installPath);
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
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            BufferSize = EnumerationBufferSize,
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
        };

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

    static GameAssetType GetAssetType(ReadOnlySpan<char> fileName)
    {
        if (TryGetAssetType(fileName, out var assetType))
        {
            return assetType;
        }

        throw new InvalidOperationException($"Unexpected game asset filename: {fileName.ToString()}");
    }

    static string? TryGetNtfsVolumeRoot(string installPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(installPath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root) || root.Length < 2 || root[1] != ':')
            {
                return null;
            }

            var drive = new DriveInfo(root);
            if (drive.IsReady == false || drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase) == false)
            {
                return null;
            }

            return root.ToUpperInvariant();
        }
        catch (Exception err) when (err is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    sealed class ScanSession
    {
        readonly ConcurrentDictionary<string, Lazy<Task<VolumeAssetIndex?>>> _volumeIndexes =
            new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, SemaphoreSlim> _fallbackGates =
            new(StringComparer.OrdinalIgnoreCase);

        internal async Task<IReadOnlyList<DiscoveredGameAsset>?> TryFindIndexedAsync(string installPath)
        {
            var volumeRoot = TryGetNtfsVolumeRoot(installPath);
            if (volumeRoot is null)
            {
                return null;
            }

            var lazyIndex = _volumeIndexes.GetOrAdd(
                volumeRoot,
                static root => new Lazy<Task<VolumeAssetIndex?>>(
                    () => Task.Run(() => VolumeAssetIndex.TryCreate(root)),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            var index = await lazyIndex.Value.ConfigureAwait(false);
            return index?.FindUnder(installPath);
        }

        internal async Task<IReadOnlyList<DiscoveredGameAsset>> FindWithFallbackAsync(string installPath)
        {
            var volumeRoot = Path.GetPathRoot(Path.GetFullPath(installPath)) ?? installPath;
            var fallbackConcurrency = GetFallbackConcurrency();
            var gate = _fallbackGates.GetOrAdd(
                volumeRoot,
                _ => new SemaphoreSlim(fallbackConcurrency, fallbackConcurrency));

            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return EnumerateTree(installPath);
            }
            finally
            {
                gate.Release();
            }
        }

        static int GetFallbackConcurrency()
        {
            return Math.Clamp((Settings.Instance.RecursiveScanConcurrency + 3) / 4, 1, 8);
        }
    }

    sealed class BatchScope : IDisposable
    {
        ScanSession? _session;

        internal BatchScope(ScanSession session)
        {
            _session = session;
        }

        public void Dispose()
        {
            var session = Interlocked.Exchange(ref _session, null);
            if (session is null)
            {
                return;
            }

            lock (_sessionLock)
            {
                if (ReferenceEquals(_currentSession, session))
                {
                    _currentSession = null;
                }
            }
        }
    }

    sealed class VolumeAssetIndex
    {
        readonly string[] _paths;

        VolumeAssetIndex(IEnumerable<string> paths)
        {
            _paths = paths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static VolumeAssetIndex? TryCreate(string volumeRoot)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var paths = NtfsMasterFileTable.FindTargetFiles(volumeRoot);
                var index = new VolumeAssetIndex(paths);
                Logger.Info($"Indexed {index._paths.Length:N0} game asset candidate(s) from the NTFS MFT on {volumeRoot} in {stopwatch.Elapsed.TotalSeconds:N2} seconds.");
                return index;
            }
            catch (Exception err) when (err is IOException or UnauthorizedAccessException or Win32Exception or NotSupportedException)
            {
                Logger.Warning($"Unable to use the NTFS MFT index on {volumeRoot}; using the directory walker instead. {err.Message}");
                return null;
            }
        }

        internal IReadOnlyList<DiscoveredGameAsset> FindUnder(string installPath)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installPath));
            var prefix = root + Path.DirectorySeparatorChar;
            var first = LowerBound(_paths, prefix);
            var assets = new List<DiscoveredGameAsset>();

            for (var i = first; i < _paths.Length; i++)
            {
                var path = _paths[i];
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == false)
                {
                    break;
                }

                if (TryGetAssetType(Path.GetFileName(path), out var assetType))
                {
                    assets.Add(new DiscoveredGameAsset(path, assetType));
                }
            }

            return assets;
        }

        static int LowerBound(string[] paths, string value)
        {
            var low = 0;
            var high = paths.Length;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (StringComparer.OrdinalIgnoreCase.Compare(paths[middle], value) < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }

    static class NtfsMasterFileTable
    {
        const uint FsctlEnumUsnData = 0x000900B3;
        const uint OpenExisting = 3;
        const uint FileShareRead = 0x00000001;
        const uint FileShareWrite = 0x00000002;
        const uint FileShareDelete = 0x00000004;
        const uint FileFlagBackupSemantics = 0x02000000;
        const int ErrorHandleEof = 38;
        const int OutputBufferSize = 1024 * 1024;
        const int UsnRecordV2MinimumLength = 60;

        internal static IReadOnlyList<string> FindTargetFiles(string volumeRoot)
        {
            using var volume = OpenVolume(volumeRoot);
            var fileIds = EnumerateTargetFileIds(volume);
            var paths = new List<string>(fileIds.Count);
            foreach (var fileId in fileIds)
            {
                var path = TryGetPath(volume, fileId);
                if (path is not null && TryGetAssetType(Path.GetFileName(path), out _))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        static SafeFileHandle OpenVolume(string volumeRoot)
        {
            var volumePath = $@"\\.\{volumeRoot[..2]}";
            var handle = CreateFileW(
                volumePath,
                0,
                FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, $"Could not open volume {volumeRoot}");
            }

            return handle;
        }

        static HashSet<ulong> EnumerateTargetFileIds(SafeFileHandle volume)
        {
            var results = new HashSet<ulong>();
            var input = new MftEnumData
            {
                StartFileReferenceNumber = 0,
                LowUsn = 0,
                HighUsn = long.MaxValue,
            };
            var output = new byte[OutputBufferSize];

            while (true)
            {
                var previousStart = input.StartFileReferenceNumber;
                if (DeviceIoControl(
                    volume,
                    FsctlEnumUsnData,
                    ref input,
                    Marshal.SizeOf<MftEnumData>(),
                    output,
                    output.Length,
                    out var bytesReturned,
                    IntPtr.Zero) == false)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == ErrorHandleEof)
                    {
                        break;
                    }

                    throw new Win32Exception(error, "Could not enumerate NTFS MFT records.");
                }

                if (bytesReturned < sizeof(ulong))
                {
                    break;
                }

                input.StartFileReferenceNumber = BinaryPrimitives.ReadUInt64LittleEndian(output.AsSpan(0, sizeof(ulong)));
                var offset = sizeof(ulong);
                while (offset + UsnRecordV2MinimumLength <= bytesReturned)
                {
                    var record = output.AsSpan(offset, bytesReturned - offset);
                    var recordLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(record));
                    if (recordLength < UsnRecordV2MinimumLength || offset + recordLength > bytesReturned)
                    {
                        throw new IOException("The NTFS MFT returned an invalid USN record.");
                    }

                    var majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(record[4..]);
                    if (majorVersion != 2)
                    {
                        throw new NotSupportedException($"Unsupported USN record version {majorVersion}.");
                    }

                    var attributes = (FileAttributes)BinaryPrimitives.ReadUInt32LittleEndian(record[52..]);
                    var fileNameLength = BinaryPrimitives.ReadUInt16LittleEndian(record[56..]);
                    var fileNameOffset = BinaryPrimitives.ReadUInt16LittleEndian(record[58..]);
                    if ((attributes & FileAttributes.Directory) == 0
                        && fileNameLength > 0
                        && fileNameOffset + fileNameLength <= recordLength)
                    {
                        var fileNameBytes = record.Slice(fileNameOffset, fileNameLength);
                        var fileName = MemoryMarshal.Cast<byte, char>(fileNameBytes);
                        if (TryGetAssetType(fileName, out _))
                        {
                            results.Add(BinaryPrimitives.ReadUInt64LittleEndian(record[8..]));
                        }
                    }

                    offset += recordLength;
                }

                if (input.StartFileReferenceNumber <= previousStart)
                {
                    break;
                }
            }

            return results;
        }

        static string? TryGetPath(SafeFileHandle volume, ulong fileId)
        {
            var descriptor = new FileIdDescriptor
            {
                Size = (uint)Marshal.SizeOf<FileIdDescriptor>(),
                Type = FileIdType.FileId,
                Identifier = new FileIdUnion { FileId = fileId },
            };

            using var file = OpenFileById(
                volume,
                ref descriptor,
                0,
                FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero,
                FileFlagBackupSemantics);
            if (file.IsInvalid)
            {
                return null;
            }

            var pathBuffer = new char[512];
            var length = GetFinalPathNameByHandleW(file, pathBuffer, pathBuffer.Length, 0);
            if (length == 0)
            {
                return null;
            }
            if (length >= pathBuffer.Length)
            {
                pathBuffer = new char[checked((int)length + 1)];
                length = GetFinalPathNameByHandleW(file, pathBuffer, pathBuffer.Length, 0);
                if (length == 0 || length >= pathBuffer.Length)
                {
                    return null;
                }
            }

            return NormalizeDevicePath(new string(pathBuffer, 0, checked((int)length)));
        }

        static string NormalizeDevicePath(string path)
        {
            const string uncPrefix = @"\\?\UNC\";
            const string devicePrefix = @"\\?\";
            if (path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return @"\\" + path[uncPrefix.Length..];
            }
            if (path.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return path[devicePrefix.Length..];
            }

            return path;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MftEnumData
        {
            internal ulong StartFileReferenceNumber;
            internal long LowUsn;
            internal long HighUsn;
        }

        enum FileIdType : uint
        {
            FileId = 0,
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct FileIdUnion
        {
            [FieldOffset(0)]
            internal ulong FileId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FileIdDescriptor
        {
            internal uint Size;
            internal FileIdType Type;
            internal FileIdUnion Identifier;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint controlCode,
            ref MftEnumData input,
            int inputSize,
            byte[] output,
            int outputSize,
            out int bytesReturned,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern SafeFileHandle OpenFileById(
            SafeFileHandle volumeHint,
            ref FileIdDescriptor fileId,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint flagsAndAttributes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandleW(
            SafeFileHandle file,
            [Out] char[] filePath,
            int filePathLength,
            uint flags);
    }
}
