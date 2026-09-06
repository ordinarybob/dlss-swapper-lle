using DLSS_Swapper.Data.Streamline;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineWorkflowTests
{
    public static async Task RunAsync()
    {
        var fixture = Directory.CreateTempSubdirectory("lle-streamline-workflow-");
        try
        {
            var game = Directory.CreateDirectory(Path.Combine(fixture.FullName, "game")).FullName;
            var package = Directory.CreateDirectory(Path.Combine(fixture.FullName, "production")).FullName;
            foreach (var name in StreamlineComponentSet.FileNames)
                StreamlineSafetyTests.WriteDll(Path.Combine(package, name), "new");
            var target = Path.Combine(game, "sl.dlss.dll");
            StreamlineSafetyTests.WriteDll(target, "old");
            File.WriteAllText(Path.Combine(game, "unrelated.txt"), "keep");
            var originalFiles = Directory.GetFiles(fixture.FullName, "*", SearchOption.AllDirectories);
            var timestamp = File.GetLastWriteTimeUtc(target);

            foreach (var action in new[] { "update", "restore", "recover" })
            {
                var arguments = new List<string> { "streamline", action, "--path", game, "--dry-run" };
                if (action == "update") arguments.AddRange(["--package", Path.Combine(fixture.FullName, "absent-package")]);
                var result = await DlssSwapper.Linux.Cli.Program.RunAsync(arguments.ToArray(), false,
                    () => throw new Exception("Dry-run reached download cache."),
                    () => throw new Exception("Dry-run reached persistent library state."));
                Check(result == 0, "Dry-run failed.");
            }
            Check(Directory.GetFiles(fixture.FullName, "*", SearchOption.AllDirectories).SequenceEqual(originalFiles),
                "Dry-run created files.");
            Check(File.GetLastWriteTimeUtc(target) == timestamp && StreamlineSafetyTests.ReadLabel(target) == "old",
                "Dry-run changed the target.");
            Check(StreamlineWorkflow.Execute(game, "inspect").ComponentCount == 1, "Inspection missed component.");
            var update = StreamlineWorkflow.Execute(game, "update", package);
            Check(update.Success && update.ComponentCount == 1, update.Message);
            Check(StreamlineSafetyTests.ReadLabel(target) == "new", "Update did not use shared engine.");
            Check(!File.Exists(Path.Combine(game, "sl.common.dll")), "Update added an absent component.");
            Check(File.ReadAllText(Path.Combine(game, "unrelated.txt")) == "keep", "Unrelated file changed.");
            var restore = StreamlineWorkflow.Execute(game, "restore");
            Check(restore.Success && StreamlineSafetyTests.ReadLabel(target) == "old", restore.Message);
            Check(StreamlineWorkflow.Execute(game, "recover").Success, "Empty recovery should be harmless.");
            var language = new Translations("en-US");
            ((Dictionary<string, string>)language.Values)["Linux_StreamlineNoRecovery"] = "NOTHING TO RECOVER";
            var recovery = StreamlineWorkflow.Execute(game, "recover", translations: language);
            Check(recovery.Success && recovery.ComponentCount == 0 && recovery.Message == "NOTHING TO RECOVER",
                "Localized recovery changed result or missed the empty-operation message.");
            Check(StreamlineWorkflow.Execute(game, "recover").Message == "No interrupted Streamline operation was found.",
                "Default CLI recovery message changed.");
            foreach (var args in new[]
            {
                new[] { "streamline", "update", "--path", game, "--package", package },
                new[] { "streamline", "restore", "--all", "--yes" },
                new[] { "streamline", "inspect", "--path", game, "--yes" },
                new[] { "scan", "--path", game, "--package", package },
            })
            {
                var rejected = false;
                try { DlssSwapper.Linux.Cli.CliParser.Parse(args); }
                catch (DlssSwapper.Linux.Cli.UsageException) { rejected = true; }
                Check(rejected, "Invalid Streamline command bypassed explicit scope/confirmation.");
            }
        }
        finally { fixture.Delete(recursive: true); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
