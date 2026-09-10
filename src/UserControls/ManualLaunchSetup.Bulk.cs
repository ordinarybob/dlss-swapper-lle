using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Data.ManuallyAdded;

namespace DLSS_Swapper.UserControls;

internal static partial class ManualLaunchSetup
{
    internal sealed record BulkLaunchResult(int Saved, int Kept, IReadOnlyList<string> Skipped, bool Cancelled);

    internal static async Task<BulkLaunchResult> SaveDefaultsAsync(
        IReadOnlyList<ManuallyAddedGame> games,
        Func<ManuallyAddedGame, Task<List<ManualLaunchManifest.Candidate>>> findCandidates,
        Action<int, string>? progress = null, CancellationToken token = default)
    {
        var saved = 0;
        var kept = 0;
        var skipped = new List<string>();
        for (var index = 0; index < games.Count; index++)
        {
            if (token.IsCancellationRequested) return new(saved, kept, skipped, true);
            var game = games[index];
            progress?.Invoke(index, game.Title);
            var old = (game.LaunchExecutable, game.LaunchArguments, game.LaunchWorkingDirectory);
            try
            {
                if (!string.IsNullOrWhiteSpace(game.LaunchExecutable)
                    && !ManualLaunchManifest.IsExcluded(game.LaunchExecutable))
                {
                    ManualLaunchManifest.Validate(game.LaunchExecutable,
                        game.LaunchArguments ?? "", game.LaunchWorkingDirectory ?? "");
                    kept++;
                    continue;
                }
                var candidates = await findCandidates(game);
                if (token.IsCancellationRequested) return new(saved, kept, skipped, true);
                if (candidates.Count == 0)
                {
                    skipped.Add($"{game.Title}: no suggested executable.");
                    continue;
                }
                var manifest = ManualLaunchManifest.Validate(candidates[0].Path,
                    game.LaunchArguments ?? "", game.LaunchWorkingDirectory ?? "");
                game.LaunchExecutable = manifest.Executable;
                game.LaunchArguments = manifest.Arguments;
                game.LaunchWorkingDirectory = manifest.WorkingDirectory;
                if (!await game.SaveToDatabaseAsync(bypassBatch: true))
                    throw new IOException("Launch details could not be saved.");
                saved++;
            }
            catch (Exception ex)
            {
                (game.LaunchExecutable, game.LaunchArguments, game.LaunchWorkingDirectory) = old;
                skipped.Add($"{game.Title}: {ex.Message}");
            }
        }
        return new(saved, kept, skipped, false);
    }
}
