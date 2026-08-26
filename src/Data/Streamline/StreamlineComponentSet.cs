using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace DLSS_Swapper.Data.Streamline;

public sealed record StreamlineComponentOperationResult(
    bool Success,
    int ComponentCount,
    string Message);

public static class StreamlineComponentSet
{
    public const string BackupSuffix = ".dlsss";

    public static readonly IReadOnlyList<string> FileNames =
    [
        "sl.common.dll",
        "sl.deepdvc.dll",
        "sl.directsr.dll",
        "sl.dlss.dll",
        "sl.dlss_d.dll",
        "sl.dlss_g.dll",
        "sl.interposer.dll",
        "sl.nis.dll",
        "sl.nvperf.dll",
        "sl.pcl.dll",
        "sl.reflex.dll",
    ];

    static readonly HashSet<string> KnownFileNames =
        new(FileNames, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> FindInstalled(string gameRoot)
    {
        if (Directory.Exists(gameRoot) == false)
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        return Directory
            .EnumerateFiles(gameRoot, "*.dll", options)
            .Where(path => KnownFileNames.Contains(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> ExtractProductionFiles(
        string archivePath,
        string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var temporaryDirectory = destinationDirectory + ".extracting-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entries = archive.Entries
                .Where(entry => TryGetProductionFileName(entry.FullName, out _))
                .ToArray();

            var duplicate = entries
                .GroupBy(entry => Path.GetFileName(entry.FullName), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() != 1);
            if (duplicate is not null)
            {
                throw new InvalidDataException(
                    $"The SDK archive contains duplicate production copies of {duplicate.Key}.");
            }

            var entryByName = entries.ToDictionary(
                entry => Path.GetFileName(entry.FullName),
                StringComparer.OrdinalIgnoreCase);
            var missing = FileNames.Where(fileName => entryByName.ContainsKey(fileName) == false).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException(
                    $"The SDK archive is missing production component(s): {string.Join(", ", missing)}.");
            }

            foreach (var fileName in FileNames)
            {
                var entry = entryByName[fileName];
                if (entry.Length <= 0)
                {
                    throw new InvalidDataException($"The production component {fileName} is empty.");
                }

                entry.ExtractToFile(Path.Combine(temporaryDirectory, fileName));
            }

            if (Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }

            Directory.Move(temporaryDirectory, destinationDirectory);
            return FileNames
                .Select(fileName => Path.Combine(destinationDirectory, fileName))
                .ToArray();
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            throw;
        }
    }

    public static StreamlineComponentOperationResult UpdateExisting(
        string sourceDirectory,
        IReadOnlyList<string> installedFiles,
        Action<int, string>? beforeReplace = null)
    {
        return Mutate(
            installedFiles,
            targetPath => Path.Combine(sourceDirectory, Path.GetFileName(targetPath)),
            preserveOriginalBackup: true,
            beforeReplace);
    }

    public static StreamlineComponentOperationResult RestoreOriginals(
        IReadOnlyList<string> installedFiles,
        Action<int, string>? beforeReplace = null)
    {
        var restorable = installedFiles
            .Where(path => File.Exists(path + BackupSuffix))
            .ToArray();
        if (restorable.Length == 0)
        {
            return new(true, 0, "No original Streamline components are available to restore.");
        }

        return Mutate(
            restorable,
            targetPath => targetPath + BackupSuffix,
            preserveOriginalBackup: false,
            beforeReplace);
    }

    static StreamlineComponentOperationResult Mutate(
        IReadOnlyList<string> targetFiles,
        Func<string, string> sourceForTarget,
        bool preserveOriginalBackup,
        Action<int, string>? beforeReplace)
    {
        var targets = targetFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targets.Length == 0)
        {
            return new(true, 0, "No installed Streamline components were found.");
        }

        var operationId = Guid.NewGuid().ToString("N");
        var prepared = new List<PreparedMutation>(targets.Length);
        try
        {
            foreach (var targetPath in targets)
            {
                ValidateTarget(targetPath);
                var sourcePath = sourceForTarget(targetPath);
                ValidateSource(sourcePath, Path.GetFileName(targetPath));

                var rollbackPath = targetPath + $".streamline-rollback-{operationId}";
                var incomingPath = targetPath + $".streamline-incoming-{operationId}";
                File.Copy(targetPath, rollbackPath, overwrite: false);
                File.Copy(sourcePath, incomingPath, overwrite: false);
                prepared.Add(new(targetPath, rollbackPath, incomingPath));
            }

            var backupsCreated = new List<string>();
            try
            {
                if (preserveOriginalBackup)
                {
                    foreach (var mutation in prepared)
                    {
                        var backupPath = mutation.TargetPath + BackupSuffix;
                        if (File.Exists(backupPath) == false)
                        {
                            File.Copy(mutation.TargetPath, backupPath, overwrite: false);
                            backupsCreated.Add(backupPath);
                        }
                    }
                }

                for (var index = 0; index < prepared.Count; index++)
                {
                    var mutation = prepared[index];
                    beforeReplace?.Invoke(index, mutation.TargetPath);
                    File.Move(mutation.IncomingPath, mutation.TargetPath, overwrite: true);
                }
            }
            catch
            {
                foreach (var mutation in prepared)
                {
                    if (File.Exists(mutation.RollbackPath))
                    {
                        File.Copy(mutation.RollbackPath, mutation.TargetPath, overwrite: true);
                    }
                }

                foreach (var backupPath in backupsCreated)
                {
                    File.Delete(backupPath);
                }

                throw;
            }

            return new(
                true,
                prepared.Count,
                preserveOriginalBackup
                    ? $"Updated {prepared.Count} existing Streamline component(s)."
                    : $"Restored {prepared.Count} original Streamline component(s).");
        }
        catch (Exception exception)
        {
            return new(false, 0, exception.Message);
        }
        finally
        {
            foreach (var mutation in prepared)
            {
                TryDelete(mutation.IncomingPath);
                TryDelete(mutation.RollbackPath);
            }
        }
    }

    static bool TryGetProductionFileName(string archiveEntryName, out string fileName)
    {
        const string prefix = "bin/x64/";
        var normalized = archiveEntryName.Replace('\\', '/');
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == false)
        {
            fileName = string.Empty;
            return false;
        }

        fileName = normalized[prefix.Length..];
        return fileName.Contains('/') == false && KnownFileNames.Contains(fileName);
    }

    static void ValidateTarget(string targetPath)
    {
        if (KnownFileNames.Contains(Path.GetFileName(targetPath)) == false)
        {
            throw new InvalidOperationException($"Unsupported Streamline component: {targetPath}");
        }

        if (File.Exists(targetPath) == false)
        {
            throw new FileNotFoundException("An installed Streamline component disappeared.", targetPath);
        }

        if ((File.GetAttributes(targetPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"Refusing to replace a linked file: {targetPath}");
        }
    }

    static void ValidateSource(string sourcePath, string expectedFileName)
    {
        var sourceFileName = Path.GetFileName(sourcePath);
        var nameMatches = sourceFileName.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase)
            || sourceFileName.Equals(expectedFileName + BackupSuffix, StringComparison.OrdinalIgnoreCase);
        if (nameMatches == false
            || File.Exists(sourcePath) == false
            || new FileInfo(sourcePath).Length <= 0)
        {
            throw new InvalidDataException(
                $"The staged component is missing or invalid: {expectedFileName} ({sourcePath})");
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Cleanup failure does not invalidate a completed replacement or rollback.
        }
    }

    sealed record PreparedMutation(string TargetPath, string RollbackPath, string IncomingPath);
}
