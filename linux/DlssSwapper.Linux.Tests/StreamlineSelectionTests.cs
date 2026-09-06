using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineSelectionTests
{
    internal static void Run()
    {
        var fixture = Directory.CreateTempSubdirectory("lle-streamline-selection-");
        try
        {
            var game = Directory.CreateDirectory(Path.Combine(fixture.FullName, "game")).FullName;
            var package = Directory.CreateDirectory(Path.Combine(fixture.FullName, "package")).FullName;
            var targets = StreamlineComponentSet.FileNames.Take(3).Select(name => Path.Combine(game, name)).ToArray();
            foreach (var target in targets)
            {
                StreamlineSafetyTests.WriteDll(target, "original");
                StreamlineSafetyTests.WriteDll(Path.Combine(package, Path.GetFileName(target)), "updated");
            }
            var preview = StreamlineDecisionPreview.Create(game, targets, package);
            Check(!preview.SelectTargets([]).CanUpdate, "An empty selection cannot apply.");
            var selected = preview.SelectTargets([targets[0], targets[0]]);
            Check(selected.Components.Count == 1 && selected.CanUpdate, "Selection deduplicates full paths.");
            var rejected = false;
            try { preview.SelectTargets([Path.Combine(game, "not-in-preview.dll")]); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Unknown selections must not silently apply another set.");

            StreamlineSafetyTests.WriteDll(targets[2], "changed outside selection");
            var result = StreamlineComponentSet.UpdateExisting(package,
                selected.Components.Select(item => item.TargetPath).ToArray(), expectedPreview: selected);
            Check(result.Success && result.ComponentCount == 1 && result.ChangedPaths!.Single() == targets[0], result.Message);
            Check(StreamlineSafetyTests.ReadLabel(targets[0]) == "updated", "Selected file was not applied.");
            Check(StreamlineSafetyTests.ReadLabel(targets[1]) == "original"
                && StreamlineSafetyTests.ReadLabel(targets[2]) == "changed outside selection", "Unselected files changed.");
            Check(!File.Exists(targets[1] + StreamlineComponentSet.BackupSuffix)
                && !File.Exists(targets[2] + StreamlineComponentSet.BackupSuffix), "Unselected backups were created.");

            var all = StreamlineDecisionPreview.Create(game, targets, package);
            var allResult = StreamlineComponentSet.UpdateExisting(package, targets, expectedPreview: all);
            Check(allResult.Success && allResult.ComponentCount == 2, "Apply all must skip the already identical selected file.");
            Check(targets.All(path => StreamlineSafetyTests.ReadLabel(path) == "updated"), "Apply all missed a component.");
            Check(!StreamlineDecisionPreview.Create(game, targets, package).CanUpdate, "No-op apply all should be disabled.");

            var subfolder = Directory.CreateDirectory(Path.Combine(game, "other")).FullName;
            var duplicateName = Path.Combine(subfolder, Path.GetFileName(targets[0]));
            StreamlineSafetyTests.WriteDll(duplicateName, "nested original");
            var duplicatePreview = StreamlineDecisionPreview.Create(game, targets.Append(duplicateName), package).SelectTargets([duplicateName]);
            Check(duplicatePreview.Components.Single().TargetPath == duplicateName, "Selection must identify paths, not basenames.");
            var nestedResult = StreamlineComponentSet.UpdateExisting(package, [duplicateName], expectedPreview: duplicatePreview);
            Check(nestedResult.Success && nestedResult.ComponentCount == 1, nestedResult.Message);
            var restorePreview = StreamlineDecisionPreview.Create(game, targets.Append(duplicateName), package)
                .SelectTargets([duplicateName]);
            var restored = StreamlineComponentSet.RestoreOriginals([duplicateName], expectedPreview: restorePreview);
            Check(restored.Success && restored.ComponentCount == 1, restored.Message);
            Check(StreamlineSafetyTests.ReadLabel(duplicateName) == "nested original", "Selected restore missed its target.");
            Check(targets.All(path => StreamlineSafetyTests.ReadLabel(path) == "updated"), "Selected restore changed unselected files.");
            Check(StreamlineSafetyTests.ReadLabel(targets[0] + StreamlineComponentSet.BackupSuffix) == "original", "Other component's original was overwritten.");
        }
        finally { fixture.Delete(recursive: true); }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
