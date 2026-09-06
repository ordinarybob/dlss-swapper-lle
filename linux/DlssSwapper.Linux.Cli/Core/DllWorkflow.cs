using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record SelectedGame(string Name, string RootPath, string? SteamAppId)
{
    public DlssSwapper.Linux.Cli.Platform.ProviderGameIdentity? ProviderIdentity { get; init; }
    public string? WinePrefix { get; init; }
    public string? LocalIconPath { get; init; }
    public string? CoverUrl { get; init; }
    public int LocalIconIndex { get; init; }
    public DlssSwapper.Linux.Cli.Platform.BattleNetMetadata? BattleNet { get; init; }
    public DlssSwapper.Linux.Cli.Platform.ProviderLaunch? ProviderLaunch { get; init; }
    public IReadOnlyList<DlssSwapper.Linux.Cli.Platform.ProviderLaunch> ProviderLaunchChoices { get; init; } = [];
    public IReadOnlyList<DlssSwapper.Linux.Cli.Platform.ProviderGameIdentity> ProviderIdentityAliases { get; init; } = [];
}

public sealed record DetectedDll(
    DllType Type,
    string Path,
    string RelativePath,
    string Md5,
    string Version,
    long FileLength = 0,
    DateTime LastWriteTimeUtc = default)
{
    public bool HasHash => !string.IsNullOrWhiteSpace(Md5);
}

public sealed record ScanResult(
    SelectedGame Game,
    IReadOnlyList<DetectedDll> Dlls,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string> StreamlineFiles { get; init; } = [];
    public DateTimeOffset? CachedAtUtc { get; init; }
}

public enum UpdatePlanStatus
{
    Ready,
    AlreadyCurrent,
    Skipped,
}

public sealed record UpdatePlanItem(
    SelectedGame Game,
    DllTypeDefinition Family,
    DllCatalogEntry Candidate,
    IReadOnlyList<DetectedDll> Targets,
    UpdatePlanStatus Status,
    string Message);

public sealed record RestorePlanItem(
    SelectedGame Game,
    DllTypeDefinition Family,
    string BackupPath,
    string TargetPath,
    string RelativeTargetPath);

public enum OperationOutcome { Completed, AlreadyCurrent, Skipped, Cancelled, Failed }

public sealed record OperationResult(
    SelectedGame Game,
    string Family,
    string Target,
    bool Success,
    string Message,
    OperationOutcome? Outcome = null)
{
    public OperationOutcome EffectiveOutcome => Outcome ?? (Success ? OperationOutcome.Completed : OperationOutcome.Failed);
}

public sealed class DllScanner
{
    public ScanResult Scan(SelectedGame game, DllCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(catalog);
        var warnings = new List<string>();
        return ScanFiles(
            game,
            catalog,
            EnumerateRegularFiles(game.RootPath, warnings),
            warnings,
            computeHashes: true);
    }

