namespace DlssSwapper.Linux.Gui;

// Translate shared preview labels only at the GUI boundary; CLI output stays unchanged.
internal static class StreamlineDisplay
{
    internal static string Description(string fileName)
    {
        var known = DLSS_Swapper.Data.Streamline.StreamlineComponentSet.FileNames
            .FirstOrDefault(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase));
        var key = known is null ? "unknown" : known[3..^4].ToLowerInvariant();
        return LanguageAppearance.Get("Linux_StreamlineDescription_" + key,
            DLSS_Swapper.Data.Streamline.StreamlineComponentDescriptions.GetDescription(fileName));
    }

    internal static string Text(string value)
    {
        var key = value switch
        {
            "Not saved" => "NotSaved",
            "No original backup" => "NoBackup",
            "Unknown" => "Unknown",
            "Missing" => "Missing",
            "Unreadable" => "Unreadable",
            "Not downloaded" => "NotDownloaded",
            "Installed file unavailable" => "InstalledUnavailable",
            "Download a package to compare" => "ComparisonNeedsPackage",
            "Source file missing" => "SourceMissing",
            "Source file unreadable" => "SourceUnreadable",
            "File comparison unavailable" => "ComparisonUnavailable",
            "Identical — no change" => "Identical",
            "Different files — version order unknown" => "UnknownOrder",
            "Upgrade" => "Upgrade",
            "Downgrade" => "Downgrade",
            "Same version — different files" => "DifferentBytes",
            "(game folder)" => "GameFolder",
            _ => null
        };
        return key is null ? value : LanguageAppearance.Get("Linux_Streamline_" + key, value);
    }
}
