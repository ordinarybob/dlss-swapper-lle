using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;

namespace DLSS_Swapper.Data;

internal static class BatchPresetUpdateWorkflow
{
    internal static async Task<List<BatchSwapResult>> ApplyAsync(
        IReadOnlyList<Game> games,
        IReadOnlyList<BatchPresetSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(selections);

        var results = new List<BatchSwapResult>();
        foreach (var game in games)
        {
            await Task.Yield();
            bool profileChecked = false, hasProfile = false;
            foreach (var selection in selections)
            {
                if (HasMatchingDll(game, selection.Kind) == false)
                {
                    continue;
                }

                var actionLabel = ResourceHelper.GetFormattedResourceTemplate(
                    "GamesPage_Batch_DllLabelTemplate",
                    GetPresetName(selection.Kind),
                    selection.DisplayName);

                try
                {
                    if (!profileChecked)
                    {
                        hasProfile = NVAPIHelper.Instance.FindGameProfile(game) is not null;
                        profileChecked = true;
                    }
                    if (!hasProfile)
                    {
                        results.Add(CreateResult(
                            game,
                            BatchSwapStatus.Skipped,
                            actionLabel,
                            "GamesPage_Batch_Skipped_NoNvidiaProfile"));
                        continue;
                    }

                    var current = GetCurrentPreset(game, selection.Kind);
                    if (current.Success == false)
                    {
                        results.Add(CreateResult(
                            game,
                            BatchSwapStatus.Error,
                            actionLabel,
                            errorMessage: ResourceHelper.GetString(
                                "GamesPage_Batch_Error_NvidiaPreset")));
                        continue;
                    }

                    if (current.Result == selection.Value)
                    {
                        results.Add(CreateResult(
                            game,
                            BatchSwapStatus.AlreadyCurrent,
                            actionLabel));
                        continue;
                    }

                    var update = SetPreset(game, selection.Kind, selection.Value);
                    results.Add(CreateResult(
                        game,
                        update.Success
                            ? BatchSwapStatus.PresetApplied
                            : BatchSwapStatus.Error,
                        actionLabel,
                        errorMessage: update.Success
                            ? string.Empty
                            : ResourceHelper.GetString(
                                "GamesPage_Batch_Error_NvidiaPreset")));
                }
                catch (Exception err)
                {
                    Logger.Error(
                        err,
                        $"Could not update {selection.Kind} preset for \"{game.Title}\".");
                    results.Add(CreateResult(
                        game,
                        BatchSwapStatus.Error,
                        actionLabel,
                        errorMessage: err.Message));
                }
            }
        }

        return results;
    }

    static bool HasMatchingDll(Game game, BatchPresetKind kind) => kind switch
    {
        BatchPresetKind.Dlss => game.CurrentDLSS is not null,
        BatchPresetKind.DlssD => game.CurrentDLSS_D is not null,
        BatchPresetKind.DlssG => game.CurrentDLSS_G is not null,
        _ => false,
    };

    static string GetPresetName(BatchPresetKind kind) => kind switch
    {
        BatchPresetKind.Dlss => ResourceHelper.GetString("General_Name_DLSS_Preset"),
        BatchPresetKind.DlssD => ResourceHelper.GetString("General_Name_DLSSD_Preset"),
        BatchPresetKind.DlssG => ResourceHelper.GetString("General_Name_DLSSG_Preset"),
        _ => string.Empty,
    };

    static NVAPIResult<uint> GetCurrentPreset(Game game, BatchPresetKind kind) =>
        kind switch
        {
            BatchPresetKind.Dlss => NVAPIHelper.Instance.GetGameDLSSPreset(game),
            BatchPresetKind.DlssD => NVAPIHelper.Instance.GetGameDLSSDPreset(game),
            BatchPresetKind.DlssG => NVAPIHelper.Instance.GetGameDLSSGPreset(game),
            _ => new NVAPIResult<uint>(false, 0),
        };

    static NVAPIResult<bool> SetPreset(
        Game game,
        BatchPresetKind kind,
        uint value) => kind switch
    {
        BatchPresetKind.Dlss => NVAPIHelper.Instance.SetGameDLSSPreset(game, value),
        BatchPresetKind.DlssD => NVAPIHelper.Instance.SetGameDLSSDPreset(game, value),
        BatchPresetKind.DlssG => NVAPIHelper.Instance.SetGameDLSSGPreset(game, value),
        _ => new NVAPIResult<bool>(false, false),
    };

    static BatchSwapResult CreateResult(
        Game game,
        BatchSwapStatus status,
        string actionLabel,
        string reasonKey = "",
        string errorMessage = "") => new()
    {
        GameIdentity = game.ID,
        GameTitle = game.Title,
        Status = status,
        ActionLabel = actionLabel,
        ReasonKey = reasonKey,
        ErrorMessage = errorMessage,
        PromptToRelaunchAsAdmin = NVAPIHelper.Instance.PermissionIssue,
    };
}