    public ScanResult ScanCandidates(
        SelectedGame game,
        DllCatalog catalog,
        CandidateFileResult candidates)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(candidates);
        return ScanFiles(
            game,
            catalog,
            candidates.Files,
            candidates.Warnings.ToList(),
            computeHashes: false);
    }

    public DetectedDll ResolveIdentity(DetectedDll detected, DllCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(catalog);
        if (detected.HasHash)
        {
            return detected;
        }

        var info = new FileInfo(detected.Path);
        if (!info.Exists
            || info.Length != detected.FileLength
            || info.LastWriteTimeUtc != detected.LastWriteTimeUtc)
        {
            throw new IOException($"DLL identity changed after scanning: {detected.Path}");
        }

        var md5 = ComputeMd5(detected.Path);
        return detected with
        {
            Md5 = md5,
            Version = GetVersion(detected.Path, catalog, detected.Type, md5),
        };
    }

    private static ScanResult ScanFiles(
        SelectedGame game,
        DllCatalog catalog,
        IEnumerable<string> files,
        List<string> warnings,
        bool computeHashes)
    {
        var dlls = new List<DetectedDll>();
        var streamline = new List<string>();
        foreach (var file in files)
        {
            if (!DllTypes.TryFromFileName(file, out var definition))
            {
                if (DLSS_Swapper.Data.Streamline.StreamlineComponentSet.FileNames.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                    streamline.Add(file);
                continue;
            }

            try
            {
                var info = new FileInfo(file);
                var md5 = computeHashes ? ComputeMd5(file) : string.Empty;
                dlls.Add(new DetectedDll(
                    definition.Type,
                    file,
                    Path.GetRelativePath(game.RootPath, file),
                    md5,
                    computeHashes
                        ? GetVersion(file, catalog, definition.Type, md5)
                        : GetFileVersion(file),
                    info.Length,
                    info.LastWriteTimeUtc));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException)
            {
                warnings.Add($"Could not inspect '{file}': {exception.Message}");
            }
        }

        return new ScanResult(
            game,
            dlls.OrderBy(dll => dll.RelativePath, StringComparer.Ordinal).ToArray(),
            warnings) { StreamlineFiles = streamline };
    }

    public IReadOnlyList<RestorePlanItem> PlanRestore(SelectedGame game)
        => PlanRestore(game, out _);

    public IReadOnlyList<RestorePlanItem> PlanRestore(SelectedGame game, out IReadOnlyList<string> scanWarnings)
    {
        ArgumentNullException.ThrowIfNull(game);
        const string backupSuffix = ".dlsss";
        var warnings = new List<string>();
        var items = new List<RestorePlanItem>();
        foreach (var file in EnumerateRegularFiles(game.RootPath, warnings))
        {
            var fileName = Path.GetFileName(file);
            if (!fileName.EndsWith(backupSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetName = fileName[..^backupSuffix.Length];
            if (!DllTypes.TryFromFileName(targetName, out var definition))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(file)
                ?? throw new InvalidDataException($"Backup has no parent directory: {file}");
            var target = Path.Combine(directory, targetName);
            items.Add(new RestorePlanItem(
                game,
                definition,
                file,
                target,
                Path.GetRelativePath(game.RootPath, target)));
        }

        scanWarnings = warnings.ToArray();
        return items
            .OrderBy(item => item.RelativeTargetPath, StringComparer.Ordinal)
            .ToArray();
    }

    public static string ComputeMd5(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(MD5.HashData(stream));
    }

    private static string GetVersion(
        string path,
        DllCatalog catalog,
        DllType type,
        string md5)
    {
        if (catalog.TryGetKnownVersion(type, md5, out var knownVersion))
        {
            return knownVersion;
        }

        return GetFileVersion(path);
    }

    private static string GetFileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown";
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
    }

    private static IEnumerable<string> EnumerateRegularFiles(
        string root,
        List<string> warnings)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(directory);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                warnings.Add($"Could not enumerate '{directory}': {exception.Message}");
                continue;
            }

            foreach (var entry in entries)
            {
                FileSystemInfo info;
                try
                {
                    info = Directory.Exists(entry)
                        ? new DirectoryInfo(entry)
                        : new FileInfo(entry);
                    if (info.LinkTarget is not null
                        || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        warnings.Add($"Skipped symbolic link '{entry}'.");
                        continue;
                    }
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException)
                {
                    warnings.Add($"Could not inspect '{entry}': {exception.Message}");
                    continue;
                }

                if ((info.Attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(info.FullName);
                }
                else
                {
                    yield return info.FullName;
                }
            }
        }
    }
}

