namespace DlssSwapper.Linux.Cli.Core;

public static class GameDllChoices
{
    public static IReadOnlyList<DllCatalogEntry> Eligible(ScanResult scan, DllType family, DllCatalog catalog) =>
        catalog.GetEntries(family).Where(entry => new UpdatePlanner().Plan([scan], new Dictionary<DllType, DllCatalogEntry> { [family] = entry })
            .Any(item => item.Status != UpdatePlanStatus.Skipped)).ToArray();

    public static DllCatalogEntry? Current(ScanResult scan, DllType family, IReadOnlyList<DllCatalogEntry> entries)
    {
        var hashes = scan.Dlls.Where(dll => dll.Type == family).Select(dll => dll.Md5).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return hashes.Length == 1 ? entries.FirstOrDefault(entry => entry.Md5.Equals(hashes[0], StringComparison.OrdinalIgnoreCase)) : null;
    }
}
