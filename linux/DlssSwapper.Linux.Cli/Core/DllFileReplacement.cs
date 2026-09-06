namespace DlssSwapper.Linux.Cli.Core;

public static class DllFileReplacement
{
    public static async Task CopyAsync(string source, string destination, string expectedMd5,
        bool overwrite, Action validatePaths, CancellationToken token)
    {
        var temporary = destination + ".incoming-" + Guid.NewGuid().ToString("N");
        var owned = false;
        try
        {
            token.ThrowIfCancellationRequested();
            validatePaths();
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                owned = true;
                await input.CopyToAsync(output, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            if (!DllScanner.ComputeMd5(temporary).Equals(expectedMd5, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Staged DLL does not match the expected file. The destination was not changed.");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(File.Exists(destination) ? destination : source));
            token.ThrowIfCancellationRequested();
            validatePaths();
            File.Move(temporary, destination, overwrite);
        }
        finally { if (owned && File.Exists(temporary)) File.Delete(temporary); }
    }
}