public sealed class UpdatePlanner
{
    public IReadOnlyList<UpdatePlanItem> Plan(
        IReadOnlyList<ScanResult> scans,
        IReadOnlyDictionary<DllType, DllCatalogEntry> candidates, Translations? translations = null)
    {
        ArgumentNullException.ThrowIfNull(scans);
        ArgumentNullException.ThrowIfNull(candidates);
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var plan = new List<UpdatePlanItem>();
        foreach (var scan in scans)
        {
            foreach (var familyGroup in scan.Dlls.GroupBy(dll => dll.Type))
            {
                if (!candidates.TryGetValue(familyGroup.Key, out var candidate))
                {
                    continue;
                }

                var family = DllTypes.Get(familyGroup.Key);
                var targets = familyGroup.ToArray();
                var compatibility = scan.CachedAtUtc is not null ? T("Linux_PlanRescan", "Scan the game again before updating; these are last-known files.") : CheckCompatibility(familyGroup.Key, targets, candidate, translations);
                if (compatibility is not null)
                {
                    plan.Add(new UpdatePlanItem(
                        scan.Game,
                        family,
                        candidate,
                        [],
                        UpdatePlanStatus.Skipped,
                        compatibility));
                    continue;
                }

                var changedTargets = targets
                    .Where(target => target.HasHash
                        ? !target.Md5.Equals(
                            candidate.Md5,
                            StringComparison.OrdinalIgnoreCase)
                        : !target.Version.Equals(
                            candidate.Version,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                plan.Add(new UpdatePlanItem(
                    scan.Game,
                    family,
                    candidate,
                    changedTargets,
                    changedTargets.Length == 0
                        ? UpdatePlanStatus.AlreadyCurrent
                        : UpdatePlanStatus.Ready,
                    changedTargets.Length == 0
                        ? T("Linux_OperationCurrent", "Already current.")
                        : T("Linux_PlanUpdate", "Update to {0}.", candidate.Version)));
            }
        }

        return plan;
    }

    private static string? CheckCompatibility(
        DllType type,
        IReadOnlyList<DetectedDll> targets,
        DllCatalogEntry candidate, Translations? translations)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        if (type != DllType.Dlss)
        {
            return null;
        }

        if (targets.Any(target =>
                target.Version.Equals("unknown", StringComparison.OrdinalIgnoreCase)))
        {
            return T("Linux_PlanUnknownGeneration", "Skipped because the existing DLSS generation could not be determined.");
        }

        var hasV1 = targets.Any(target =>
            target.Version.StartsWith("1.", StringComparison.Ordinal));
        var hasV2Plus = targets.Any(target =>
            !target.Version.StartsWith("1.", StringComparison.Ordinal));
        if (hasV1 && hasV2Plus)
        {
            return T("Linux_PlanMixedGenerations", "Skipped because the game mixes DLSS generations.");
        }

        var candidateIsV1 = candidate.Version.StartsWith("1.", StringComparison.Ordinal);
        return hasV1 != candidateIsV1
            ? T("Linux_PlanIncompatible", "Skipped because the selected DLSS generation is incompatible.")
            : null;
    }
}

public sealed record CacheAcquisition(string Path, bool WasDownloaded);

public sealed class DownloadCache : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;
    private readonly bool _ownsHttpClient;
    private readonly Dictionary<(DllType Type, string Version, string Md5), SemaphoreSlim> _downloads = [];

    public DownloadCache(HttpClient? httpClient = null, string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot ?? GetCacheRoot();
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        if (_ownsHttpClient)
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DLSS-Swapper-LLE-Linux-MVP");
    }

    public async Task<string> GetAsync(
        DllCatalogEntry entry,
        CancellationToken cancellationToken)
        => (await AcquireAsync(entry, cancellationToken).ConfigureAwait(false)).Path;

    public async Task<CacheAcquisition> AcquireAsync(
        DllCatalogEntry entry,
        CancellationToken cancellationToken)
        => await AcquireAsync(entry, cancellationToken, null).ConfigureAwait(false);

    public async Task<CacheAcquisition> AcquireAsync(
        DllCatalogEntry entry,
        CancellationToken cancellationToken,
        Action<long, long>? transferProgress)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var key = (entry.Type, entry.Version, entry.Md5);
        cancellationToken.ThrowIfCancellationRequested();
        SemaphoreSlim gate;
        lock (_downloads)
        {
            if (!_downloads.TryGetValue(key, out gate!))
            {
                gate = new SemaphoreSlim(1, 1);
                _downloads.Add(key, gate);
            }
        }

