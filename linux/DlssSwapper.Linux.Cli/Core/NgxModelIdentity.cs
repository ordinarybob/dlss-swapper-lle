namespace DlssSwapper.Linux.Cli.Core;

public static class NgxModelIdentity
{
    // Product identity is independent of signature trust and of the cache directory name.
    public static DllTypeDefinition? Identify(string? productName) => productName switch
    {
        "NVIDIA Deep Learning SuperSampling" or "NGX DL SuperSampling" => DllTypes.Get(DllType.Dlss),
        "NVIDIA DLSS Ray Reconstruction" => DllTypes.Get(DllType.DlssRayReconstruction),
        "NVIDIA DLSS-G MFGLW" => DllTypes.Get(DllType.DlssFrameGeneration),
        _ => null,
    };
}
