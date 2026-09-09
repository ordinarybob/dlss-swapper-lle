using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace DLSS_Swapper.Data.Streamline;

public enum StreamlineFileState { Available, NotSelected, Missing, Unreadable }
public enum StreamlineChangeKind { Identical, Upgrade, Downgrade, SameVersionDifferentBytes, UnknownVersion, Unavailable }

public sealed record StreamlineFileSnapshot(string Path, string VersionText, Version? Version,
    string? Sha256, StreamlineFileState State, string? Error = null);

public sealed record StreamlineComparison(StreamlineChangeKind Kind, string Text, bool CanApply, bool Changes)
{
    public bool IsDowngrade => Kind == StreamlineChangeKind.Downgrade;
    public bool UnknownOrdering => Kind == StreamlineChangeKind.UnknownVersion;
}

public sealed record StreamlineComponentPreview(string TargetPath, string FileName, string RelativeDirectory,
    StreamlineFileSnapshot Installed, StreamlineFileSnapshot Package, StreamlineFileSnapshot Original,
    StreamlineComparison Update, StreamlineComparison Restore)
{
    public string InstalledVersion => Installed.VersionText;
    public string PackageVersion => Package.VersionText;
    public string OriginalVersion => Original.State == StreamlineFileState.Missing ? "Not saved" : Original.VersionText;
    public string UpdateText => Update.Text;
    public string RestoreText => Original.State == StreamlineFileState.Missing ? "No original backup" : Restore.Text;
    public bool CanUpdate => Update.CanApply;
    public bool CanRestore => Restore.CanApply;
    public bool UpdateChanges => Update.Changes;
    public bool RestoreChanges => Restore.Changes;
    public bool IsDowngrade => Update.IsDowngrade;
    public bool UnknownOrdering => Update.UnknownOrdering;
    public string Description => StreamlineComponentDescriptions.GetDescription(FileName);
}

public sealed class StreamlinePreviewSnapshot
{
    public ReadOnlyCollection<StreamlineComponentPreview> Components { get; }
    public bool CanUpdate => Components.Count > 0 && Components.All(item => item.CanUpdate) && Components.Any(item => item.UpdateChanges);
    public bool CanRestore => Components.Any(item => item.CanRestore && item.RestoreChanges) &&
        Components.Where(item => item.Original.State != StreamlineFileState.Missing).All(item => item.CanRestore);

    internal StreamlinePreviewSnapshot(IEnumerable<StreamlineComponentPreview> components) => Components = Array.AsReadOnly(components.ToArray());

    public StreamlinePreviewSnapshot SelectTargets(IEnumerable<string> paths)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var selected = paths.Select(Path.GetFullPath).ToHashSet(comparer);
        if (selected.Except(Components.Select(item => item.TargetPath), comparer).Any())
            throw new InvalidOperationException("A selected component is no longer available. Review the refreshed list and select again.");
        return new(Components.Where(item => selected.Contains(item.TargetPath)));
    }

    /// <summary>Compare a freshly read snapshot immediately before mutation. This does not replace mutation-time locking.</summary>
    public bool IsEquivalentTo(StreamlinePreviewSnapshot other, bool restore)
    {
        if (Components.Count != other.Components.Count) return false;
        return Components.Zip(other.Components).All(pair => pair.First.TargetPath == pair.Second.TargetPath &&
            pair.First.Installed == pair.Second.Installed &&
            (restore ? pair.First.Original == pair.Second.Original : pair.First.Package == pair.Second.Package));
    }
}

/// <summary>Explicit, read-only inspection. SDK release labels are never substituted for DLL file versions.</summary>
public static class StreamlineDecisionPreview
{
    public static StreamlinePreviewSnapshot Create(string gameRoot, IEnumerable<string> installedPaths, string? packageDirectory,
        IReadOnlyDictionary<string, StreamlineFileSnapshot>? packageSnapshot = null)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var packageFiles = new Dictionary<string, StreamlineFileSnapshot>(StringComparer.OrdinalIgnoreCase);
        var components = installedPaths.Select(Path.GetFullPath).Distinct(comparer).OrderBy(path => path, comparer).Select(path =>
        {
            var name = Path.GetFileName(path);
            var installed = ReadFile(path);
            if (!packageFiles.TryGetValue(name, out var package))
            {
                package = packageSnapshot is not null && packageSnapshot.TryGetValue(name, out var cached)
                    ? cached : packageDirectory is null
                    ? new("", "Not downloaded", null, null, StreamlineFileState.NotSelected)
                    : ReadFile(Path.Combine(packageDirectory, StreamlineComponentSet.FileNames.FirstOrDefault(
                        known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase)) ?? name));
                packageFiles.Add(name, package);
            }
            var original = ReadFile(path + StreamlineComponentSet.BackupSuffix);
            var directory = Path.GetDirectoryName(Path.GetRelativePath(gameRoot, path));
            return new StreamlineComponentPreview(path, name, string.IsNullOrEmpty(directory) ? "(game folder)" : directory,
                installed, package, original, Compare(installed, package), Compare(installed, original));
        });
        return new(components);
    }

    public static StreamlineComparison Compare(StreamlineFileSnapshot installed, StreamlineFileSnapshot source)
    {
        if (installed.State != StreamlineFileState.Available)
            return new(StreamlineChangeKind.Unavailable, "Installed file unavailable", false, false);
        if (source.State != StreamlineFileState.Available)
            return new(StreamlineChangeKind.Unavailable, source.State switch
            {
                StreamlineFileState.NotSelected => "Download a package to compare",
                StreamlineFileState.Missing => "Source file missing",
                _ => "Source file unreadable",
            }, false, false);
        if (string.IsNullOrEmpty(installed.Sha256) || string.IsNullOrEmpty(source.Sha256))
            return new(StreamlineChangeKind.Unavailable, "File comparison unavailable", false, false);
        if (string.Equals(installed.Sha256, source.Sha256, StringComparison.OrdinalIgnoreCase))
            return new(StreamlineChangeKind.Identical, "Identical — no change", true, false);
        if (installed.Version is null || source.Version is null)
            return new(StreamlineChangeKind.UnknownVersion, "Different files — version order unknown", true, true);
        var order = Normalize(source.Version).CompareTo(Normalize(installed.Version));
        return order switch
        {
            > 0 => new(StreamlineChangeKind.Upgrade, "Upgrade", true, true),
            < 0 => new(StreamlineChangeKind.Downgrade, "Downgrade", true, true),
            _ => new(StreamlineChangeKind.SameVersionDifferentBytes, "Same version — different files", true, true),
        };
    }

    static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));

    public static StreamlineFileSnapshot ReadFile(string path)
    {
        try
        {
            // Deny concurrent writers while hashing/reading metadata on Windows; no output or cache is created.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            Version? version = null;
            var text = "Unknown";
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.FileVersion))
                {
                    text = info.FileVersion;
                    version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                // Hash comparison remains useful when version resources are unavailable.
            }
            return new(path, text, version, hash, StreamlineFileState.Available);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new(path, "Missing", null, null, StreamlineFileState.Missing, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            return new(path, "Unreadable", null, null, StreamlineFileState.Unreadable, ex.Message);
        }
    }
}
