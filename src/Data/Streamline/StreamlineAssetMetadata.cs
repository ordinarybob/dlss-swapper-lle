using System;
using System.IO;
using System.Linq;

namespace DLSS_Swapper.Data.Streamline;

internal sealed record StreamlineAssetDefinition(string FileName, GameAssetType Type, string DisplayName);

// Windows history reuses the existing reserved IDs without changing the catalog
// or enrolling these components in independent per-DLL batch updates.
internal static class StreamlineAssetMetadata
{
    static readonly StreamlineAssetDefinition[] Definitions =
    [
        new("sl.common.dll", GameAssetType.Streamline_Common, "Streamline Common"),
        new("sl.deepdvc.dll", GameAssetType.Streamline_DeepDVC, "Streamline DeepDVC"),
        new("sl.directsr.dll", GameAssetType.Streamline_DirectSR, "Streamline DirectSR"),
        new("sl.dlss.dll", GameAssetType.Streamline_DLSS, "Streamline DLSS"),
        new("sl.dlss_d.dll", GameAssetType.Streamline_DLSS_D, "Streamline DLSS Ray Reconstruction"),
        new("sl.dlss_g.dll", GameAssetType.Streamline_DLSS_G, "Streamline DLSS Frame Generation"),
        new("sl.interposer.dll", GameAssetType.Streamline_Interposer, "Streamline Interposer"),
        new("sl.nis.dll", GameAssetType.Streamline_NIS, "Streamline NIS"),
        new("sl.nvperf.dll", GameAssetType.Streamline_NvPerf, "Streamline NvPerf"),
        new("sl.pcl.dll", GameAssetType.Streamline_PCL, "Streamline PCL"),
        new("sl.reflex.dll", GameAssetType.Streamline_Reflex, "Streamline Reflex"),
    ];

    internal static StreamlineAssetDefinition? FindFile(string path) => Definitions.FirstOrDefault(
        item => item.FileName.Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));

    internal static string? GetDisplayName(GameAssetType type) =>
        Definitions.FirstOrDefault(item => item.Type == type)?.DisplayName;
}
