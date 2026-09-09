using System.IO;
using System.Reflection.PortableExecutable;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.Helpers;

internal static class NgxPayloadIdentity
{
    public static bool Matches(string path, string? productName, GameAssetType family)
    {
        using var stream = File.OpenRead(path);
        return Matches(stream, productName, family);
    }

    public static bool Matches(Stream stream, string? productName, GameAssetType family)
    {
        var productMatches = family switch
        {
            GameAssetType.DLSS => productName is "NVIDIA Deep Learning SuperSampling" or "NGX DL SuperSampling",
            GameAssetType.DLSS_D => productName == "NVIDIA DLSS Ray Reconstruction",
            GameAssetType.DLSS_G => productName == "NVIDIA DLSS-G MFGLW",
            _ => false,
        };
        if (!productMatches) return false;
        try
        {
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            return pe.PEHeaders.CoffHeader.Machine == Machine.Amd64
                && pe.PEHeaders.PEHeader?.Magic == PEMagic.PE32Plus
                && pe.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.Dll);
        }
        catch (System.BadImageFormatException) { return false; }
    }
}
