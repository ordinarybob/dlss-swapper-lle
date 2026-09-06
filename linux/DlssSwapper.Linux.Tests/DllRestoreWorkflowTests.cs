using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class DllRestoreWorkflowTests
{
    public static void Run()
    {
        var root = Directory.CreateTempSubdirectory("lle-restore-preview-");
        try
        {
            var target = Path.Combine(root.FullName, "libxess.dll");
            if (FileLocation.ContainingFolder(target) != root.FullName) throw new Exception("Missing-file folder fallback failed.");
            try { FileLocation.ContainingFolder(Path.Combine(root.FullName, "missing", "file.dll")); throw new Exception("Missing parent was accepted."); }
            catch (DirectoryNotFoundException) { }
            File.WriteAllText(target, "current"); File.WriteAllText(target + ".dlsss", "original");
            var streamline = Path.Combine(root.FullName, "sl.common.dll.dlsss"); File.WriteAllText(streamline, "keep");
            var game = new SelectedGame("Fixture", root.FullName, null);
            var report = OperationReport.Describe([
                new(game, "DLSS", "one", true, "Changed."),
                new(game, "DLSS", "two", true, "No change.", OperationOutcome.AlreadyCurrent),
                new(game, "DLSS", "three", false, "Incompatible.", OperationOutcome.Skipped),
                new(game, "DLSS", "four", false, "Stopped.", OperationOutcome.Cancelled),
                new(game, "DLSS", "five", false, "Access denied.")]);
            foreach (var outcome in Enum.GetValues<OperationOutcome>())
                if (!report.Contains($"{outcome}: 1")) throw new Exception("Report outcome counts were conflated.");
            if (!report.Contains("Access denied.") || !report.Contains(root.FullName) || OperationReport.Describe([]) != "No operation results.")
                throw new Exception("Report details or empty result handling failed.");
            var missing = DllRestoreWorkflow.Inspect(game with { RootPath = Path.Combine(root.FullName, "missing") });
            if (missing.Files.Count != 0 || missing.Warnings.Count == 0)
                throw new Exception("Unavailable folder was reported as a complete empty inspection.");
            var preview = DllRestoreWorkflow.Preview(game);
            if (preview.Count != 1) throw new Exception("Ordinary restore included Streamline or missed an original.");
            File.WriteAllText(target, "external");
            if (DllRestoreWorkflow.Apply(preview, default).Single().Success || File.ReadAllText(target) != "external")
                throw new Exception("Stale installed file was overwritten.");
            preview = DllRestoreWorkflow.Preview(game);
            File.WriteAllText(target + ".dlsss", "changed original");
            if (DllRestoreWorkflow.Apply(preview, default).Single().Success) throw new Exception("Stale original was restored.");
            preview = DllRestoreWorkflow.Preview(game);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            if (DllRestoreWorkflow.Apply(preview, cancelled.Token).Single().Success) throw new Exception("Cancelled restore changed files.");
            if (!DllRestoreWorkflow.Apply(preview, default).Single().Success || File.Exists(target + ".dlsss")
                || File.ReadAllText(target) != "changed original" || File.ReadAllText(streamline) != "keep")
                throw new Exception("Restore did not preserve ordinary/Streamline backup semantics.");
            File.WriteAllText(target + ".dlsss", "replacement"); File.Delete(target);
            preview = DllRestoreWorkflow.Preview(game);
            if (preview.Single().InstalledVersion != "Missing" || !DllRestoreWorkflow.Apply(preview, default).Single().Success)
                throw new Exception("Missing installed target could not be restored.");
            var language = new Translations("en-US");
            var text = (Dictionary<string, string>)language.Values;
            text["Linux_Streamline_Missing"] = "ABSENT";
            text["Linux_OperationRestored"] = "RESTORED";
            text["Linux_RestoreCancelled"] = "RESTORE CANCELLED";
            File.WriteAllText(target + ".dlsss", "localized original"); File.Delete(target);
            var localized = DllRestoreWorkflow.Inspect(game, language).Files;
            if (localized.Single().InstalledVersion != "ABSENT"
                || DllRestoreWorkflow.Apply(localized, cancelled.Token, language).Single().Message != "RESTORE CANCELLED"
                || DllRestoreWorkflow.Apply(localized, default, language).Single().Message != "RESTORED"
                || File.ReadAllText(target) != "localized original")
                throw new Exception("Localized restore changed file behavior or outcome.");
        }
        finally { root.Delete(true); }
    }
}
