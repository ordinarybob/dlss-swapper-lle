using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class LatestAcquisitionTests
{
    public static Task RunAsync()
    {
        var regular = new DllCatalogEntry(DllType.Dlss, "3.10", 310, new string('a', 32), new string('b', 32),
            new Uri("https://example.invalid/fixture.zip"), 1, 1, true, false);
        var debug = regular with { VersionNumber = 309, Version = "3.9", IsDevFile = true, Md5 = new string('c', 32) };
        if (DllReleaseDisplay.Name(regular with { Version = "2.5.0.0", AdditionalLabel = "preview" }) != "v2.5 (preview)"
            || DllReleaseDisplay.Name(debug with { IsSignatureValid = false }) != "v3.9 (Debug) (Untrusted)"
            || DllReleaseDisplay.Name(regular with { Type = DllType.Fsr31Dx12, Version = "1.0.1.41314", InternalName = "3.1.4" }) != "v3.1.4 (v1.0.1.41314)"
            || DllReleaseDisplay.Name(regular with { Version = "1.0.0.0" }) != "v1.0")
            throw new Exception("Release display lost public/raw version, labels or trust/debug information.");
        var old = regular with { VersionNumber = 308 };
        var imported = regular with { VersionNumber = 999, IsImported = true };
        var selected = LibraryDownloadWorkflow.SelectLatestEligible([old, debug, regular, imported]);
        if (selected.Count != 2 || !selected.Contains(regular) || !selected.Contains(debug))
            throw new Exception("Latest regular/debug tracks were merged or imports selected.");
        if (LibraryDownloadWorkflow.SelectLatestEligible([regular, old]).Single() != regular)
            throw new Exception("Regular-only selection changed.");
        var summary = LibraryDownloadWorkflow.Describe([
            new(regular, LibraryDownloadStatus.Cached), new(debug, LibraryDownloadStatus.Downloaded)]);
        if (summary.Contains(regular.Md5[..8]) || !summary.Contains("(Debug)") || !summary.Contains(debug.Md5[..8]))
            throw new Exception("Acquisition summary reported cached item or omitted acquired debug identity.");
        if (!LibraryDownloadWorkflow.Describe([new(debug, LibraryDownloadStatus.Cached)]).StartsWith("No new files"))
            throw new Exception("Cached debug item was reported as downloaded.");
        return Task.CompletedTask;
    }
}
