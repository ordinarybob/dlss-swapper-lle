using System.IO.Compression;
using DLSS_Swapper.Helpers;

internal static class DllArchiveExportTests
{
    public static void Run()
    {
        var root = Directory.CreateTempSubdirectory("dll-export-tests-").FullName;
        try
        {
            var source = Path.Combine(root, "sample.dll");
            var output = Path.Combine(root, "export.zip");
            File.WriteAllText(source, "DLL fixture");
            File.WriteAllText(output, "previous export");
            var files = new[] { (source, "DLSS/version/sample.dll") };
            void Check(bool value)
            {
                if (!value) throw new Exception("Export contract failed.");
            }
            void MustFail(Action action)
            {
                try { action(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
                {
                    Check(File.ReadAllText(output) == "previous export");
                    Check(Directory.GetFiles(root, "*.tmp").Length == 0);
                    return;
                }
                throw new Exception("Expected export failure.");
            }
            MustFail(() => DllArchiveExport.Write(output,
                [(source, "first.dll"), (Path.Combine(root, "missing.dll"), "second.dll")]));
            MustFail(() => DllArchiveExport.Write(output, files, cancellationToken: new CancellationToken(true)));
            using (var cancel = new CancellationTokenSource())
            {
                MustFail(() => DllArchiveExport.Write(output, files,
                    new InlineProgress(() => cancel.Cancel()), cancel.Token));
            }
            if (OperatingSystem.IsWindows())
            {
                using var locked = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read);
                MustFail(() => DllArchiveExport.Write(output, files));
            }
            DllArchiveExport.Write(output, files);
            using (var archive = ZipFile.OpenRead(output))
            {
                Check(archive.Entries.Count == 1);
                Check(archive.Entries[0].FullName == files[0].Item2);
                using var reader = new StreamReader(archive.Entries[0].Open());
                Check(reader.ReadToEnd() == "DLL fixture");
            }
            Check(Directory.GetFiles(root, "*.tmp").Length == 0);
            Console.WriteLine("Export: missing source, cancellation before/during export, locked target, preserved output and verified ZIP passed.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    sealed class InlineProgress(Action action) : IProgress<int>
    {
        public void Report(int value) => action();
    }
}
