using DLSS_Swapper.Data;
using DLSS_Swapper.Data.Streamline;

internal static class StreamlineMetadataTests
{
    internal static void Run()
    {
        var expected = new (string FileName, GameAssetType Type, int Id)[]
        {
            ("sl.common.dll", GameAssetType.Streamline_Common, 53),
            ("sl.deepdvc.dll", GameAssetType.Streamline_DeepDVC, 51),
            ("sl.directsr.dll", GameAssetType.Streamline_DirectSR, 49),
            ("sl.dlss.dll", GameAssetType.Streamline_DLSS, 47),
            ("sl.dlss_d.dll", GameAssetType.Streamline_DLSS_D, 45),
            ("sl.dlss_g.dll", GameAssetType.Streamline_DLSS_G, 43),
            ("sl.interposer.dll", GameAssetType.Streamline_Interposer, 41),
            ("sl.nis.dll", GameAssetType.Streamline_NIS, 39),
            ("sl.nvperf.dll", GameAssetType.Streamline_NvPerf, 37),
            ("sl.pcl.dll", GameAssetType.Streamline_PCL, 35),
            ("sl.reflex.dll", GameAssetType.Streamline_Reflex, 33),
        };
        foreach (var (fileName, type, id) in expected)
        {
            var definition = StreamlineAssetMetadata.FindFile(Path.Combine("fixture", fileName.ToUpperInvariant()));
            if (definition is null || definition.Type != type || (int)type != id
                || StreamlineAssetMetadata.GetDisplayName(type) != definition.DisplayName
                || DllFamilyRegistry.Find(type) is not null
                || !definition.DisplayName.StartsWith("Streamline ", StringComparison.Ordinal))
                throw new Exception($"Streamline history mapping mismatch for {fileName}.");
        }
        if (StreamlineAssetMetadata.FindFile("nvngx_dlss.dll") is not null
            || StreamlineAssetMetadata.FindFile("sl.unknown.dll") is not null
            || StreamlineAssetMetadata.GetDisplayName(GameAssetType.DLSS) is not null)
            throw new Exception("Streamline history mapping claimed an unrelated DLL family.");
    }
}
