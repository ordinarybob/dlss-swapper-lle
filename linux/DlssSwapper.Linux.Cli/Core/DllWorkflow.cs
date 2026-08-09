using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record SelectedGame(string Name, string RootPath, string? SteamAppId);

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
    IReadOnlyList<string> Warnings);

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

public sealed record OperationResult(
    SelectedGame Game,
    string Family,
    string Target,
    bool Success,
    string Message);

public sealed class DllScanner
{
    public ScanResult Scan(SelectedGame game, DllCatalog catalog)
    {
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
        foreach (var file in files)
        {
            if (!DllTypes.TryFromFileName(file, out var definition))
            {
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
            warnings);
    }

    public IReadOnlyList<RestorePlanItem> PlanRestore(SelectedGame game)
    {
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
        IReadOnlyDictionary<DllType, DllCatalogEntry> candidates)
    {
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
                var compatibility = CheckCompatibility(familyGroup.Key, targets, candidate);
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
                    .Where(target => !(target.HasHash && target.Md5.Equals(
                            candidate.Md5,
                            StringComparison.OrdinalIgnoreCase))
                        && !target.Version.Equals(
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
                        ? "Already current."
                        : $"Update to {candidate.Version}."));
            }
        }

        return plan;
    }

    private static string? CheckCompatibility(
        DllType type,
        IReadOnlyList<DetectedDll> targets,
        DllCatalogEntry candidate)
    {
        if (type != DllType.Dlss)
        {
            return null;
        }

        if (targets.Any(target =>
                target.Version.Equals("unknown", StringComparison.OrdinalIgnoreCase)))
        {
            return "Skipped because the existing DLSS generation could not be determined.";
        }

        var hasV1 = targets.Any(target =>
            target.Version.StartsWith("1.", StringComparison.Ordinal));
        var hasV2Plus = targets.Any(target =>
            !target.Version.StartsWith("1.", StringComparison.Ordinal));
        if (hasV1 && hasV2Plus)
        {
            return "Skipped because the game mixes DLSS generations.";
        }

        var candidateIsV1 = candidate.Version.StartsWith("1.", StringComparison.Ordinal);
        return hasV1 != candidateIsV1
            ? "Skipped because the selected DLSS generation is incompatible."
            : null;
    }
}

public sealed class DownloadCache : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;
    private readonly Dictionary<(DllType Type, string Version, string Md5), Task<string>> _downloads = [];

    public DownloadCache()
    {
        _cacheRoot = GetCacheRoot();
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DLSS-Swapper-LLE-Linux-MVP");
    }

    public Task<string> GetAsync(DllCatalogEntry entry, CancellationToken cancellationToken)
    {
        var key = (entry.Type, entry.Version, entry.Md5);
        if (!_downloads.TryGetValue(key, out var download))
        {
            download = GetCoreAsync(entry, cancellationToken);
            _downloads.Add(key, download);
        }

        return download;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<string> GetCoreAsync(
        DllCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        var definition = DllTypes.Get(entry.Type);
        var cacheDirectory = Path.Combine(
            _cacheRoot,
            definition.ManifestKey,
            $"{Sanitize(entry.Version)}-{entry.Md5}");
        var cachePath = Path.Combine(cacheDirectory, definition.FileName);
        if (File.Exists(cachePath)
            && DllScanner.ComputeMd5(cachePath).Equals(
                entry.Md5,
                StringComparison.OrdinalIgnoreCase))
        {
            return cachePath;
        }

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

                archiveBuffer.Write(buffer, 0, read);
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

            await using var payload = matches[0].Open();
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
        await File.WriteAllBytesAsync(cachePath, dllBytes, cancellationToken)
            .ConfigureAwait(false);
        return cachePath;
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
        CancellationToken cancellationToken) =>
        await ApplyUpdatesAsync(
            plan,
            cache.GetAsync,
            cancellationToken).ConfigureAwait(false);

    public static async Task<IReadOnlyList<OperationResult>> ApplyUpdatesAsync(
        IReadOnlyList<UpdatePlanItem> plan,
        Func<DllCatalogEntry, CancellationToken, Task<string>> getPayloadAsync,
        CancellationToken cancellationToken)
    {
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
                    $"Download failed: {exception.Message}"));
                continue;
            }

            try
            {
                if (!DllScanner.ComputeMd5(sourcePath).Equals(
                        item.Candidate.Md5,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Downloaded DLL hash no longer matches the selected manifest entry.");
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
                    $"Payload validation failed: {exception.Message}"));
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
                            "Already current."));
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
                        File.Copy(targetPath, backupPath);
                    }

                    ValidateMutationPath(
                        item.Game,
                        targetPath,
                        mustExist: true,
                        "DLL target");
                    File.Copy(sourcePath, targetPath, overwrite: true);
                    results.Add(new OperationResult(
                        item.Game,
                        item.Family.DisplayName,
                        target.RelativePath,
                        true,
                        $"Updated to {item.Candidate.Version}."));
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
        IReadOnlyList<RestorePlanItem> plan)
    {
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
                    "Restored original DLL."));
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
