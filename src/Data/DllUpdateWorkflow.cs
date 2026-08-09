using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

internal readonly record struct DllUpdateSelection(GameAssetType Type, DLLRecord Record);

internal static class DllUpdateWorkflow
{
    sealed record PlannedUpdate(
        int Sequence,
        Game Game,
        DLLRecord Record,
        string ActionLabel);

    internal static List<DLLRecord> GetEligibleRecords(GameAssetType type)
    {
        var records = type switch
        {
            GameAssetType.DLSS => DLLManager.Instance.DLSSRecords.ToList(),
            GameAssetType.DLSS_G => DLLManager.Instance.DLSSGRecords.ToList(),
            GameAssetType.DLSS_D => DLLManager.Instance.DLSSDRecords.ToList(),
            GameAssetType.FSR_31_DX12 => DLLManager.Instance.FSR31DX12Records.ToList(),
            GameAssetType.FSR_31_VK => DLLManager.Instance.FSR31VKRecords.ToList(),
            GameAssetType.XeSS => DLLManager.Instance.XeSSRecords.ToList(),
            GameAssetType.XeLL => DLLManager.Instance.XeLLRecords.ToList(),
            GameAssetType.XeSS_FG => DLLManager.Instance.XeSSFGRecords.ToList(),
            GameAssetType.XeSS_DX11 => DLLManager.Instance.XeSSDX11Records.ToList(),
            _ => [],
        };

        if (Settings.Instance.OnlyShowDownloadedDlls)
        {
            records.RemoveAll(record => record.LocalRecord?.IsDownloaded != true);
        }

        if (Settings.Instance.AllowDebugDlls == false)
        {
            records.RemoveAll(record => record.IsDevFile);
        }

        if (Settings.Instance.AllowUntrusted == false)
        {
            records.RemoveAll(record => record.IsSignatureValid == false);
        }

        records.RemoveAll(record => record.LocalRecord is null);
        return records;
    }

    internal static DLLRecord? FindLatestRecord(IEnumerable<DLLRecord> records)
    {
        DLLRecord? latest = null;
        foreach (var candidate in records)
        {
            if (latest is null || candidate.CompareTo(latest) < 0)
            {
                latest = candidate;
            }
        }

        return latest;
    }

    internal static List<DllUpdateSelection> GetLatestSelections(IEnumerable<Game> games)
    {
        var detectedTypes = games
            .SelectMany(game => game.GameAssets)
            .Select(asset => asset.AssetType)
            .Distinct()
            .OrderBy(type => (int)type);

        var selections = new List<DllUpdateSelection>();
        foreach (var type in detectedTypes)
        {
            var latest = FindLatestRecord(GetEligibleRecords(type));
            if (latest is not null)
            {
                selections.Add(new DllUpdateSelection(type, latest));
            }
        }

        return selections;
    }

    internal static Task<List<BatchSwapResult>> UpdateDetectedToLatestAsync(
        IReadOnlyList<Game> games)
    {
        return ApplyAsync(games, GetLatestSelections(games));
    }

