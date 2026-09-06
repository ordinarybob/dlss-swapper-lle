using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DLSS_Swapper.Data;

// Selectors deliberately retain the existing manifest schema and observable
// collections: changing how families are enumerated must not replace UI bindings.
internal sealed record DllFamily(
    GameAssetType Type,
    GameAssetType BackupType,
    string FileName,
    Func<Manifest, List<DLLRecord>> ManifestRecords,
    Func<DLLManager, ObservableCollection<DLLRecord>> Records)
{
    internal string NameResourceKey => "General_Name_" + Type;
}

internal static class DllFamilyRegistry
{
    internal static IReadOnlyList<DllFamily> All { get; } = Array.AsReadOnly(new DllFamily[]
    {
        new(GameAssetType.DLSS, GameAssetType.DLSS_BACKUP, "nvngx_dlss.dll", m => m.DLSS, m => m.DLSSRecords),
        new(GameAssetType.DLSS_G, GameAssetType.DLSS_G_BACKUP, "nvngx_dlssg.dll", m => m.DLSS_G, m => m.DLSSGRecords),
        new(GameAssetType.DLSS_D, GameAssetType.DLSS_D_BACKUP, "nvngx_dlssd.dll", m => m.DLSS_D, m => m.DLSSDRecords),
        new(GameAssetType.FSR_31_DX12, GameAssetType.FSR_31_DX12_BACKUP, "amd_fidelityfx_dx12.dll", m => m.FSR_31_DX12, m => m.FSR31DX12Records),
        new(GameAssetType.FSR_31_VK, GameAssetType.FSR_31_VK_BACKUP, "amd_fidelityfx_vk.dll", m => m.FSR_31_VK, m => m.FSR31VKRecords),
        new(GameAssetType.XeSS, GameAssetType.XeSS_BACKUP, "libxess.dll", m => m.XeSS, m => m.XeSSRecords),
        new(GameAssetType.XeSS_FG, GameAssetType.XeSS_FG_BACKUP, "libxess_fg.dll", m => m.XeSS_FG, m => m.XeSSFGRecords),
        new(GameAssetType.XeSS_DX11, GameAssetType.XeSS_DX11_BACKUP, "libxess_dx11.dll", m => m.XeSS_DX11, m => m.XeSSDX11Records),
        new(GameAssetType.XeLL, GameAssetType.XeLL_BACKUP, "libxell.dll", m => m.XeLL, m => m.XeLLRecords),
    });

    internal static DllFamily? Find(GameAssetType type) => All.FirstOrDefault(family => family.Type == type);

    // Preserve the importer's exact-name matching. Archive extraction and scans
    // apply their own filesystem-appropriate comparison rules.
    internal static DllFamily? FindFile(string fileName) => All.FirstOrDefault(family => family.FileName == fileName);

    internal static DllFamily Get(GameAssetType type) => Find(type)
        ?? throw new Exception($"Unknown AssetType: {type}");
}
