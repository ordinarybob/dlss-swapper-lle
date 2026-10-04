using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Data.ManuallyAdded;

namespace DLSS_Swapper.UserControls;

internal static partial class ManualLaunchSetup
{
    internal sealed record LaunchScanEntry(ManuallyAddedGame Game,
        List<ManualLaunchManifest.Candidate> Candidates, string? Error = null);

    internal static async Task<IReadOnlyList<LaunchScanEntry>> ScanAllDefaultsAsync(
        IReadOnlyList<ManuallyAddedGame> games,
        Func<ManuallyAddedGame, Task<List<ManualLaunchManifest.Candidate>>> findCandidates,
        IProgress<int>? progress = null, CancellationToken token = default)
    {
        var results = new LaunchScanEntry[games.Count];
        var completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, games.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token },
            async (index, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                var game = games[index];
                try
                {
                    results[index] = new(game, await findCandidates(game));
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception ex) { results[index] = new(game, [], ex.Message); }
                cancellation.ThrowIfCancellationRequested();
                progress?.Report(Interlocked.Increment(ref completed));
            });
        return results;
    }

    internal sealed class LaunchDraft
    {
        internal ManuallyAddedGame Game { get; }
        internal List<ManualLaunchManifest.Candidate> Candidates { get; }
        internal string Executable { get; set; }
        internal string Arguments { get; set; }
        internal string WorkingDirectory { get; set; }
        internal string Error { get; set; }

        internal LaunchDraft(LaunchScanEntry scan)
        {
            Game = scan.Game;
            Candidates = new(scan.Candidates);
            Executable = !string.IsNullOrWhiteSpace(Game.LaunchExecutable) && !ManualLaunchManifest.IsExcluded(Game.LaunchExecutable)
                ? Game.LaunchExecutable : Candidates.FirstOrDefault()?.Path ?? "";
            Arguments = Game.LaunchArguments ?? "";
            WorkingDirectory = Game.LaunchWorkingDirectory ?? "";
            Error = string.IsNullOrEmpty(Executable)
                ? scan.Error ?? "No suggested executable. Use Browse." : "";
        }
    }

    internal static async Task<int> SaveDraftsAsync(IReadOnlyList<LaunchDraft> drafts, bool skipUnselected = false)
    {
        var failures = 0;
        foreach (var draft in drafts)
        {
            if (skipUnselected && string.IsNullOrWhiteSpace(draft.Executable))
            {
                draft.Error = "";
                continue;
            }
            var game = draft.Game;
            var old = (game.LaunchExecutable, game.LaunchArguments, game.LaunchWorkingDirectory);
            try
            {
                var manifest = ManualLaunchManifest.Validate(draft.Executable, draft.Arguments, draft.WorkingDirectory);
                if (old != (manifest.Executable, manifest.Arguments, manifest.WorkingDirectory))
                {
                    game.LaunchExecutable = manifest.Executable;
                    game.LaunchArguments = manifest.Arguments;
                    game.LaunchWorkingDirectory = manifest.WorkingDirectory;
                    if (!await game.SaveToDatabaseAsync(bypassBatch: true))
                        throw new IOException("Launch details could not be saved. Try again.");
                }
                draft.Error = "";
            }
            catch (Exception ex)
            {
                (game.LaunchExecutable, game.LaunchArguments, game.LaunchWorkingDirectory) = old;
                draft.Error = ex.Message;
                failures++;
            }
        }
        return failures;
    }
}
