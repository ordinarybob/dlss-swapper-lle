using System.IO.Compression;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record DllImportSourceResult(string Source, bool Success, string Message);

/// <summary>Reads supported inputs; delegates validation/commit and never extracts archive paths to disk.</summary>
public static class DllImportSources
{
    public static string Describe(IReadOnlyList<DllImportSourceResult> results, Translations? translations = null)
    {
        const string fallback = "{0} succeeded · {1} failed or cancelled. No game files were changed.\n";
        var succeeded = results.Count(result => result.Success);
        return (translations?.Format("Linux_ImportSummary", fallback, succeeded, results.Count - succeeded) ?? string.Format(fallback, succeeded, results.Count - succeeded))
            + string.Join("\n", results.Select(result => $"{(result.Success ? translations?.Get("Linux_ImportOK", "OK") ?? "OK" : translations?.Get("Linux_NotImported", "Not imported") ?? "Not imported")}: {result.Source}: {result.Message}"));
    }
    private const long MaximumDllBytes = 512L * 1024 * 1024;
    private const long MaximumArchiveExpandedBytes = 2L * 1024 * 1024 * 1024;

    public static async Task<IReadOnlyList<DllImportSourceResult>> ReadAsync(IEnumerable<string> paths,
        Func<string, Stream, CancellationToken, Task<string>> import, CancellationToken token, Translations? translations = null)
    {
        string T(string key, string fallback) => translations?.Get(key, fallback) ?? fallback;
        var results = new List<DllImportSourceResult>();
        foreach (var path in paths)
        {
            if (token.IsCancellationRequested) { results.Add(new(path, false, T("Linux_ImportCancelled", "Not imported: cancelled."))); continue; }
            try
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    await using var stream = File.OpenRead(path);
                    if (stream.Length > MaximumDllBytes) throw new IOException(T("Linux_ImportDllTooLarge", "DLL exceeds the supported size limit."));
                    await ReadOne(Path.GetFileName(path), path, stream);
                }
                else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var archive = ZipFile.OpenRead(path);
                    var files = archive.Entries.Where(entry => entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (files.Length == 0) throw new IOException(T("Linux_ImportZipEmpty", "The ZIP contains no DLL files."));
                    if (files.Length > 1000 || files.Any(file => file.Length > MaximumDllBytes)
                        || files.Sum(file => file.Length) > MaximumArchiveExpandedBytes)
                        throw new IOException(T("Linux_ImportZipTooLarge", "The ZIP exceeds the supported import size or entry limit."));
                    foreach (var file in files)
                    {
                        var source = $"{path}: {file.FullName}";
                        if (token.IsCancellationRequested) { results.Add(new(source, false, T("Linux_ImportCancelled", "Not imported: cancelled."))); continue; }
                        try { await using var stream = file.Open(); await ReadOne(file.Name, source, stream); }
                        catch (Exception ex) when (ex is IOException or InvalidDataException) { results.Add(new(source, false, ex.Message)); }
                    }
                }
                else results.Add(new(path, false, T("Linux_ImportChooseDllZip", "Choose a DLL or ZIP file.")));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            { results.Add(new(path, false, ex.Message)); }
        }
        return results;

        async Task ReadOne(string name, string source, Stream stream)
        {
            try
            {
                // Copy with a hard streaming limit: ZIP metadata is not a trusted allocation size.
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, token).ConfigureAwait(false)) != 0)
                {
                    if (buffer.Length + read > MaximumDllBytes) throw new IOException(T("Linux_ImportDllTooLarge", "DLL exceeds the supported size limit."));
                    await buffer.WriteAsync(chunk.AsMemory(0, read), token).ConfigureAwait(false);
                }
                buffer.Position = 0;
                results.Add(new(source, true, await import(name, buffer, token).ConfigureAwait(false)));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { results.Add(new(source, false, ex is OperationCanceledException ? T("Linux_ImportCancelled", "Not imported: cancelled.") : ex.Message)); }
        }
    }
}
