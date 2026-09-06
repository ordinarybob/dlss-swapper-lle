using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DLSS_Swapper.Data.Streamline;

/// <summary>Only finalized stable-release directories may be selected as cached SDKs.</summary>
public static class StreamlinePackageCache
{
    static readonly Regex StableTag = new(@"^[vV]?[0-9]+(?:\.[0-9]+){1,3}$", RegexOptions.CultureInvariant);

    public static bool TryGetVersion(string tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (!StableTag.IsMatch(tag) || !Version.TryParse(tag.TrimStart('v', 'V'), out var parsed))
        {
            return false;
        }

        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
        return true;
    }

    public static string? FindNewest(string root, Func<string, bool> isComplete)
    {
        if (!Directory.Exists(root)) return null;

        return Directory.EnumerateDirectories(root)
            .Select(path => new { Path = path, Version = TryGetVersion(Path.GetFileName(path), out var version) ? version : null })
            .Where(candidate => candidate.Version is not null && isComplete(candidate.Path))
            .OrderByDescending(candidate => candidate.Version)
            .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }
}
