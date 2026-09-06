using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class OperationReportSaveTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "report-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "report.txt");
            var text = "Game — 日本語\nUpdated: 1\n";
            await OperationReport.SaveLocalAsync(path, text);
            if (File.ReadAllText(path) != text) throw new Exception("New report text changed.");
            if (!File.ReadAllBytes(path).SequenceEqual(new System.Text.UTF8Encoding(false).GetBytes(text)))
                throw new Exception("Report encoding changed.");
            await OperationReport.SaveLocalAsync(path, "replacement");
            if (File.ReadAllText(path) != "replacement") throw new Exception("Report replacement failed.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try
            {
                await OperationReport.SaveLocalAsync(path, "cancelled", cancellation.Token);
                throw new Exception("Cancelled save succeeded.");
            }
            catch (OperationCanceledException) { }
            if (File.ReadAllText(path) != "replacement") throw new Exception("Cancellation damaged existing report.");
            if (OperatingSystem.IsWindows())
            {
                using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try
                    {
                        await OperationReport.SaveLocalAsync(path, "blocked replacement");
                        throw new Exception("Locked destination replacement succeeded.");
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                if (File.ReadAllText(path) != "replacement") throw new Exception("Failed replacement damaged existing report.");
            }
            var directory = Path.Combine(root, "directory.txt");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "sentinel"), "keep");
            try
            {
                await OperationReport.SaveLocalAsync(directory, "invalid target");
                throw new Exception("Directory replacement succeeded.");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (File.ReadAllText(Path.Combine(directory, "sentinel")) != "keep")
                throw new Exception("Failed save changed destination.");
            if (Directory.GetFiles(root).Length != 1) throw new Exception("Temporary report leaked.");
        }
        finally { Directory.Delete(root, true); }
    }
}
