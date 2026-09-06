using System.IO.Compression;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class DllImportSourcesTests
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("lle-import-sources-");
        try
        {
            var zip = Path.Combine(root.FullName, "inputs.ZIP");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                foreach (var name in new[] { "nested/LIBXESS.DLL", "../nvngx_dlss.dll", "broken.dll" })
                { using var writer = new StreamWriter(archive.CreateEntry(name).Open()); writer.Write(name); }
            }
            var names = new List<string>();
            var results = await DllImportSources.ReadAsync([zip], async (name, stream, token) =>
            {
                names.Add(name);
                if (name == "broken.dll") throw new IOException("fixture rejection");
                using var reader = new StreamReader(stream); await reader.ReadToEndAsync(token); return "accepted fixture";
            }, default);
            if (results.Count != 3 || results.Count(result => result.Success) != 2 || !names.Contains("LIBXESS.DLL")
                || !names.Contains("nvngx_dlss.dll") || Directory.GetFiles(root.FullName).Length != 1)
                throw new Exception("Case-insensitive ZIP streaming or per-entry isolation failed.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var summary = DllImportSources.Describe(results);
            if (!summary.Contains("2 succeeded") || !summary.Contains("1 failed") || !summary.Contains("fixture rejection"))
                throw new Exception("Import summary lost counts or per-file failure details.");
            var stopped = await DllImportSources.ReadAsync([zip], (_, _, _) => throw new Exception("must not import"), cancelled.Token);
            if (stopped.Single().Success) throw new Exception("Cancelled input reported imported.");
            var language = new Translations("en-US");
            ((Dictionary<string, string>)language.Values)["Linux_ImportCancelled"] = "IMPORT CANCELLED";
            var localizedStop = await DllImportSources.ReadAsync([zip], (_, _, _) => throw new Exception("must not import"), cancelled.Token, language);
            if (localizedStop.Single().Success || localizedStop.Single().Message != "IMPORT CANCELLED" || localizedStop.Single().Source != zip)
                throw new Exception("Localized import cancellation changed source or outcome.");
            var missing = await DllImportSources.ReadAsync([Path.Combine(root.FullName, "missing.dll")], (_, _, _) => throw new Exception("must not import"), default);
            if (missing.Single().Success) throw new Exception("Missing file reported imported.");
            var path = typeof(DllImportSourcesTests).Assembly.Location;
            if (DllImportMetadata.Read(path).IsDebug != System.Diagnostics.FileVersionInfo.GetVersionInfo(path).IsDebug)
                throw new Exception("DLL debug metadata was discarded.");
        }
        finally { root.Delete(true); }
    }
}
