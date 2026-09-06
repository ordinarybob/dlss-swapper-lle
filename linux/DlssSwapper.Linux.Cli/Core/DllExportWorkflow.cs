using System.IO.Compression;
using System.Security.Cryptography;

namespace DlssSwapper.Linux.Cli.Core;

public static class DllExportWorkflow
{
    public static async Task<int> ExportAsync(IEnumerable<DllCatalogEntry> entries, DownloadCache cache,
        string destination, CancellationToken token)
    {
        var selected = entries.DistinctBy(entry => (entry.Type, entry.Md5)).ToArray();
        if (selected.Length == 0) throw new IOException("There are no downloaded DLLs to export.");
        destination = Path.GetFullPath(destination);
        if (selected.Any(entry => PathComparers.FileSystemPath.Equals(cache.GetCachedPath(entry), destination)))
            throw new IOException("Choose an export path outside the source DLL files.");
        var staging = destination + ".export-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var entry in selected)
                {
                    token.ThrowIfCancellationRequested();
                    var family = DllTypes.Get(entry.Type);
                    var name = selected.Length == 1 ? family.FileName
                        : $"{(entry.IsImported ? "Imported/" : "")}{family.ManifestKey}/{entry.Md5}/{family.FileName}";
                    await using var source = File.OpenRead(cache.GetCachedPath(entry));
                    await using var target = archive.CreateEntry(name).Open();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
                    var buffer = new byte[81920];
                    int count;
                    while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                    {
                        hash.AppendData(buffer, 0, count);
                        await target.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    }
                    if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(entry.Md5, StringComparison.OrdinalIgnoreCase))
                        throw new IOException($"{family.DisplayName} {entry.Version} is corrupt or changed during export. Download or import it again.");
                }
            }
            token.ThrowIfCancellationRequested();
            File.Move(staging, destination, overwrite: true);
            return selected.Length;
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
}
