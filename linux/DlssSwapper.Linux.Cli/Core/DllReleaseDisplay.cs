namespace DlssSwapper.Linux.Cli.Core;

public static class DllReleaseDisplay
{
    public static string Name(DllCatalogEntry entry)
    {
        var version = entry.Version;
        while (version.EndsWith(".0", StringComparison.Ordinal)) version = version[..^2];
        if (version.Length == 1) version += ".0";
        var isFsr = entry.Type is DllType.Fsr31Dx12 or DllType.Fsr31Vulkan;
        if (isFsr && !string.IsNullOrWhiteSpace(entry.InternalName)) version = entry.InternalName;
        var name = $"v{version}" + (entry.IsDevFile ? " (Debug)" : "");
        if (isFsr) name += $" (v{entry.Version})";
        else if (!string.IsNullOrWhiteSpace(entry.AdditionalLabel)) name += $" ({entry.AdditionalLabel})";
        return name + (!entry.IsSignatureValid ? " (Untrusted)" : "");
    }
}
