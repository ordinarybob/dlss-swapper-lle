using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

public enum BatchSwapStatus
{
    Swapped,
    PresetApplied,
    AlreadyCurrent,
    Skipped,
    Error,
}

public sealed class BatchSwapResult
{
    public string GameIdentity { get; init; } = string.Empty;
    public string GameTitle { get; init; } = string.Empty;
    public BatchSwapStatus Status { get; init; }
    public string ActionLabel { get; init; } = string.Empty;
    public string ReasonKey { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public bool PromptToRelaunchAsAdmin { get; init; }

    public string DisplayText => Status switch
    {
        BatchSwapStatus.Skipped =>
            $"{GameTitle} — {ActionLabel} — {ResourceHelper.GetString(ReasonKey)}",
        BatchSwapStatus.Error when string.IsNullOrWhiteSpace(ErrorMessage) =>
            $"{GameTitle} — {ActionLabel} — {ResourceHelper.GetString("GamesPage_Batch_Error_Generic")}",
        BatchSwapStatus.Error =>
            $"{GameTitle} — {ActionLabel} — {ErrorMessage}",
        _ => $"{GameTitle} — {ActionLabel}",
    };
}
