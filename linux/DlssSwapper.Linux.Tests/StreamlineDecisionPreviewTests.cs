using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineDecisionPreviewTests
{
    public static void Run()
    {
        static StreamlineFileSnapshot File(string? version, string hash = "AA") =>
            new("test.dll", version ?? "Unknown", version is null ? null : Version.Parse(version), hash, StreamlineFileState.Available);
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        var original = File("2.9.0.0");
        var upgrade = StreamlineDecisionPreview.Compare(original, File("2.12.0.0", "BB"));
        Require(upgrade.Kind == StreamlineChangeKind.Upgrade && upgrade.CanApply && upgrade.Changes, "Versions must compare numerically.");
        Require(StreamlineDecisionPreview.Compare(original, File("2.8", "BB")).IsDowngrade, "Downgrade must be explicit.");
        Require(StreamlineDecisionPreview.Compare(original, File("2.9", "BB")).Kind == StreamlineChangeKind.SameVersionDifferentBytes,
            "Matching numeric versions with different hashes are not no-ops.");
        Require(!StreamlineDecisionPreview.Compare(original, File(null)).Changes, "Identical bytes must be a no-op even when versions are unavailable.");
        Require(StreamlineDecisionPreview.Compare(original, File(null, "BB")).UnknownOrdering, "Unknown metadata must not imply upgrade.");
        foreach (var state in new[] { StreamlineFileState.NotSelected, StreamlineFileState.Missing, StreamlineFileState.Unreadable })
        {
            var unavailable = StreamlineDecisionPreview.Compare(original, new("missing", "Unavailable", null, null, state));
            Require(!unavailable.CanApply && !unavailable.Changes, "Unavailable source must not be actionable.");
        }

        var directory = Path.Combine(Path.GetTempPath(), "streamline-preview-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var game = Path.Combine(directory, "game");
            var package = Path.Combine(directory, "package");
            Directory.CreateDirectory(game);
            Directory.CreateDirectory(package);
            var target = Path.Combine(game, "sl.common.dll");
            var source = Path.Combine(package, "sl.common.dll");
            // Bytes are intentional non-PE fixtures: comparison must report unknown versions, not invent an SDK version.
            System.IO.File.WriteAllText(target, "installed");
            System.IO.File.WriteAllText(source, "replacement");
            var before = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order().ToArray();
            var preview = StreamlineDecisionPreview.Create(game, [target], package);
            Require(preview.Components.Single().InstalledVersion == "Unknown", "Missing version metadata must remain unknown.");
            Require(preview.CanUpdate && !preview.CanRestore, "Readiness should reflect the package and absent original backup.");
            Require(preview.IsEquivalentTo(StreamlineDecisionPreview.Create(game, [target], package), false), "Unchanged snapshot must compare equal.");
            Require(before.SequenceEqual(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order()), "Preview must not create files.");
            Require(System.IO.File.ReadAllText(target) == "installed" && System.IO.File.ReadAllText(source) == "replacement", "Preview must not mutate sources.");
            System.IO.File.WriteAllText(source, "changed package");
            Require(!preview.IsEquivalentTo(StreamlineDecisionPreview.Create(game, [target], package), false), "Changed source must invalidate confirmation.");
            System.IO.File.WriteAllText(target + StreamlineComponentSet.BackupSuffix, "original");
            var restore = StreamlineDecisionPreview.Create(game, [target], package);
            Require(restore.CanRestore, "Available original backup should be visible for restore.");
            System.IO.File.WriteAllText(target + StreamlineComponentSet.BackupSuffix, "changed original");
            Require(!restore.IsEquivalentTo(StreamlineDecisionPreview.Create(game, [target], package), true), "Changed backup must invalidate restore confirmation.");
            System.IO.File.WriteAllText(target, "changed installed");
            Require(!preview.IsEquivalentTo(StreamlineDecisionPreview.Create(game, [target], package), false), "Changed installed DLL must invalidate confirmation.");
            var missing = StreamlineDecisionPreview.Create(game, [target], Path.Combine(directory, "missing-package"));
            Require(!missing.CanUpdate && missing.Components.Single().Package.State == StreamlineFileState.Missing, "Missing package contents must disable update.");
            var noPackage = StreamlineDecisionPreview.Create(game, [target], null);
            Require(!noPackage.CanUpdate && noPackage.Components.Single().Package.State == StreamlineFileState.NotSelected, "No selected package is distinct from missing contents.");
            var upperTarget = Path.Combine(game, "SL.REFLEX.DLL");
            System.IO.File.WriteAllText(upperTarget, "installed uppercase filename");
            System.IO.File.WriteAllText(Path.Combine(package, "sl.reflex.dll"), "replacement lowercase filename");
            Require(StreamlineDecisionPreview.Create(game, [upperTarget], package).CanUpdate,
                "Package lookup must use canonical names when an installed component has uppercase letters.");

            var guardedGame = Path.Combine(directory, "guarded-game");
            var guardedPackage = Path.Combine(directory, "guarded-package");
            Directory.CreateDirectory(guardedGame);
            Directory.CreateDirectory(guardedPackage);
            var guardedTarget = Path.Combine(guardedGame, "sl.common.dll");
            var guardedSource = Path.Combine(guardedPackage, "sl.common.dll");
            StreamlineSafetyTests.WriteDll(guardedTarget, "old");
            StreamlineSafetyTests.WriteDll(guardedSource, "new");
            var confirmed = StreamlineDecisionPreview.Create(guardedGame, [guardedTarget], guardedPackage);
            var sourceSnapshots = new Dictionary<string, StreamlineFileSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["sl.common.dll"] = StreamlineDecisionPreview.ReadFile(guardedSource),
            };
            var cachedPreview = StreamlineDecisionPreview.Create(guardedGame, [guardedTarget], guardedPackage, sourceSnapshots);
            Require(confirmed.IsEquivalentTo(cachedPreview, false), "Cached package snapshot changed preview semantics.");
            StreamlineSafetyTests.WriteDll(guardedTarget, "outside edit");
            Require(!StreamlineComponentSet.UpdateExisting(guardedPackage, [guardedTarget], expectedPreview: confirmed).Success &&
                StreamlineSafetyTests.ReadLabel(guardedTarget) == "outside edit", "Engine must reject stale installed bytes before staging.");
            StreamlineSafetyTests.WriteDll(guardedTarget, "old");
            StreamlineSafetyTests.WriteDll(guardedSource, "changed package");
            Require(!StreamlineComponentSet.UpdateExisting(guardedPackage, [guardedTarget], expectedPreview: cachedPreview).Success,
                "Cached package snapshot allowed changed source bytes.");
            Require(!StreamlineComponentSet.UpdateExisting(guardedPackage, [guardedTarget], expectedPreview: confirmed).Success &&
                !System.IO.File.Exists(guardedTarget + StreamlineComponentSet.BackupSuffix), "Engine must reject stale package bytes before creating backups.");
            StreamlineSafetyTests.WriteDll(guardedSource, "new");
            Require(StreamlineComponentSet.UpdateExisting(guardedPackage, [guardedTarget], expectedPreview: confirmed).Success,
                "An unchanged confirmed update must succeed.");
            var confirmedRestore = StreamlineDecisionPreview.Create(guardedGame, [guardedTarget], guardedPackage);
            StreamlineSafetyTests.WriteDll(guardedTarget + StreamlineComponentSet.BackupSuffix, "changed backup");
            Require(!StreamlineComponentSet.RestoreOriginals([guardedTarget], expectedPreview: confirmedRestore).Success &&
                StreamlineSafetyTests.ReadLabel(guardedTarget) == "new", "Engine must reject stale restore backup.");
            System.IO.File.Delete(guardedTarget + StreamlineComponentSet.BackupSuffix);
            Require(!StreamlineComponentSet.RestoreOriginals([guardedTarget], expectedPreview: confirmedRestore).Success,
                "A missing confirmed restore target must not silently become a successful empty operation.");
            StreamlineSafetyTests.WriteDll(guardedTarget + StreamlineComponentSet.BackupSuffix, "old");
            Require(StreamlineComponentSet.RestoreOriginals([guardedTarget], expectedPreview: confirmedRestore).Success &&
                StreamlineSafetyTests.ReadLabel(guardedTarget) == "old", "An unchanged confirmed restore must succeed.");
            var confirmedNoOp = StreamlineDecisionPreview.Create(guardedGame, [guardedTarget], guardedPackage);
            StreamlineSafetyTests.WriteDll(guardedTarget, "new");
            Require(!StreamlineComponentSet.UpdateExisting(guardedPackage, [guardedTarget], expectedPreview: confirmedNoOp).Success,
                "Even an actual no-op must reject a changed confirmed installed fingerprint.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
