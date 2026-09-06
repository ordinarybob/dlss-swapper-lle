using System.IO.Compression;
using System.Text.Json;

namespace DlssSwapper.Linux.Cli.Platform;

public static class GogLocalArtwork
{
    // Read the installer's cover reference; never extract or modify its archive.
    public static string? ReadCover(string root, ICollection<string> warnings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var path = Path.Combine(root, "webcache.zip");
        if (!File.Exists(path)) return null;
        try
        {
            using var input = File.OpenRead(path);
            if (input.Length > 64 * 1024 * 1024) throw new IOException("Artwork archive exceeds 64 MiB.");
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            var entry = archive.GetEntry("resources.json");
            if (entry is null) return null;
            if (entry.Length > 1024 * 1024) throw new IOException("Artwork metadata exceeds 1 MiB.");
            using var content = entry.Open();
            var bytes = new byte[checked((int)entry.Length)];
            content.ReadExactly(bytes);
            if (content.ReadByte() != -1) throw new IOException("Artwork metadata length changed.");
            token.ThrowIfCancellationRequested();
            using var json = JsonDocument.Parse(bytes);
            if (!json.RootElement.TryGetProperty("logo", out var logo) || logo.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(logo.GetString())) return null;
            return "https://images.gog.com/" + logo.GetString()!.Replace("glx_logo", "glx_vertical_cover", StringComparison.Ordinal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            warnings.Add($"Could not read GOG artwork in {path}: {error.Message}");
            return null;
        }
    }
}
