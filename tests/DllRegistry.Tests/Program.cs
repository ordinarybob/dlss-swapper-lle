using System.Collections.ObjectModel;
using System.Text.Json;
using DLSS_Swapper.Data;

// UI-free contract tests use small record/manager doubles. Windows build validates
// the same selectors against the actual observable collections and DLLRecord.
var manifest = new Manifest();
var manager = new DLLManager();
var expected = new (GameAssetType Type, int Id, int Backup, string File, string Key)[]
{
    (GameAssetType.DLSS, 1, 9, "nvngx_dlss.dll", "dlss"),
    (GameAssetType.DLSS_G, 2, 10, "nvngx_dlssg.dll", "dlss_g"),
    (GameAssetType.DLSS_D, 3, 11, "nvngx_dlssd.dll", "dlss_d"),
    (GameAssetType.FSR_31_DX12, 4, 12, "amd_fidelityfx_dx12.dll", "fsr_31_dx12"),
    (GameAssetType.FSR_31_VK, 5, 13, "amd_fidelityfx_vk.dll", "fsr_31_vk"),
    (GameAssetType.XeSS, 6, 14, "libxess.dll", "xess"),
    (GameAssetType.XeSS_FG, 8, 16, "libxess_fg.dll", "xess_fg"),
    (GameAssetType.XeSS_DX11, 17, 18, "libxess_dx11.dll", "xess_dx11"),
    (GameAssetType.XeLL, 7, 15, "libxell.dll", "xell"),
};
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
Check(DllFamilyRegistry.All.Count == 9, "Family count");
foreach (var item in expected)
{
    var family = DllFamilyRegistry.Get(item.Type);
    Check((int)family.Type == item.Id && (int)family.BackupType == item.Backup, "Stored enum IDs");
    Check(family.FileName == item.File, "File name");
    Check(ReferenceEquals(DllFamilyRegistry.FindFile(item.File), family), "Import dispatch");
    var record = new DLLRecord { VersionNumber = (ulong)item.Id };
    family.ManifestRecords(manifest).Add(record);
    family.Records(manager).Add(record);
    Check(ReferenceEquals(family.Records(manager), family.Records(manager)), "Stable observable collection");
    Check(family.NameResourceKey == "General_Name_" + item.Type, "Resource key");
}
using (var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest)))
{
    foreach (var item in expected)
        Check(json.RootElement.GetProperty(item.Key)[0].GetProperty("VersionNumber").GetUInt64() == (ulong)item.Id,
            "Manifest selector matches unchanged serialized key");
    Check(json.RootElement.TryGetProperty("known_dlls", out _), "Preserve known_dlls schema");
}
Check(DllFamilyRegistry.Find(GameAssetType.Unknown) is null, "Unknown type");
Check(DllFamilyRegistry.FindFile("NVNGX_DLSS.DLL") is null, "Preserve exact import matching");

foreach (var type in expected.Select(x => x.Type))
{
    foreach (var debug in new[] { false, true })
    {
        var low = new DLLRecord { AssetType = type, IsDevFile = debug, VersionNumber = 1, DisplayVersionVersion = new(3, 1) };
        var high = new DLLRecord { AssetType = type, IsDevFile = debug, VersionNumber = 2, DisplayVersionVersion = new(3, 2) };
        var filtered = new DLLRecord { AssetType = type, IsDevFile = !debug, VersionNumber = 100, DisplayVersionVersion = new(9, 0) };
        Check(ReferenceEquals(DllRecordSelection.FindLatest([low, high, filtered], DllRecordSelectionPolicy.LibraryVersion, debug), high), "Latest + dev filter");
        Check(ReferenceEquals(DllRecordSelection.FindLatest([high, high with { }], DllRecordSelectionPolicy.LibraryVersion, debug), high), "First tie retained");
    }
}
foreach (var type in new[] { GameAssetType.FSR_31_DX12, GameAssetType.FSR_31_VK, GameAssetType.FSR_31_DX12_BACKUP, GameAssetType.FSR_31_VK_BACKUP })
{
    var displayNewer = new DLLRecord { AssetType = type, VersionNumber = 1, DisplayVersionVersion = new(3, 2) };
    var fileNewer = new DLLRecord { AssetType = type, VersionNumber = 2, DisplayVersionVersion = new(3, 1) };
    Check(ReferenceEquals(DllRecordSelection.FindLatest([fileNewer, displayNewer], DllRecordSelectionPolicy.LibraryVersion), displayNewer), "FSR display precedence");
    Check(ReferenceEquals(DllRecordSelection.FindLatest([displayNewer, fileNewer], DllRecordSelectionPolicy.BatchRecordOrder), fileNewer), "Batch delegates CompareTo");
}
Check(DllRecordSelection.FindLatest([], DllRecordSelectionPolicy.LibraryVersion) is null, "Empty selection");
StreamlineMetadataTests.Run();
Console.WriteLine("PASS: nine-family registry, serialized keys/IDs, stable collections, version policies, filters, ties and 11 Streamline history mappings.");

namespace DLSS_Swapper.Data
{
    internal record DLLRecord
    {
        public GameAssetType AssetType { get; init; }
        public bool IsDevFile { get; init; }
        public ulong VersionNumber { get; init; }
        public Version DisplayVersionVersion { get; init; } = new(0, 0);
        public int CompareTo(DLLRecord other) => other.VersionNumber.CompareTo(VersionNumber);
    }

    internal class DLLManager
    {
        public ObservableCollection<DLLRecord> DLSSRecords { get; } = [];
        public ObservableCollection<DLLRecord> DLSSGRecords { get; } = [];
        public ObservableCollection<DLLRecord> DLSSDRecords { get; } = [];
        public ObservableCollection<DLLRecord> FSR31DX12Records { get; } = [];
        public ObservableCollection<DLLRecord> FSR31VKRecords { get; } = [];
        public ObservableCollection<DLLRecord> XeSSRecords { get; } = [];
        public ObservableCollection<DLLRecord> XeLLRecords { get; } = [];
        public ObservableCollection<DLLRecord> XeSSFGRecords { get; } = [];
        public ObservableCollection<DLLRecord> XeSSDX11Records { get; } = [];
    }
}