    internal static async Task<List<BatchSwapResult>> ApplyAsync(
        IReadOnlyList<Game> games,
        IReadOnlyList<DllUpdateSelection> selections)
    {
        var completedResults = new List<(int Sequence, BatchSwapResult Result)>();
        var plannedUpdates = new List<PlannedUpdate>();
        var sequence = 0;

        foreach (var game in games)
        {
            var detectedTypes = game.GameAssets
                .Select(asset => asset.AssetType)
                .Distinct()
                .ToHashSet();

            foreach (var selection in selections)
            {
                if (detectedTypes.Contains(selection.Type) == false)
                {
                    continue;
                }

                var actionSequence = sequence++;
                var actionLabel = ResourceHelper.GetFormattedResourceTemplate(
                    "GamesPage_Batch_DllLabelTemplate",
                    DLLManager.Instance.GetAssetTypeName(selection.Type),
                    selection.Record.DisplayName);

                var compatibility = DLLManager.GetBatchCompatibility(game, selection.Record);
                if (compatibility.Compatible == false)
                {
                    if (compatibility.ReasonKey != "GamesPage_Batch_Skipped_NoAsset")
                    {
                        completedResults.Add((
                            actionSequence,
                            CreateResult(
                                game,
                                BatchSwapStatus.Skipped,
                                actionLabel,
                                reasonKey: compatibility.ReasonKey)));
                    }

                    continue;
                }

                var targetAssets = game.GameAssets
                    .Where(asset => asset.AssetType == selection.Type)
                    .ToList();
                var versionsMatch = targetAssets.Count > 0
                    && targetAssets.All(asset => string.Equals(
                        asset.Version,
                        selection.Record.Version,
                        StringComparison.OrdinalIgnoreCase));
                var hasKnownDifferentBuild = targetAssets.Any(asset =>
                    string.IsNullOrWhiteSpace(asset.Hash) == false
                    && string.Equals(
                        asset.Hash,
                        selection.Record.MD5Hash,
                        StringComparison.OrdinalIgnoreCase) == false);
                if (versionsMatch && hasKnownDifferentBuild == false)
                {
                    completedResults.Add((
                        actionSequence,
                        CreateResult(
                            game,
                            BatchSwapStatus.AlreadyCurrent,
                            actionLabel)));
                    continue;
                }

                plannedUpdates.Add(new PlannedUpdate(
                    actionSequence,
                    game,
                    selection.Record,
                    actionLabel));
            }
        }

        IEqualityComparer<DLLRecord> recordComparer =
            ReferenceEqualityComparer.Instance;
        var downloadErrors = new Dictionary<DLLRecord, string>(recordComparer);
        foreach (var record in plannedUpdates
            .Select(update => update.Record)
            .Distinct(recordComparer))
        {
            var availability = await EnsureAvailableAsync(record);
            if (availability.Success == false)
            {
                downloadErrors[record] = availability.Message;
            }
        }

        var readyUpdates = new List<PlannedUpdate>();
        foreach (var update in plannedUpdates)
        {
            if (downloadErrors.TryGetValue(update.Record, out var downloadError))
            {
                completedResults.Add((
                    update.Sequence,
                    CreateResult(
                        update.Game,
                        BatchSwapStatus.Error,
                        update.ActionLabel,
                        errorMessage: downloadError)));
            }
            else
            {
                readyUpdates.Add(update);
            }
        }

        var appliedResults = new ConcurrentBag<(int Sequence, BatchSwapResult Result)>();
        var updatesByGame = readyUpdates.GroupBy(update => update.Game).ToList();
        await Parallel.ForEachAsync(
            updatesByGame,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Settings.Instance.BatchSwapConcurrency),
            },
            async (gameUpdates, _) =>
            {
                foreach (var update in gameUpdates)
                {
                    try
                    {
                        var outcome = await update.Game.UpdateDllAsync(update.Record);
                        appliedResults.Add((
                            update.Sequence,
                            CreateResult(
                                update.Game,
                                outcome.Success
                                    ? BatchSwapStatus.Swapped
                                    : BatchSwapStatus.Error,
                                update.ActionLabel,
                                errorMessage: outcome.Message,
                                promptToRelaunchAsAdmin: outcome.PromptToRelaunchAsAdmin)));
                    }
                    catch (Exception err)
                    {
                        Logger.Error(
                            err,
                            $"Could not update {update.Record.AssetType} for \"{update.Game.Title}\".");
                        appliedResults.Add((
                            update.Sequence,
                            CreateResult(
                                update.Game,
                                BatchSwapStatus.Error,
                                update.ActionLabel,
                                errorMessage: err.Message)));
                    }
                }
            });

        completedResults.AddRange(appliedResults);
        return completedResults
            .OrderBy(item => item.Sequence)
            .Select(item => item.Result)
            .ToList();
    }

    static async Task<(bool Success, string Message)> EnsureAvailableAsync(DLLRecord record)
    {
        var localRecord = record.LocalRecord;
        if (localRecord is null)
        {
            return (false, "The local DLL record is unavailable.");
        }

        if (localRecord.IsDownloaded && File.Exists(localRecord.ExpectedPath))
        {
            return (true, string.Empty);
        }

        try
        {
            var download = await record.DownloadAsync();
            if (download.Success == false)
            {
                var message = string.IsNullOrWhiteSpace(download.Message)
                    ? "The DLL download did not complete."
                    : download.Message;
                return (false, message);
            }

            if (File.Exists(localRecord.ExpectedPath) == false)
            {
                return (false, "The downloaded DLL was not found.");
            }

            return (true, string.Empty);
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Could not download {record.AssetType} {record.DisplayName}.");
            return (false, err.Message);
        }
    }

    static BatchSwapResult CreateResult(
        Game game,
        BatchSwapStatus status,
        string actionLabel,
        string reasonKey = "",
        string errorMessage = "",
        bool promptToRelaunchAsAdmin = false)
    {
        return new BatchSwapResult
        {
            GameIdentity = game.ID,
            GameTitle = game.Title,
            Status = status,
            ActionLabel = actionLabel,
            ReasonKey = reasonKey,
            ErrorMessage = errorMessage,
            PromptToRelaunchAsAdmin = promptToRelaunchAsAdmin,
        };
    }
}
