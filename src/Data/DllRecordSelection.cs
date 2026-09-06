using System.Collections.Generic;

namespace DLSS_Swapper.Data;

internal enum DllRecordSelectionPolicy
{
    BatchRecordOrder,
    LibraryVersion,
}

internal static class DllRecordSelection
{
    // Library download keeps the first version tie and separates production/dev
    // downloads; batch selection deliberately retains DLLRecord.CompareTo ties.
    internal static DLLRecord? FindLatest(IEnumerable<DLLRecord> records,
        DllRecordSelectionPolicy policy, bool? devFilesOnly = null)
    {
        DLLRecord? latest = null;
        foreach (var candidate in records)
        {
            if (devFilesOnly.HasValue && candidate.IsDevFile != devFilesOnly.Value)
            {
                continue;
            }

            if (latest is null || IsNewer(candidate, latest, policy))
            {
                latest = candidate;
            }
        }
        return latest;
    }

    static bool IsNewer(DLLRecord candidate, DLLRecord latest, DllRecordSelectionPolicy policy) =>
        policy == DllRecordSelectionPolicy.BatchRecordOrder
            ? candidate.CompareTo(latest) < 0
            : candidate.AssetType is GameAssetType.FSR_31_DX12 or GameAssetType.FSR_31_VK
                or GameAssetType.FSR_31_DX12_BACKUP or GameAssetType.FSR_31_VK_BACKUP
                ? candidate.DisplayVersionVersion > latest.DisplayVersionVersion
                : candidate.VersionNumber > latest.VersionNumber;
}
