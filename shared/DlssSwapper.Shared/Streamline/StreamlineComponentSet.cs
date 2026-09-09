using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace DLSS_Swapper.Data.Streamline;

public sealed record StreamlineComponentOperationResult(bool Success, int ComponentCount, string Message,
    IReadOnlyList<string>? ChangedPaths = null);

/// <summary>Existing-file-only SDK replacement. Journals and recovery images survive failed rollback.</summary>
public static class StreamlineComponentSet
{
    public const string BackupSuffix = ".dlsss";
    const string JournalName = ".streamline-transaction.json";
    const string PackageInventoryName = "package-components.json";
    static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static readonly IReadOnlyList<string> FileNames =
    [
        "sl.common.dll", "sl.deepdvc.dll", "sl.directsr.dll", "sl.dlss.dll", "sl.dlss_d.dll",
        "sl.dlss_g.dll", "sl.interposer.dll", "sl.nis.dll", "sl.nvperf.dll", "sl.pcl.dll", "sl.reflex.dll",
    ];
    static readonly HashSet<string> KnownFileNames = new(FileNames, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> FindInstalled(string gameRoot)
    {
        if (!Directory.Exists(gameRoot)) return [];
        return Directory.EnumerateFiles(gameRoot, "*.dll", new EnumerationOptions
        {
            RecurseSubdirectories = true, IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint, MatchCasing = MatchCasing.CaseInsensitive,
        }).Where(path => KnownFileNames.Contains(Path.GetFileName(path))).OrderBy(path => path, PathComparer).ToArray();
    }

    public static void ValidatePackage(string directory)
    {
        var inventory = Path.Combine(directory, PackageInventoryName);
        var names = File.Exists(inventory) ? JsonSerializer.Deserialize<string[]>(File.ReadAllText(inventory))
            ?? throw new InvalidDataException("Invalid SDK component inventory.") : FileNames.ToArray();
        if (names.Length == 0 || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length
            || names.Any(name => !KnownFileNames.Contains(name))
            || !names.Contains("sl.common.dll", StringComparer.OrdinalIgnoreCase)
            || !names.Contains("sl.interposer.dll", StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid SDK component inventory.");
        foreach (var name in names) ValidateSource(Path.Combine(directory, name), name);
    }

    public static bool HasPendingRecovery(string gameRoot) => RecoveryJournals(gameRoot).Any();

    public static StreamlineComponentOperationResult RecoverInterrupted(string gameRoot)
    {
        try
        {
            var root = Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var targets = RecoveryJournals(gameRoot).SelectMany(path => ReadJournal(path).Entries).Select(entry => entry.Target).ToArray();
            if (targets.Any(path => !Path.GetFullPath(path).StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                return new(false, 0, "The recovery journal references components outside this game folder; no files were changed.");
            return RecoverInterrupted(targets);
        }
        catch (Exception ex) { return new(false, 0, ex.Message); }
    }

    static IEnumerable<string> RecoveryJournals(string gameRoot) => !Directory.Exists(gameRoot) ? [] :
        Directory.EnumerateFiles(gameRoot, JournalName, new EnumerationOptions { RecurseSubdirectories = true,
            IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint });

    public static IReadOnlyList<string> ExtractProductionFiles(string archivePath, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        destinationDirectory = Path.GetFullPath(destinationDirectory);
        var stage = destinationDirectory + ".extracting-" + Guid.NewGuid().ToString("N");
        var previous = destinationDirectory + ".previous-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entries = archive.Entries.Where(entry => TryGetProductionFileName(entry.FullName, out _))
                .GroupBy(entry => entry.FullName.Replace('\\', '/').Split('/')[2], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            foreach (var name in entries.Keys)
            {
                if (!entries.TryGetValue(name, out var matches) || matches.Length != 1)
                    throw new InvalidDataException($"The SDK archive must contain exactly one production copy of {name}.");
                matches[0].ExtractToFile(Path.Combine(stage, name));
            }
            File.WriteAllText(Path.Combine(stage, PackageInventoryName), JsonSerializer.Serialize(entries.Keys.ToArray()));
            ValidatePackage(stage);
            // Keep the previous complete package until promotion succeeds. Never delete it first.
            if (Directory.Exists(destinationDirectory)) Directory.Move(destinationDirectory, previous);
            try { Directory.Move(stage, destinationDirectory); }
            catch
            {
                if (Directory.Exists(previous) && !Directory.Exists(destinationDirectory)) Directory.Move(previous, destinationDirectory);
                throw;
            }
            if (Directory.Exists(previous)) TryDeleteDirectory(previous);
            return entries.Keys.Select(name => Path.Combine(destinationDirectory, name)).ToArray();
        }
        finally { TryDeleteDirectory(stage); }
    }

    public static StreamlineComponentOperationResult UpdateExisting(string sourceDirectory, IReadOnlyList<string> installedFiles,
        Action<int, string>? beforeReplace = null, StreamlinePreviewSnapshot? expectedPreview = null) => Mutate(installedFiles,
            target => Path.Combine(sourceDirectory, FileNames.First(name => name.Equals(Path.GetFileName(target), StringComparison.OrdinalIgnoreCase))), true, beforeReplace, expectedPreview);

    public static StreamlineComponentOperationResult RestoreOriginals(IReadOnlyList<string> installedFiles,
        Action<int, string>? beforeReplace = null, StreamlinePreviewSnapshot? expectedPreview = null) => Mutate(installedFiles.Where(path => File.Exists(path + BackupSuffix)).ToArray(),
            target => target + BackupSuffix, false, beforeReplace, expectedPreview);

    /// <summary>Retry retained rollback only when current bytes still match this transaction's old or new image.</summary>
    public static StreamlineComponentOperationResult RecoverInterrupted(IReadOnlyList<string> installedFiles)
    {
        var locks = new List<FileStream>();
        try
        {
            var paths = Directories(installedFiles).Select(dir => Path.Combine(dir, JournalName)).Where(File.Exists).ToArray();
            var journals = paths.Select(ReadJournal).GroupBy(j => j.Id).Select(group => group.First()).ToArray();
            foreach (var discovered in journals)
            {
                var journal = discovered;
                AcquireLocks(Directories(journal.Entries.Select(entry => entry.Target)), locks);
                var canonical = Path.Combine(Directories(journal.Entries.Select(entry => entry.Target))[0], JournalName);
                // Only the canonical journal records commit. Replica journals are discovery breadcrumbs.
                if (!File.Exists(canonical))
                {
                    // Another owner may have completed cleanup after discovery.
                    // Never act on its stale in-memory journal.
                    if (Directories(journal.Entries.Select(entry => entry.Target))
                        .Any(dir => File.Exists(Path.Combine(dir, JournalName))))
                        throw new IOException("The canonical recovery journal is missing. Remaining recovery files were preserved.");
                    DisposeLocks(locks);
                    continue;
                }
                journal = ReadJournal(canonical);
                if (journal.Id != discovered.Id || !journal.Entries.Select(entry => entry.Target).SequenceEqual(discovered.Entries.Select(entry => entry.Target), PathComparer))
                    throw new IOException("The recovery journal changed while acquiring its locks; retry recovery.");
                if (!journal.Committed && !Rollback(journal, out var errors))
                    return new(false, 0, $"Recovery retained for {canonical}: {errors}");
                Cleanup(journal);
                DisposeLocks(locks);
            }
            return new(true, 0, journals.Length == 0 ? "No interrupted Streamline operation was found." : "Interrupted Streamline operations recovered.");
        }
        catch (Exception ex) { return new(false, 0, ex.Message); }
        finally { DisposeLocks(locks); }
    }

    static StreamlineComponentOperationResult Mutate(IReadOnlyList<string> targetFiles, Func<string, string> sourceForTarget,
        bool preserveOriginal, Action<int, string>? beforeReplace, StreamlinePreviewSnapshot? expectedPreview)
    {
        var locks = new List<FileStream>();
        Journal? journal = null;
        var durable = false;
        try
        {
            var targets = targetFiles.Select(Path.GetFullPath).Distinct(PathComparer).OrderBy(path => path, PathComparer).ToArray();
            var directories = Directories(targets);
            AcquireLocks(directories, locks);
            var expected = expectedPreview?.Components.Where(item => preserveOriginal || item.Original.State != StreamlineFileState.Missing)
                .ToDictionary(item => Path.GetFullPath(item.TargetPath), PathComparer);
            if (expected is not null && !targets.SequenceEqual(expected.Keys.OrderBy(path => path, PathComparer), PathComparer))
                throw new IOException("The component set changed since confirmation. Review the comparison and confirm again.");
            if (targets.Length == 0) return new(true, 0, "No installed components are available for this operation.");
            foreach (var dir in directories)
                if (File.Exists(Path.Combine(dir, JournalName)))
                    return new(false, 0, $"An interrupted Streamline operation needs recovery: {Path.Combine(dir, JournalName)}");

            journal = new Journal { Id = Guid.NewGuid().ToString("N") };
            foreach (var target in targets)
            {
                ValidateTarget(target);
                var source = sourceForTarget(target);
                ValidateSource(source, Path.GetFileName(target));
                var oldHash = Hash(target);
                var newHash = Hash(source);
                if (expected is not null)
                {
                    var component = expected[target];
                    var expectedSource = preserveOriginal ? component.Package : component.Original;
                    if (component.Installed.State != StreamlineFileState.Available || expectedSource.State != StreamlineFileState.Available ||
                        !PathComparer.Equals(Path.GetFullPath(expectedSource.Path), Path.GetFullPath(source)) ||
                        !string.Equals(oldHash, component.Installed.Sha256, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(newHash, expectedSource.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new IOException($"{Path.GetFileName(target)} changed since confirmation. No replacement was started; review the comparison and confirm again.");
                }
                if (oldHash == newHash) continue;
                journal.Entries.Add(new Entry { Target = target, Source = source, OldHash = oldHash, NewHash = newHash });
            }
            if (journal.Entries.Count == 0) return new(true, 0, "All Streamline components already match; no files changed.");

            // Persist all planned stage names before creating any, so interrupted preparation is discoverable.
            foreach (var dir in Directories(journal.Entries.Select(entry => entry.Target))) WriteJournal(Path.Combine(dir, JournalName), journal);
            durable = true;
            foreach (var entry in journal.Entries)
            {
                CopyDurable(entry.Target, RollbackPath(journal, entry));
                CopyDurable(entry.Source, IncomingPath(journal, entry));
                RequireHash(RollbackPath(journal, entry), entry.OldHash);
                RequireHash(IncomingPath(journal, entry), entry.NewHash);
                if (preserveOriginal && !File.Exists(entry.Target + BackupSuffix))
                {
                    // Stage originals as well: an interrupted copy must never become a permanent partial backup.
                    var backupStage = BackupStagePath(journal, entry);
                    CopyDurable(RollbackPath(journal, entry), backupStage);
                    File.Move(backupStage, entry.Target + BackupSuffix, overwrite: false);
                }
            }
            for (var index = 0; index < journal.Entries.Count; index++)
            {
                var entry = journal.Entries[index];
                beforeReplace?.Invoke(index, entry.Target);
                ValidateTarget(entry.Target);
                RequireHash(entry.Target, entry.OldHash);
                RequireHash(IncomingPath(journal, entry), entry.NewHash);
                File.Move(IncomingPath(journal, entry), entry.Target, overwrite: true);
                RequireHash(entry.Target, entry.NewHash);
            }
            journal.Committed = true;
            WriteJournal(Path.Combine(Directories(journal.Entries.Select(entry => entry.Target))[0], JournalName), journal);
            Cleanup(journal);
            return new(true, journal.Entries.Count, $"{(preserveOriginal ? "Updated" : "Restored")} {journal.Entries.Count} existing Streamline component(s).",
                journal.Entries.Select(entry => entry.Target).ToArray());
        }
        catch (Exception ex)
        {
            if (journal is not null && journal.Entries.Count > 0)
            {
                if (!durable || Rollback(journal, out _)) Cleanup(journal);
                else return new(false, 0, $"{ex.Message} Recovery files were retained; retry recovery before updating again.");
            }
            return new(false, 0, ex.Message);
        }
        finally { DisposeLocks(locks); }
    }

    static bool Rollback(Journal journal, out string errors)
    {
        var failures = new List<string>();
        foreach (var entry in journal.Entries)
        {
            try
            {
                ValidateTarget(entry.Target);
                var current = Hash(entry.Target);
                if (current == entry.OldHash) continue; // Includes interrupted preparation and already recovered entries.
                if (current != entry.NewHash) throw new IOException("The component changed outside this operation; it was not overwritten.");
                RequireHash(RollbackPath(journal, entry), entry.OldHash);
                var recoveryStage = RecoveryStagePath(journal, entry);
                TryDelete(recoveryStage);
                CopyDurable(RollbackPath(journal, entry), recoveryStage);
                RequireHash(entry.Target, entry.NewHash);
                File.Move(recoveryStage, entry.Target, overwrite: true);
                RequireHash(entry.Target, entry.OldHash);
            }
            catch (Exception ex) { failures.Add($"{entry.Target}: {ex.Message}"); }
        }
        errors = string.Join(Environment.NewLine, failures);
        return failures.Count == 0;
    }

    static string[] Directories(IEnumerable<string> files) => files.Select(path => Path.GetDirectoryName(Path.GetFullPath(path))!)
        .Distinct(PathComparer).OrderBy(path => path, PathComparer).ToArray();

    static void AcquireLocks(IEnumerable<string> directories, List<FileStream> locks)
    {
        // Lock the complete, sorted directory set before reading or changing any
        // component. Disjoint games may proceed; a partial overlap is rejected.
        // Opening is nonblocking and callers release any acquired subset on failure.
        foreach (var dir in directories)
        {
            var path = Path.Combine(dir, ".streamline-operation.lock");
            RejectLinks(path);
            // Persistent zero-byte lock avoids unlink/recreate races between processes on Linux.
            locks.Add(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
    }
    static void DisposeLocks(List<FileStream> locks) { foreach (var item in locks) item.Dispose(); locks.Clear(); }
    static string RollbackPath(Journal j, Entry e) => e.Target + ".streamline-rollback-" + j.Id;
    static string IncomingPath(Journal j, Entry e) => e.Target + ".streamline-incoming-" + j.Id;
    static string BackupStagePath(Journal j, Entry e) => e.Target + ".streamline-original-" + j.Id;
    static string RecoveryStagePath(Journal j, Entry e) => e.Target + ".streamline-recover-" + j.Id;

    static void Cleanup(Journal journal)
    {
        foreach (var entry in journal.Entries)
        {
            TryDelete(IncomingPath(journal, entry)); TryDelete(RollbackPath(journal, entry));
            TryDelete(BackupStagePath(journal, entry)); TryDelete(RecoveryStagePath(journal, entry));
        }
        // Remove replicas first; the canonical journal remains authoritative until cleanup ends.
        var directories = Directories(journal.Entries.Select(entry => entry.Target));
        foreach (var dir in directories.Skip(1).Reverse()) TryDelete(Path.Combine(dir, JournalName));
        if (directories.Skip(1).All(dir => !File.Exists(Path.Combine(dir, JournalName)))) TryDelete(Path.Combine(directories[0], JournalName));
    }

    static void WriteJournal(string path, Journal journal)
    {
        RejectLinks(path);
        var temporary = path + ".writing-" + journal.Id;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, journal);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { TryDelete(temporary); }
    }

    static Journal ReadJournal(string path)
    {
        RejectLinks(path);
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Streamline recovery journal is too large.");
        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid Streamline journal.");
        if (journal.Version != 1 || !Guid.TryParseExact(journal.Id, "N", out _) || journal.Entries.Count == 0 || journal.Entries.Count > 1024)
            throw new InvalidDataException("Unsupported Streamline recovery journal.");
        foreach (var entry in journal.Entries)
        {
            if (!Path.IsPathFullyQualified(entry.Target) || !KnownFileNames.Contains(Path.GetFileName(entry.Target))
                || entry.OldHash.Length != 64 || entry.NewHash.Length != 64)
                throw new InvalidDataException("Invalid Streamline recovery entry.");
            RejectLinks(entry.Target);
        }
        if (!Directories(journal.Entries.Select(entry => entry.Target)).Contains(Path.GetDirectoryName(Path.GetFullPath(path))!, PathComparer))
            throw new InvalidDataException("Streamline recovery journal is outside its component directories.");
        return journal;
    }

    static void CopyDurable(string source, string destination)
    {
        RejectLinks(source); RejectLinks(destination);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }
    static string Hash(string path) { RejectLinks(path); using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    static void RequireHash(string path, string expected)
    {
        if (Hash(path) != expected) throw new IOException($"Component changed during the operation: {path}");
    }
    static void RejectLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Linked component paths are not supported: {current}");
    }
    static void ValidateTarget(string path)
    {
        RejectLinks(path);
        if (!KnownFileNames.Contains(Path.GetFileName(path)) || !File.Exists(path)) throw new IOException($"Installed component is missing or unsupported: {path}");
    }
    static void ValidateSource(string path, string expectedName)
    {
        RejectLinks(path);
        var name = Path.GetFileName(path);
        if (!name.Equals(expectedName, StringComparison.OrdinalIgnoreCase) && !name.Equals(expectedName + BackupSuffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unexpected component name: {path}");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var headers = pe.PEHeaders;
        if (headers.PEHeader is null || headers.CoffHeader.Machine != Machine.Amd64
            || (headers.CoffHeader.Characteristics & Characteristics.Dll) == 0 || headers.PEHeader.Magic != PEMagic.PE32Plus)
            throw new InvalidDataException($"The component is not a valid x64 Windows DLL: {path}");
    }
    static bool TryGetProductionFileName(string path, out string name)
    {
        const string prefix = "bin/x64/";
        path = path.Replace('\\', '/');
        name = path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path[prefix.Length..] : string.Empty;
        return !name.Contains('/') && KnownFileNames.Contains(name);
    }
    static void TryDelete(string path) { try { RejectLinks(path); File.Delete(path); } catch { /* Retained evidence is safe to clean on the next recovery. */ } }
    static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) { RejectLinks(path); Directory.Delete(path, true); } } catch { /* Retain owned staging/cache directory on cleanup failure. */ } }

    public sealed class Journal
    {
        public int Version { get; set; } = 1;
        public string Id { get; set; } = string.Empty;
        public bool Committed { get; set; }
        public List<Entry> Entries { get; set; } = [];
    }
    public sealed class Entry
    {
        public string Target { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string OldHash { get; set; } = string.Empty;
        public string NewHash { get; set; } = string.Empty;
    }
}
