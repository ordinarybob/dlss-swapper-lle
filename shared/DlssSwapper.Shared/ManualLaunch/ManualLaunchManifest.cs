using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 6, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive, AttributesToSkip = FileAttributes.ReparsePoint };
        var preferred = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (preferredDirectories is not null)
        {
            foreach (var directory in preferredDirectories)
            {
                if (IsExcluded(Path.Combine(directory, "candidate.exe"))) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*.exe", new EnumerationOptions { IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive, AttributesToSkip = FileAttributes.ReparsePoint }))
                        preferred.Add(file);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        var candidates = preferred.Concat(Directory.EnumerateFiles(root, "*.exe", options).Take(1000))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsSuggestedExecutable)
            .Select(path => new Candidate(path, Path.GetRelativePath(root, path)))
            .ToList();
        return Rank(candidates, title ?? Path.GetFileName(root), normalizeTitle, preferred);
    }

    public static bool IsSuggestedExecutable(string path) => !IsExcluded(path)
        && !new[] { "unins", "setup", "crash", "report", "redist" }.Any(word => Path.GetFileName(path).Contains(word, StringComparison.OrdinalIgnoreCase));

    public static List<Candidate> RankCandidates(IEnumerable<Candidate> candidates, string title) =>
        Rank(candidates.ToList(), title, null, new HashSet<string>());

    static List<Candidate> Rank(List<Candidate> candidates, string title,
        Func<string, string>? normalizeTitle, HashSet<string> preferred)
    {
        // Only demote a command-line variant when its standard counterpart exists.
        var standardNames = candidates.Select(item => Path.GetFileNameWithoutExtension(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool IsCommandVariant(Candidate item)
        {
            var name = Path.GetFileNameWithoutExtension(item.Path);
            return name.EndsWith("-cmd", StringComparison.OrdinalIgnoreCase)
                && standardNames.Contains(name[..^4]);
        }
        return candidates
            .OrderByDescending(item => TitleMatch(IsCommandVariant(item)
                ? item with { Path = Path.Combine(Path.GetDirectoryName(item.Path)!, Path.GetFileNameWithoutExtension(item.Path)[..^4] + Path.GetExtension(item.Path)) } : item, title, normalizeTitle))
            .ThenBy(IsCommandVariant)
            .ThenByDescending(item => Score(item, title, normalizeTitle)
                + (preferred.Contains(item.Path) ? 10 : 0))
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    static int TitleMatch(Candidate candidate, string title, Func<string, string>? normalizeTitle)
    {
        normalizeTitle ??= Normalize;
        var name = normalizeTitle(Path.GetFileNameWithoutExtension(candidate.Path));
        var game = normalizeTitle(title);
        if (game.Length == 0) return 0;
        if (name == game) return 3;
        if (game.Length >= 3 && name.Contains(game, StringComparison.Ordinal)) return 2;
        if (name.Length >= 3 && game.Contains(name, StringComparison.Ordinal)) return 1;
        // Meaningful title words cover shortened names such as Wake.exe or Alan-Win64-Shipping.exe.
        var words = new string(title.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Select(normalizeTitle).Any(word => word.Length >= 3
            && word is not ("the" or "and" or "for" or "of" or "edition" or "game")
            && name.Contains(word, StringComparison.Ordinal)) ? 1 : 0;
    }

    static int Score(Candidate candidate, string title, Func<string, string>? normalizeTitle)
    {
        normalizeTitle ??= Normalize;
        var name = normalizeTitle(Path.GetFileNameWithoutExtension(candidate.Path));
        var game = normalizeTitle(title);
        var depth = candidate.Label.Count(c => c == Path.DirectorySeparatorChar);
        var score = 40 - depth * 5;
        if (game.Length >= 3 && name == game) score += 120;
        else if (game.Length >= 3 && name.StartsWith(game, StringComparison.Ordinal)) score += 80;
        if (name.Contains("shipping", StringComparison.Ordinal)) score += 20;
        if (new[] { "helper", "service", "diagnostic", "benchmark", "editor", "updater" }
            .Any(word => name.Contains(word, StringComparison.Ordinal))) score -= 150;
        return score;
    }
}