        // Each caller owns its cancellation. A later caller revalidates the completed cache.
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetCoreAsync(entry, cancellationToken, transferProgress).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    public string GetCachedPath(DllCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var definition = DllTypes.Get(entry.Type);
        return Path.Combine(
            _cacheRoot,
            definition.ManifestKey,
            $"{Sanitize(entry.Version)}-{entry.Md5}",
            definition.FileName);
    }

    public bool IsCached(DllCatalogEntry entry)
    {
        var path = GetCachedPath(entry);
        if (!File.Exists(path)) return false;
        if (!entry.IsImported) return true;
        // Local-only entries cannot be repaired by downloading. Show reimport guidance for
        // corrupt or unreadable payloads rather than treating their mere presence as readiness.
        try { return DllScanner.ComputeMd5(path) == entry.Md5; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }

    public async Task<bool> ImportAsync(DllCatalogEntry entry, string source, CancellationToken token)
    {
        var destination = GetCachedPath(entry);
        if (File.Exists(destination) && DllScanner.ComputeMd5(destination) == entry.Md5) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".import-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, token).ConfigureAwait(false);
            if (DllScanner.ComputeMd5(temporary) != entry.Md5) throw new IOException("Import contents changed while copying.");
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            return true;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public bool Remove(DllCatalogEntry entry)
    {
        var path = GetCachedPath(entry);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory)
            && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }

        return true;
    }

