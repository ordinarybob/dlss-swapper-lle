
using System;
using System.Globalization;

namespace DLSS_Swapper;

internal static class BuildInfo
{
    public const string CheckpointVersion = "1.2.5.1";
    public const string CheckpointTag = "v1.2.5.1-lle";

    public static string GitBranch { get; } = string.Empty;
    public static string GitCommit { get; } = string.Empty;
    public static string GitTag { get; } = string.Empty;
    public static long BuildTimestamp { get; }


    public static string GitCommitShort
    {
        get
        {
            if (string.IsNullOrWhiteSpace(GitCommit) || GitCommit.Length < 7)
            {
                return "Not embedded";
            }

            return GitCommit.Substring(0, 7);
        }
    }
    public static DateTime? BuildDateTime => BuildTimestamp > 0
        ? DateTimeOffset.FromUnixTimeSeconds(BuildTimestamp).LocalDateTime
        : null;
    public static string BuildDateTimeFormattedString => BuildDateTime?.ToString("g", CultureInfo.CurrentCulture) ?? "Not embedded";
    public static bool IsFromTagBuild => string.IsNullOrWhiteSpace(GitTag) == false;
}
