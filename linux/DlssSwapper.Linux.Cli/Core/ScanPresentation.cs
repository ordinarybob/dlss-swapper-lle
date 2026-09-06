namespace DlssSwapper.Linux.Cli.Core;

public static class ScanPresentation
{
    public static string? Problem(ScanResult scan, bool rootAvailable, Translations? translations = null) =>
        !rootAvailable ? translations?.Get("Linux_FolderUnavailable", "Game folder unavailable") ?? "Game folder unavailable"
        : scan.Warnings.Count > 0 ? translations?.Get("Linux_ScanWarnings", "Scan has warnings") ?? "Scan has warnings" : null;

    public static string Detail(ScanResult scan, Translations? translations = null)
    {
        var singular = scan.Dlls.Count == 1;
        var fallback = singular ? "{0} supported DLL file scanned." : "{0} supported DLL files scanned.";
        return (translations?.Format(singular ? "Linux_ScannedOne" : "Linux_ScannedFiles", fallback, scan.Dlls.Count)
            ?? string.Format(fallback, scan.Dlls.Count)) + (scan.Warnings.Count == 0 ? "" : "\n" + string.Join("\n", scan.Warnings));
    }
}
