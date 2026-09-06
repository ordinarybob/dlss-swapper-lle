namespace DlssSwapper.Linux.Cli.Core;

public static class CacheFileWriter
{
    // The caller verifies payload identity before publishing it to the cache.
    public static async Task WriteAsync(string destination, ReadOnlyMemory<byte> contents, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var temporary = destination + ".incoming-" + Guid.NewGuid().ToString("N");
        var owned = false;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                owned = true;
                await output.WriteAsync(contents, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (owned && File.Exists(temporary)) File.Delete(temporary); }
    }
}
