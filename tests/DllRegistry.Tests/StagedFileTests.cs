using DLSS_Swapper.Helpers;
using System.Security.Cryptography;

internal static class StagedFileTests
{
    public static void Run()
    {
        var root = Directory.CreateTempSubdirectory("dll-staging-tests-").FullName;
        var source = Path.Combine(root, "source.dll");
        var target = Path.Combine(root, "installed.dll");
        var backup = Path.Combine(root, "original.dll");
        try
        {
            File.WriteAllText(source, "replacement");
            File.WriteAllText(target, "installed");
            File.WriteAllText(backup, "first original");
            void Check(bool condition)
            {
                if (!condition) throw new Exception("Staged file preservation failed.");
            }
            void MustFail(Action action)
            {
                try { action(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return; }
                throw new Exception("Expected publication failure.");
            }
            MustFail(() => StagedFile.CopyVerified(source, target, new string('0', 32)));
            Check(File.ReadAllText(target) == "installed");
            MustFail(() => StagedFile.CopyVerified(source, backup, overwrite: false));
            Check(File.ReadAllText(backup) == "first original");
            using (var staged = new StagedOutputFile(target))
                staged.Stream.Write("unfinished"u8);
            Check(File.ReadAllText(target) == "installed");
            if (OperatingSystem.IsWindows())
            {
                using var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
                MustFail(() => StagedFile.CopyVerified(source, target));
            }
            Check(File.ReadAllText(target) == "installed");
            var hash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(source)));
            StagedFile.CopyVerified(source, target, hash);
            Check(File.ReadAllText(target) == "replacement");
            var freshBackup = Path.Combine(root, "fresh.dll");
            StagedFile.CopyVerified(source, freshBackup, overwrite: false);
            Check(File.ReadAllText(freshBackup) == "replacement");
            Check(Directory.GetFiles(root, "*.tmp").Length == 0);
            CheckSerializationFailure(root);
            Console.WriteLine("Staged publication: hash failure, existing backup, abandoned write, locked destination, success and cleanup passed.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    static void CheckSerializationFailure(string root)
    {
        // Use the same staging primitive and serializers as translation exports.
        var destination = Path.Combine(root, "translation.json");
        File.WriteAllText(destination, "previous translation");
        try
        {
            using var staged = new StagedOutputFile(destination);
            System.Text.Json.JsonSerializer.Serialize(staged.Stream, new FailingTranslation());
            staged.Commit();
            throw new Exception("Expected JSON serialization failure.");
        }
        catch (InvalidOperationException error) when (error.Message == "fixture serialization failure") { }
        if (File.ReadAllText(destination) != "previous translation")
            throw new Exception("Failed JSON serialization replaced the previous output.");

        destination = Path.Combine(root, "translation.zip");
        File.WriteAllText(destination, "previous archive");
        try
        {
            using var staged = new StagedOutputFile(destination);
            using (var archive = new System.IO.Compression.ZipArchive(staged.Stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                using var entry = archive.CreateEntry("Resources.resw").Open();
                var document = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("root", "invalid\u0001translation"));
                document.Save(entry);
            }
            staged.Commit();
            throw new Exception("Expected XML serialization failure.");
        }
        catch (ArgumentException) { }
        if (File.ReadAllText(destination) != "previous archive")
            throw new Exception("Failed ZIP serialization replaced the previous output.");
        if (Directory.GetFiles(root, "*.tmp").Length != 0)
            throw new Exception("Serialization failure left staging files behind.");
        Console.WriteLine("Translation staging: JSON/XML serialization failures preserve previous outputs and clean up staging (not file-picker UI proof).");
    }

    sealed class FailingTranslation
    {
        public string First => new string('x', 32768);
        public string Second => throw new InvalidOperationException("fixture serialization failure");
    }
}
