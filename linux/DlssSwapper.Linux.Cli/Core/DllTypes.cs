namespace DlssSwapper.Linux.Cli.Core;

public enum DllType
{
    Dlss,
    DlssRayReconstruction,
    DlssFrameGeneration,
    Fsr31Dx12,
    Fsr31Vulkan,
    XeSs,
    XeLl,
    XeSsFrameGeneration,
    XeSsDx11,
}

public sealed record DllTypeDefinition(
    DllType Type,
    string ManifestKey,
    string FileName,
    string DisplayName);

public static class DllTypes
{
    private static readonly DllTypeDefinition[] Definitions =
    [
        new(DllType.Dlss, "dlss", "nvngx_dlss.dll", "DLSS"),
        new(DllType.DlssRayReconstruction, "dlss_d", "nvngx_dlssd.dll", "DLSS Ray Reconstruction"),
        new(DllType.DlssFrameGeneration, "dlss_g", "nvngx_dlssg.dll", "DLSS Frame Generation"),
        new(DllType.Fsr31Dx12, "fsr_31_dx12", "amd_fidelityfx_dx12.dll", "FSR 3.1 DirectX 12"),
        new(DllType.Fsr31Vulkan, "fsr_31_vk", "amd_fidelityfx_vk.dll", "FSR 3.1 Vulkan"),
        new(DllType.XeSs, "xess", "libxess.dll", "XeSS"),
        new(DllType.XeLl, "xell", "libxell.dll", "XeLL"),
        new(DllType.XeSsFrameGeneration, "xess_fg", "libxess_fg.dll", "XeSS Frame Generation"),
        new(DllType.XeSsDx11, "xess_dx11", "libxess_dx11.dll", "XeSS DirectX 11"),
    ];

    public static IReadOnlyList<DllTypeDefinition> All => Definitions;

    public static DllTypeDefinition Get(DllType type) =>
        Definitions.First(definition => definition.Type == type);

    public static bool TryFromManifestKey(string? key, out DllTypeDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.ManifestKey, key, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static bool TryFromFileName(string? path, out DllTypeDefinition definition)
    {
        var fileName = Path.GetFileName(path);
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }
}
