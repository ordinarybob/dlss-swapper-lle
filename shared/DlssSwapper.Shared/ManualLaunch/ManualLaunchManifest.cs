using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text.RegularExpressions;

namespace DLSS_Swapper.Data.ManuallyAdded;

public sealed record ManualLaunchManifest(string Executable, string Arguments, string WorkingDirectory)
{
    public static ManualLaunchManifest Validate(string executable, string arguments, string workingDirectory)
    {
        if (!Path.IsPathFullyQualified(executable.Trim())) throw new IOException("Choose the full path to an executable.");
        var path = Path.GetFullPath(executable.Trim());
        if (IsExcluded(path)) throw new IOException("This executable is excluded from game launching.");
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new IOException("Choose an existing .exe file.");
        if (!string.IsNullOrWhiteSpace(workingDirectory) && !Path.IsPathFullyQualified(workingDirectory.Trim())) throw new IOException("Choose the full path to a working folder.");
        var folder = string.IsNullOrWhiteSpace(workingDirectory) ? Path.GetDirectoryName(path)! : Path.GetFullPath(workingDirectory.Trim());
        if (!Directory.Exists(folder)) throw new IOException("Choose an existing working folder.");
        return new(path, arguments, folder);
    }

    public sealed record Candidate(string Path, string Label);
    public static bool IsExcluded(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return (Path.GetDirectoryName(path) ?? "").Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Contains("Artbook", StringComparison.OrdinalIgnoreCase));
    }
    public static List<Candidate> FindCandidates(string root, string? title = null,
        IEnumerable<string>? preferredDirectories = null, Func<string, string>? normalizeTitle = null)
    {
        // Use the same exhaustive enumeration approach as the asset deep scan.
        // Do not let directory depth or enumeration order hide the real executable.
        var files = new FileSystemEnumerable<string>(root,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true,
                BufferSize = 64 * 1024, AttributesToSkip = FileAttributes.ReparsePoint })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory
                && entry.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !IsSupportDirectory(entry.FileName.ToString()),
        };
        var preferred = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (preferredDirectories is not null)
        {
            foreach (var directory in preferredDirectories)
            {
                if (IsExcluded(Path.Combine(directory, "candidate.exe")) || HasSupportDirectory(directory)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*.exe", new EnumerationOptions { IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive, AttributesToSkip = FileAttributes.ReparsePoint }))
                        preferred.Add(file);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        var candidates = preferred.Concat(files)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsSuggestedExecutable)
            .Select(path => new Candidate(path, Path.GetRelativePath(root, path)))
            .ToList();
        return Rank(candidates, title ?? Path.GetFileName(root), normalizeTitle, preferred);
    }

    public static bool IsSuggestedExecutable(string path) => !IsExcluded(path)
        && !IsUtility(Path.GetFileNameWithoutExtension(path));

    public sealed record ExecutableMetadata(string ProductName, string Description, string OriginalFileName);

    public static List<Candidate> RankCandidates(IEnumerable<Candidate> candidates, string title,
        Func<string, ExecutableMetadata>? readMetadata = null) =>
        Rank(candidates.ToList(), title, null, new HashSet<string>(), readMetadata);

    static List<Candidate> Rank(List<Candidate> candidates, string title,
        Func<string, string>? normalizeTitle, HashSet<string> preferred,
        Func<string, ExecutableMetadata>? readMetadata = null)
    {
        normalizeTitle ??= Normalize;
        readMetadata ??= ReadMetadata;
        var eligible = candidates.Where(item => IsSuggestedExecutable(item.Path)
            && !HasSupportDirectory(Path.GetDirectoryName(item.Label) ?? "")).ToList();
        // Companion roles are meaningful only beside the corresponding application.
        // Handle both "app-room.exe" and "app-room_ea.exe", without banning games
        // merely because their title contains "room", "server", or "console".
        static string Key(string path, string stem) => (Path.GetDirectoryName(path) ?? "") + "\0" + stem;
        var standardNames = eligible.Select(item => Key(item.Path, Path.GetFileNameWithoutExtension(item.Path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        (string? Base, string? Role) Companion(Candidate item)
        {
            var name = Path.GetFileNameWithoutExtension(item.Path);
            var match = Regex.Match(name, @"[-_.](cmd|console|room|server|dedicated)(?=[-_.]|$)", RegexOptions.IgnoreCase);
            if (!match.Success) return (null, null);
            var stem = name.Remove(match.Index, match.Length);
            return standardNames.Contains(Key(item.Path, stem))
                ? (stem, match.Groups[1].Value.ToLowerInvariant()) : (null, null);
        }
        var related = eligible.Select(item => (Item: item, Companion: Companion(item))).ToArray();
        var frontends = related.Where(item => item.Companion.Base is not null)
            .Select(item => Key(item.Item.Path, item.Companion.Base!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Read each candidate's small version resource once. Metadata is evidence
        // for ranking, not proof that the file is safe or compatible.
        return related.Where(item => item.Companion.Role is not ("room" or "server" or "dedicated"))
            .Select(relatedItem =>
            {
                var item = relatedItem.Item;
                var metadata = readMetadata(item.Path);
                var name = Path.GetFileNameWithoutExtension(item.Path);
                var commandVariant = relatedItem.Companion.Role is "cmd" or "console";
                if (commandVariant) name = relatedItem.Companion.Base!;
                var identity = Math.Max(TitleMatch(name, title, normalizeTitle),
                    Math.Max(TitleMatch(metadata.ProductName, title, normalizeTitle),
                        Math.Max(TitleMatch(metadata.Description, title, normalizeTitle),
                            TitleMatch(Path.GetFileNameWithoutExtension(metadata.OriginalFileName), title, normalizeTitle))));
                var launcher = Words(name).Any(word => word is "launcher" or "loader" or "launch");
                var depth = item.Label.Count(c => c is '/' or '\\');
                var score = identity * 10 - (launcher ? 200 : 0) - (commandVariant ? 100 : 0)
                    + (preferred.Contains(item.Path) ? 10 : 0)
                    + (frontends.Contains(Key(item.Path, Path.GetFileNameWithoutExtension(item.Path))) ? 20 : 0)
                    - (HasBackupDirectory(Path.GetDirectoryName(item.Label) ?? "") ? 1100 : 0)
                    + (Words(name).Contains("shipping") ? 5 : 0) - Math.Min(depth, 20);
                return (Item: item, Score: score, Utility: IsUtility(metadata.Description)
                    || IsUtility(Path.GetFileNameWithoutExtension(metadata.OriginalFileName)));
            })
            .Where(item => !item.Utility)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Item.Label, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Item).ToList();
    }

    static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    static string[] Words(string value)
    {
        value = Regex.Replace(value, @"(?<=\p{L})['’]s\b", "", RegexOptions.IgnoreCase);
        var separated = Regex.Replace(value, @"(?<=[\p{Ll}\d])(?=\p{Lu})|(?<=\p{Lu})(?=\p{Lu}\p{Ll})", " ");
        return Regex.Matches(separated, @"[\p{L}]+|\d+")
            .Select(match => match.Value.ToLowerInvariant()).ToArray();
    }

    static bool IsUtility(string value)
    {
        var words = Words(value);
        var compact = Normalize(value);
        // Component fingerprints, not game titles or broad "process"/"tool" bans.
        if (Regex.IsMatch(compact, @"^(qtwebengineprocess|cefsharpbrowsersubprocess|cefsubprocess|unitycrashhandler)(32|64)?$"))
            return true;
        if (compact is "gamelaunchhelper" or "crsuploader" or "netimguiserver") return true;
        if (words.Contains("shader") && words.Any(word => word is "tool" or "compiler" or "compile")) return true;
        if (words.Contains("repair") && words.Contains("tool")) return true;
        if (words.Length > 1 && (words[^1] is "config" or "configurator")) return true;
        return words.Any(word => word is "installer" or "uninstaller" or "uninstall" or "unins"
            or "setup" or "redist" or "redistributable" or "redistributables"
            or "helper" or "updater" or "diagnostic" or "diagnostics" or "editor"
            or "watchdog" or "crashreport" or "crashreporter" or "crashhandler"
            or "crashpad" or "cefsubprocess")
            || (words.Contains("crash") && words.Any(word => word is "report" or "reporter" or "handler"));
    }

    static bool IsSupportDirectory(string name) => name.Contains("Artbook", StringComparison.OrdinalIgnoreCase)
        || name.TrimStart('_').ToLowerInvariant() is "redist" or "redistributables" or "prerequisites" or "installers";
    static bool HasSupportDirectory(string path) => path.Split('/', '\\').Any(IsSupportDirectory);
    static bool HasBackupDirectory(string path) => path.Split('/', '\\')
        .Any(part => part.ToLowerInvariant() is "backup" or "backups" or "old" or ".backup");

    static string CanonicalNumber(string word) => word switch
    {
        "ii" => "2", "iii" => "3", "iv" => "4", "v" => "5", "vi" => "6",
        "vii" => "7", "viii" => "8", "ix" => "9", "x" => "10", _ => word,
    };
    static bool IsFiller(string word) => word is "the" or "and" or "of" or "for" or "a" or "an"
        or "edition" or "deluxe" or "ultimate" or "complete" or "remastered" or "goty";

    static int TitleMatch(string value, string title, Func<string, string> normalize)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(title)) return 0;
        // Strip only known build/API suffixes; keep sequel numbers intact.
        value = Regex.Replace(value, @"(?:[-_. ](?:win32|win64|x86|x64|shipping|dx11|dx12|d3d11|d3d12))+$", "", RegexOptions.IgnoreCase);
        var name = normalize(value);
        var game = normalize(title);
        if (name == game && name.Length > 0) return 100;
        var titleWords = Words(title).Where(word => !IsFiller(word)).Select(CanonicalNumber).ToArray();
        var nameWords = Words(value).Select(CanonicalNumber).ToArray();
        var coreTitle = normalize(string.Concat(titleWords));
        var coreName = normalize(string.Concat(nameWords));
        if (coreTitle.Length >= 3 && coreName == coreTitle) return 100;
        var titleNumbers = titleWords.Where(word => word.All(char.IsDigit)).ToArray();
        var nameNumbers = nameWords.Where(word => word.All(char.IsDigit)).ToArray();
        if (titleNumbers.Length > 0 && nameNumbers.Length > 0 && !titleNumbers.SequenceEqual(nameNumbers)) return 0;
        // Initials plus intact sequel numbers: e.g. "Northern Star 12" -> "ns12".
        var acronym = string.Concat(titleWords.Select(word => word.All(char.IsDigit) ? word : word[..1]));
        if (titleWords.Count(word => !word.All(char.IsDigit)) >= 2 && acronym.Length >= 2 && coreName == acronym) return 90;
        if (coreTitle.Length >= 3 && coreName.StartsWith(coreTitle, StringComparison.Ordinal)) return 80;
        if (coreName.Length >= 3 && coreTitle.StartsWith(coreName, StringComparison.Ordinal)) return 50;
        return titleWords.Any(word => word.Length >= 3 && !word.All(char.IsDigit)
            && name.Contains(normalize(word), StringComparison.Ordinal)) ? 25 : 0;
    }

    static ExecutableMetadata ReadMetadata(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new(info.ProductName ?? "", info.FileDescription ?? "", info.OriginalFilename ?? "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or ArgumentException)
        {
            return new("", "", "");
        }
    }
}
