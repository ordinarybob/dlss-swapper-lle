namespace DlssSwapper.Linux.Cli.Core;

public static class DllCatalogRefresh
{
    public static readonly Uri Source = new("https://beeradmoore.github.io/dlss-swapper/manifest.json");

    public static async Task<DllCatalog> FetchAsync(HttpClient http, string destination,
        CancellationToken token)
    {
        destination = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var staged = destination + ".refresh-" + Guid.NewGuid().ToString("N");
        try
        {
            using var response = await http.GetAsync(Source, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    total += count;
                    if (total > 32L * 1024 * 1024) throw new IOException("The catalog exceeds the supported size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                }
            }
            var catalog = DllCatalog.Load(staged);
            token.ThrowIfCancellationRequested();
            File.Move(staged, destination, overwrite: true);
            return catalog;
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }
}
