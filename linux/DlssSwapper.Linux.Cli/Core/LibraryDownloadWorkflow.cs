namespace DlssSwapper.Linux.Cli.Core;

public enum LibraryDownloadStatus { Downloaded, Cached, Failed, Cancelled, NotAttempted }

public sealed record LibraryDownloadResult(DllCatalogEntry Entry, LibraryDownloadStatus Status, string? Error = null)
{
    public string Label => $"{DllTypes.Get(Entry.Type).DisplayName} {DllReleaseDisplay.Name(Entry)} ({Entry.Md5[..8]})";
}

public static class LibraryDownloadWorkflow
{
    // Input must already honor catalog trust/debug policy, as DllCatalog.GetEntries does.
    public static IReadOnlyList<DllCatalogEntry> SelectLatestEligible(IEnumerable<DllCatalogEntry> eligible) =>
        eligible.Where(entry => !entry.IsImported)
            .GroupBy(entry => (entry.Type, entry.IsDevFile))
            .Select(group => group.OrderByDescending(entry => entry.VersionNumber).First()).ToArray();

    public static async Task<IReadOnlyList<LibraryDownloadResult>> RunAsync(
        IEnumerable<DllCatalogEntry> entries,
        Func<DllCatalogEntry, CancellationToken, Task<CacheAcquisition>> acquire,
        CancellationToken cancellationToken,
        IProgress<LibraryDownloadResult>? progress = null)
    {
        var results = new List<LibraryDownloadResult>();
        var cancelled = false;
        foreach (var entry in entries.DistinctBy(item => (item.Type, item.Version, item.Md5)))
        {
            LibraryDownloadResult result;
            if (cancelled || cancellationToken.IsCancellationRequested)
                result = new(entry, LibraryDownloadStatus.NotAttempted);
            else
            {
                try
                {
                    var acquired = await acquire(entry, cancellationToken).ConfigureAwait(false);
                    result = new(entry, acquired.WasDownloaded ? LibraryDownloadStatus.Downloaded : LibraryDownloadStatus.Cached);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    result = new(entry, LibraryDownloadStatus.Cancelled);
                }
                catch (Exception error)
                {
                    result = new(entry, LibraryDownloadStatus.Failed, error.Message);
                }
            }
            results.Add(result);
            progress?.Report(result);
        }
        return results;
    }

    public static string Describe(IReadOnlyList<LibraryDownloadResult> results, IReadOnlyList<string>? additionalDownloads = null, Translations? translations = null)
    {
        string Get(string key, string fallback) => translations?.Get(key, fallback) ?? fallback;
        string Format(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var downloaded = results.Where(item => item.Status == LibraryDownloadStatus.Downloaded).ToArray();
        var lines = new List<string> { downloaded.Length == 0 && (additionalDownloads?.Count ?? 0) == 0 ? Get("Linux_NoDownloads", "No new files were downloaded.") : Get("Linux_DownloadHeading", "Downloaded:") };
        lines.AddRange(downloaded.Select(item => item.Label));
        if (additionalDownloads is not null) lines.AddRange(additionalDownloads);
        lines.AddRange(results.Where(item => item.Status == LibraryDownloadStatus.Failed)
            .Select(item => Format("Linux_DownloadFailed", "Failed: {0}: {1}", item.Label, item.Error)));
        var unfinished = results.Count(item => item.Status is LibraryDownloadStatus.Cancelled or LibraryDownloadStatus.NotAttempted);
        if (unfinished > 0) lines.Add(Format("Linux_DownloadCancelled", "Cancelled: {0} item(s) were not completed.", unfinished));
        lines.Add(Get("Linux_NoGameChanges", "No game files were changed."));
        return string.Join(Environment.NewLine, lines);
    }
}
