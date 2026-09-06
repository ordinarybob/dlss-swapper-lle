namespace DLSS_Swapper.Data.Streamline;

public static class StreamlineBatchSelection
{
    public static IReadOnlyList<string> SelectPaths(IEnumerable<string> installed, IEnumerable<string> selectedNames)
    {
        var names = selectedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Except(StreamlineComponentSet.FileNames, StringComparer.OrdinalIgnoreCase).Any())
            throw new ArgumentException("Unknown Streamline component selection.", nameof(selectedNames));
        return installed.Where(path => names.Contains(Path.GetFileName(path)))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();
    }
}
