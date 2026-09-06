using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data.Streamline;

internal static class StreamlineHistory
{
    // An audit failure must never turn a completed file operation into a reported
    // swap failure, nor cause a retry that might apply the same operation again.
    internal static async Task<string?> TryRecordAsync(Game game,
        StreamlineComponentOperationResult result, bool restoring)
    {
        if (!result.Success || result.ChangedPaths is not { Count: > 0 }) return null;

        try
        {
            var eventTime = DateTime.Now;
            var records = new List<GameHistory>();
            foreach (var path in result.ChangedPaths)
            {
                var component = StreamlineAssetMetadata.FindFile(path)
                    ?? throw new InvalidDataException($"Unknown Streamline component in history: {path}");
                records.Add(new GameHistory
                {
                    GameId = game.ID,
                    EventType = restoring ? GameHistoryEventType.DLLReset : GameHistoryEventType.DLLSwapped,
                    EventTime = eventTime,
                    AssetType = component.Type,
                    AssetPath = path,
                    AssetVersion = FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty,
                });
            }

            using (await Database.Instance.Mutex.LockAsync().ConfigureAwait(false))
            {
                await Database.Instance.Connection.InsertAllAsync(records, true).ConfigureAwait(false);
            }
            return null;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, $"Could not record Streamline history for game {game.ID} after a completed operation.");
            return "The files were changed successfully, but their history could not be saved. See the log for details.";
        }
    }
}
