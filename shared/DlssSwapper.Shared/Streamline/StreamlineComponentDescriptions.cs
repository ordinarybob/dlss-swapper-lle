namespace DLSS_Swapper.Data.Streamline;

/// <summary>Plain-language roles, verified against NVIDIA sources listed in docs/STREAMLINE_COMPONENTS.md.</summary>
public static class StreamlineComponentDescriptions
{
    public static string GetDescription(string fileName) => fileName.ToLowerInvariant() switch
    {
        "sl.common.dll" => "Shared Streamline services that pass the game's rendering resources and frame data to feature plugins and manage their NVIDIA NGX connection.",
        "sl.deepdvc.dll" => "Connects the game to RTX Dynamic Vibrance, an AI color-enhancement effect that adjusts saturation. It is not an upscaler or frame generator.",
        "sl.directsr.dll" => "Connects the game to DirectSR super resolution through DirectX 12, using the upscaling variant selected by the game.",
        "sl.dlss.dll" => "Connects the game to DLSS Super Resolution for AI upscaling, or DLAA for anti-aliasing at native resolution. This is the Streamline plugin, not nvngx_dlss.dll.",
        "sl.dlss_d.dll" => "Connects the game to DLSS Ray Reconstruction, which replaces ray-tracing denoisers with AI reconstruction. This is the Streamline plugin, not nvngx_dlssd.dll.",
        "sl.dlss_g.dll" => "Connects the game to DLSS Frame Generation, which creates additional frames from the game's rendered frames. This is the Streamline plugin, not nvngx_dlssg.dll.",
        "sl.interposer.dll" => "The game's entry point into Streamline. It loads feature plugins and routes the graphics API calls Streamline needs.",
        "sl.nis.dll" => "NVIDIA Image Scaling: spatial upscaling and sharpening, or sharpening alone. This is a separate algorithm from DLSS.",
        "sl.nvperf.dll" => "Connects Streamline to NVIDIA Nsight Perf SDK for GPU performance measurement and profiling. It is not an upscaler or frame generator.",
        "sl.pcl.dll" => "Records PC latency markers and timing measurements during gameplay. It measures latency; Reflex handles low-latency control separately.",
        "sl.reflex.dll" => "Connects the game to NVIDIA Reflex low-latency control and its frame-rate limiter. PC latency statistics are handled separately by sl.pcl.dll in newer SDKs.",
        _ => "No description is available for this component.",
    };
}
