using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record NgxModelCandidate(string Path, DllType Type, string Version, long Size);
public sealed record NgxModelDiscoveryResult(string? ModelsPath, IReadOnlyList<NgxModelCandidate> Models,
    IReadOnlyList<string> Warnings);

public static class NgxModelDiscovery
{
    public static IEnumerable<string> ConfigurationPaths()
    {
        var explicitPath = Environment.GetEnvironmentVariable("__NGX_CONF_FILE");
        if (!string.IsNullOrWhiteSpace(explicitPath)) yield return explicitPath;
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(config)) config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        yield return Path.Combine(config, "nvidia-ngx-conf.json");
        yield return "/usr/share/nvidia/nvidia-ngx-conf.json";
    }

    public static NgxModelDiscoveryResult Discover(IEnumerable<string> configurationPaths, CancellationToken token, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var warnings = new List<string>();
        foreach (var path in configurationPaths)
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(path))
                return new(null, [], [T("Linux_NgxConfigRelative", "NGX configuration path is not absolute: {0}", path)]);
            if (!File.Exists(path)) continue;
            try
            {
                if (new FileInfo(path).Length > 1024 * 1024) throw new IOException(T("Linux_NgxConfigLarge", "NGX configuration exceeds 1 MiB."));
                using var input = File.OpenRead(path);
                using var json = JsonDocument.Parse(input);
                if (!json.RootElement.TryGetProperty("ngx_models_path", out var value)
                    || value.ValueKind != JsonValueKind.String || !Path.IsPathFullyQualified(value.GetString()!))
                    throw new IOException(T("Linux_NgxConfigModelsPath", "NGX configuration needs an absolute ngx_models_path."));
                return Scan(value.GetString()!, token, translations);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            { return new(null, [], [T("Linux_NgxConfigReadFailed", "Could not read NGX configuration {0}: {1}", path, error.Message)]); }
        }
        warnings.Add(T("Linux_NgxConfigMissing", "No NGX configuration was found. No driver settings were changed and the updater was not started."));
        return new(null, [], warnings);
    }

    public static NgxModelDiscoveryResult Scan(string modelsPath, CancellationToken token, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var models = new List<NgxModelCandidate>();
        var warnings = new List<string>();
        if (!Path.IsPathFullyQualified(modelsPath)) return new(null, [], [T("Linux_NgxCacheRelative", "The model-cache path must be absolute.")]);
        if (!Directory.Exists(modelsPath)) return new(modelsPath, [], [T("Linux_NgxCacheMissing", "Model cache is missing or unavailable: {0}", modelsPath)]);
        var pending = new Stack<(string Path, int Depth)>(); pending.Push((modelsPath, 0));
        var examined = 0;
        while (pending.TryPop(out var current))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(current.Path))
                {
                    token.ThrowIfCancellationRequested();
                    if (++examined > 20000) return new(modelsPath, models, [.. warnings, T("Linux_NgxScanLimit", "Model scan stopped at its 20,000-entry limit; results are incomplete.")]);
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) { warnings.Add(T("Linux_NgxSkippedLink", "Skipped linked path: {0}", path)); continue; }
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            if (current.Depth < 16) pending.Push((path, current.Depth + 1));
                            else warnings.Add(T("Linux_NgxSkippedDepth", "Skipped deeply nested directory: {0}", path));
                            continue;
                        }
                        if (!path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                        using var input = File.OpenRead(path);
                        if (input.Length > 512L * 1024 * 1024) throw new IOException(T("Linux_NgxModelLarge", "Model exceeds the supported size limit."));
                        using var pe = new PEReader(input);
                        if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64
                            || (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) == 0
                            || pe.PEHeaders.PEHeader?.Magic != PEMagic.PE32Plus) continue;
                        var info = DlssSwapper.Shared.PeVersionInfo.Read(path);
                        var family = NgxModelIdentity.Identify(info.ProductName);
                        if (family is not null) models.Add(new(path, family.Type, info.FileVersion ?? T("Linux_Streamline_Unknown", "Unknown"), input.Length));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or BadImageFormatException)
                    { warnings.Add(T("Linux_NgxInspectFailed", "Could not inspect {0}: {1}", path, error.Message)); }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { warnings.Add(T("Linux_NgxScanFailed", "Could not scan {0}: {1}", current.Path, error.Message)); }
        }
        return new(modelsPath, models, warnings);
    }
}
