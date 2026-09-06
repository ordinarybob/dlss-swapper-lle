using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data.Streamline;

internal sealed record StreamlineBatchGame(Game Game, IReadOnlyList<string> Paths, string? Error = null);

internal static class StreamlineBatchUpdateWorkflow
{
    internal static async Task<IReadOnlyList<StreamlineBatchGame>> DiscoverAsync(IReadOnlyList<Game> games) =>
        await Task.Run(() => games.Select(game =>
        {
            try { return new StreamlineBatchGame(game, StreamlineComponentSet.FindInstalled(game.InstallPath)); }
            catch (Exception exception) { return new StreamlineBatchGame(game, [], exception.Message); }
        }).ToArray());

    internal static bool HasPartialSets(IEnumerable<StreamlineBatchGame> games, IReadOnlyList<string> names) =>
        games.Any(game => { var count = StreamlineBatchSelection.SelectPaths(game.Paths, names).Count;
            return count > 0 && count < game.Paths.Count; });

    internal static async Task<List<BatchSwapResult>> ApplyAsync(IReadOnlyList<StreamlineBatchGame> games,
        IReadOnlyList<string> names, bool partialApproved)
    {
        var results = new List<BatchSwapResult>();
        if (names.Count == 0) return results;
        StreamlinePackage? package = null;
        string? packageError = null;
        var eligible = games.Where(game => game.Error is null &&
            StreamlineBatchSelection.SelectPaths(game.Paths, names).Count > 0).ToArray();
        if (eligible.Length > 0)
        {
            try
            {
                package = Settings.Instance.OnlyShowDownloadedDlls
                    ? await Task.Run(StreamlineReleaseManager.FindNewestCached)
                    : await StreamlineReleaseManager.PrepareLatestAsync();
                if (package is null) packageError = "No cached Streamline SDK is available. Disable downloaded-only filtering to allow a download.";
            }
            catch (Exception exception) { packageError = exception.Message; }
        }
        var allowUntrusted = Settings.Instance.AllowUntrusted;
        foreach (var entry in games)
        {
            var label = "Streamline" + (package is null ? string.Empty : $" {package.Tag}");
            if (entry.Error is not null) { results.Add(Result(entry.Game, BatchSwapStatus.Error, label, entry.Error)); continue; }
            if (StreamlineBatchSelection.SelectPaths(entry.Paths, names).Count == 0)
            { results.Add(Result(entry.Game, BatchSwapStatus.Skipped, label, "No selected Streamline components installed.")); continue; }
            if (packageError is not null) { results.Add(Result(entry.Game, BatchSwapStatus.Error, label, packageError)); continue; }
            try
            {
                // One transaction per game: Streamline's engine serializes operations and rolls back the entire selected set.
                var outcome = await Task.Run(() =>
                {
                    var installed = StreamlineComponentSet.FindInstalled(entry.Game.InstallPath);
                    var targets = StreamlineBatchSelection.SelectPaths(installed, names);
                    if (targets.Count == 0) return new StreamlineComponentOperationResult(false, 0, "Selected components are no longer installed.");
                    if (!partialApproved && targets.Count < installed.Count)
                        return new(false, 0, "The component set changed and now requires a partial-set warning. Run the batch again.");
                    if (StreamlineComponentSet.HasPendingRecovery(entry.Game.InstallPath))
                        return new(false, 0, "Recover the interrupted Streamline operation from the game's Streamline window first.");
                    var preview = StreamlineDecisionPreview.Create(entry.Game.InstallPath, targets, package!.DirectoryPath);
                    if (!allowUntrusted && preview.Components.Any(item => !WinTrust.VerifyEmbeddedSignature(item.Package.Path)))
                        return new(false, 0, "A Streamline package signature could not be verified. No Streamline files were changed.");
                    return StreamlineComponentSet.UpdateExisting(package.DirectoryPath, targets, expectedPreview: preview);
                });
                var historyWarning = await StreamlineHistory.TryRecordAsync(entry.Game, outcome, false);
                results.Add(Result(entry.Game, !outcome.Success ? BatchSwapStatus.Error
                    : outcome.ComponentCount == 0 ? BatchSwapStatus.AlreadyCurrent : BatchSwapStatus.Swapped,
                    label, historyWarning is null ? outcome.Message : outcome.Message + " " + historyWarning));
            }
            catch (Exception exception) { results.Add(Result(entry.Game, BatchSwapStatus.Error, label, exception.Message)); }
        }
        return results;
    }

    static BatchSwapResult Result(Game game, BatchSwapStatus status, string label, string detail) => new()
    { GameIdentity = game.ID, GameTitle = game.Title, Status = status, ActionLabel = label,
      ErrorMessage = status == BatchSwapStatus.Error ? detail : string.Empty,
      DetailMessage = status == BatchSwapStatus.Error ? string.Empty : detail };
}
