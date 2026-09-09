using System;
using System.Collections.Generic;
using System.IO;

namespace DLSS_Swapper.Data;

internal static class DiscoveryMetadata
{
    internal static bool HasBattleNetInstallationState(bool? installed, string? installPath) =>
        installed.HasValue && !string.IsNullOrWhiteSpace(installPath);

    internal static string BattleNetTitle(string? knownTitle, string? aggregateTitle, string installPath) =>
        !string.IsNullOrWhiteSpace(knownTitle) ? knownTitle :
        !string.IsNullOrWhiteSpace(aggregateTitle) ? aggregateTitle : new DirectoryInfo(installPath).Name;

    internal static string UbisoftThumbnail(string? thumbnail, Dictionary<string, Dictionary<string, string>>? localizations)
    {
        if (string.IsNullOrWhiteSpace(thumbnail)) return string.Empty;
        if (!thumbnail.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            && !thumbnail.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            if (localizations?.TryGetValue("default", out var values) != true || values is null
                || !values.TryGetValue(thumbnail, out thumbnail) || string.IsNullOrWhiteSpace(thumbnail))
                return string.Empty;
        }
        return $"https://ubistatic3-a.akamaihd.net/orbit/uplay_launcher_3_0/assets/{thumbnail}";
    }

    internal static bool CanRemoveSteamCache(string installPath, bool indexRead, IEnumerable<string> readableSteamApps)
    {
        if (!indexRead || string.IsNullOrWhiteSpace(installPath)) return false;
        var normalized = Path.GetFullPath(installPath);
        foreach (var source in readableSteamApps)
        {
            var common = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(source, "common")))
                + Path.DirectorySeparatorChar;
            if (normalized.StartsWith(common, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