    private async Task<CacheAcquisition> GetCoreAsync(
        DllCatalogEntry entry,
        CancellationToken cancellationToken, Action<long, long>? transferProgress)
    {
        var definition = DllTypes.Get(entry.Type);
        var cachePath = GetCachedPath(entry);
        var cacheDirectory = Path.GetDirectoryName(cachePath)
            ?? throw new InvalidDataException("The DLL cache path has no parent directory.");
        if (File.Exists(cachePath)
            && DllScanner.ComputeMd5(cachePath).Equals(
                entry.Md5,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(cachePath, false);
        }

        if (entry.IsImported || entry.DownloadUri is null) throw new IOException("The imported DLL is missing or changed. Import it again from the original source.");
        using var request = new HttpRequestMessage(HttpMethod.Get, entry.DownloadUri);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is null
            || !finalUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(finalUri.UserInfo))
        {
            throw new InvalidDataException("The catalog download redirected to an unsafe URL.");
        }

        if (response.Content.Headers.ContentLength is long contentLength
            && contentLength != entry.ZipFileSize)
        {
            throw new InvalidDataException(
                $"Downloaded archive size does not match {definition.DisplayName} {entry.Version}.");
        }

        byte[] archiveBytes;
        await using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken)
                         .ConfigureAwait(false))
        using (var archiveBuffer = new MemoryStream())
        {
            var buffer = new byte[81920];
            long total = 0;
            transferProgress?.Invoke(0, entry.ZipFileSize);
            while (true)
            {
                var read = await responseStream.ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > entry.ZipFileSize)
                {
                    throw new InvalidDataException(
                        $"Downloaded archive exceeds its manifest size for {definition.DisplayName} {entry.Version}.");
                }

                await archiveBuffer.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken).ConfigureAwait(false);
                transferProgress?.Invoke(total, entry.ZipFileSize);
            }

            if (total != entry.ZipFileSize)
            {
                throw new InvalidDataException(
                    $"Downloaded archive size does not match {definition.DisplayName} {entry.Version}.");
            }

            archiveBytes = archiveBuffer.ToArray();
        }
        var archiveMd5 = Convert.ToHexString(MD5.HashData(archiveBytes));
        if (!archiveMd5.Equals(entry.ZipMd5, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Downloaded archive MD5 does not match {definition.DisplayName} {entry.Version}.");
        }

        byte[] dllBytes;
        using (var archiveStream = new MemoryStream(archiveBytes, writable: false))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read))
        {
            var matches = archive.Entries
                .Where(candidate => string.Equals(
                    Path.GetFileName(candidate.FullName),
                    definition.FileName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1 || matches[0].Length != entry.FileSize)
            {
                throw new InvalidDataException(
                    $"Archive does not contain the expected {definition.FileName} payload.");
            }

            await using var payload = await matches[0].OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            using var buffer = new MemoryStream((int)matches[0].Length);
            await payload.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            dllBytes = buffer.ToArray();
        }

        var dllMd5 = Convert.ToHexString(MD5.HashData(dllBytes));
        if (!dllMd5.Equals(entry.Md5, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Downloaded DLL MD5 does not match {definition.DisplayName} {entry.Version}.");
        }

        Directory.CreateDirectory(cacheDirectory);
        await CacheFileWriter.WriteAsync(cachePath, dllBytes, cancellationToken)
            .ConfigureAwait(false);
        return new(cachePath, true);
    }

    private static string GetCacheRoot()
    {
        var configured = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
        {
            return Path.Combine(configured, "dlss-swapper-lle");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME")
                ?? throw new InvalidOperationException("Could not locate the user cache directory.");
        }

        return Path.Combine(home, ".cache", "dlss-swapper-lle");
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}

public static class DllOperations
{
    public static async Task<IReadOnlyList<OperationResult>> ApplyUpdatesAsync(
        IReadOnlyList<UpdatePlanItem> plan,
        DownloadCache cache,
        CancellationToken cancellationToken, Translations? translations = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        return await ApplyUpdatesAsync(
            plan,
            cache.GetAsync,
            cancellationToken, translations).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<OperationResult>> ApplyUpdatesAsync(
        IReadOnlyList<UpdatePlanItem> plan,
        Func<DllCatalogEntry, CancellationToken, Task<string>> getPayloadAsync,
        CancellationToken cancellationToken, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(getPayloadAsync);
        var results = new List<OperationResult>();
        foreach (var item in plan.Where(item => item.Status == UpdatePlanStatus.Ready))
        {
            string sourcePath;
            try
            {
                sourcePath = await getPayloadAsync(item.Candidate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new OperationResult(
                    item.Game,
                    item.Family.DisplayName,
                    item.Game.RootPath,
                    false,
                    T("Linux_OperationDownloadFailed", "Download failed: {0}", exception.Message)));
                continue;
            }

            try
            {
                if (!DllScanner.ComputeMd5(sourcePath).Equals(
                        item.Candidate.Md5,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        T("Linux_OperationHashChanged", "Downloaded DLL hash no longer matches the selected manifest entry."));
                }
            }
            catch (Exception exception) when (exception is InvalidDataException
                or IOException
                or UnauthorizedAccessException)
            {
                results.Add(new OperationResult(
                    item.Game,
                    item.Family.DisplayName,
                    item.Game.RootPath,
                    false,
                    T("Linux_OperationPayloadFailed", "Payload validation failed: {0}", exception.Message)));
                continue;
            }

            foreach (var target in item.Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var targetPath = ValidateMutationPath(
                        item.Game,
                        target.Path,
                        mustExist: true,
                        "DLL target");
                    var backupPath = ValidateMutationPath(
                        item.Game,
                        targetPath + ".dlsss",
                        mustExist: false,
                        "DLL backup");
                    if (DllScanner.ComputeMd5(targetPath).Equals(
                            item.Candidate.Md5,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new OperationResult(
                            item.Game,
                            item.Family.DisplayName,
                            target.RelativePath,
                            true,
                            T("Linux_OperationCurrent", "Already current."), OperationOutcome.AlreadyCurrent));
                        continue;
                    }

                    if (!File.Exists(backupPath))
                    {
                        ValidateMutationPath(
                            item.Game,
                            targetPath,
                            mustExist: true,
                            "DLL target");
                        ValidateMutationPath(
                            item.Game,
                            backupPath,
                            mustExist: false,
                            "DLL backup");
                        await DllFileReplacement.CopyAsync(targetPath, backupPath, DllScanner.ComputeMd5(targetPath), false, () =>
                        {
                            ValidateMutationPath(item.Game, targetPath, true, "DLL target");
                            ValidateMutationPath(item.Game, backupPath, false, "DLL backup");
                        }, cancellationToken).ConfigureAwait(false);
                    }

                    ValidateMutationPath(
                        item.Game,
                        targetPath,
                        mustExist: true,
                        "DLL target");
                    await DllFileReplacement.CopyAsync(sourcePath, targetPath, item.Candidate.Md5, true, () =>
                    {
                        ValidateMutationPath(item.Game, targetPath, true, "DLL target");
                    }, cancellationToken).ConfigureAwait(false);
                    results.Add(new OperationResult(
                        item.Game,
                        item.Family.DisplayName,
                        target.RelativePath,
                        true,
                        T("Linux_OperationUpdated", "Updated to {0}.", item.Candidate.Version)));
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException)
                {
                    results.Add(new OperationResult(
                        item.Game,
                        item.Family.DisplayName,
                        target.RelativePath,
                        false,
                        exception.Message));
                }
            }
        }

        return results;
    }

    public static IReadOnlyList<OperationResult> ApplyRestores(
        IReadOnlyList<RestorePlanItem> plan, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        ArgumentNullException.ThrowIfNull(plan);
        var results = new List<OperationResult>();
        foreach (var item in plan)
        {
            try
            {
                var backupPath = ValidateMutationPath(
                    item.Game,
                    item.BackupPath,
                    mustExist: true,
                    "DLL backup");
                var targetPath = ValidateMutationPath(
                    item.Game,
                    item.TargetPath,
                    mustExist: false,
                    "DLL target");
                File.Move(backupPath, targetPath, overwrite: true);
                results.Add(new OperationResult(
                    item.Game,
                    item.Family.DisplayName,
                    item.RelativeTargetPath,
                    true,
                    T("Linux_OperationRestored", "Restored original DLL.")));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                results.Add(new OperationResult(
                    item.Game,
                    item.Family.DisplayName,
                    item.RelativeTargetPath,
                    false,
                    exception.Message));
            }
        }

        return results;
    }

    private static string ValidateMutationPath(
        SelectedGame game,
        string path,
        bool mustExist,
        string description)
    {
        var root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(game.RootPath));
        var candidate = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relative)
            || relative.Equals(".", StringComparison.Ordinal)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new IOException($"{description} escaped the selected game directory.");
        }

        ValidateDirectory(root);
        var parent = Path.GetDirectoryName(candidate)
            ?? throw new IOException($"{description} has no parent directory.");
        var relativeParent = Path.GetRelativePath(root, parent);
        var current = root;
        foreach (var component in relativeParent.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (component.Equals(".", StringComparison.Ordinal))
            {
                continue;
            }

            current = Path.Combine(current, component);
            ValidateDirectory(current);
        }

        var information = new FileInfo(candidate);
        if (information.LinkTarget is not null
            || (information.Exists
                && (information.Attributes & FileAttributes.ReparsePoint) != 0))
        {
            throw new IOException($"{description} is a symbolic link or reparse point.");
        }

        if (Directory.Exists(candidate))
        {
            throw new IOException($"{description} is a directory.");
        }

        if (mustExist && !information.Exists)
        {
            throw new FileNotFoundException($"{description} no longer exists.", candidate);
        }

        return candidate;
    }

    private static void ValidateDirectory(string path)
    {
        var information = new DirectoryInfo(path);
        if (!information.Exists)
        {
            throw new DirectoryNotFoundException(
                $"Game-file parent directory no longer exists: {path}");
        }

        if (information.LinkTarget is not null
            || (information.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                $"Game-file path contains a symbolic link or reparse point: {path}");
        }
    }
}
