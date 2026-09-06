using System.IO.Compression;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class TranslationDocumentTests
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("lle-translation-document-");
        try
        {
            var source = new Translations("en-US");
            var document = new TranslationDocument(source);
            const string key = "General_Cancel";
            const string translated = "Line one, \"quoted\"\n第二行";
            document.Rows[key] = document.Rows[key] with { NewTranslation = translated };
            foreach (var extension in new[] { "json", "csv" })
            {
                var path = Path.Combine(root.FullName, "draft." + extension);
                await document.SaveAsync(path);
                var loaded = new TranslationDocument(source);
                loaded.Load(path);
                if (loaded.Edits().Count != 1 || loaded.Edits()[key] != translated
                    || loaded.Rows[key].SourceTranslation != source.Get(key, ""))
                    throw new Exception("Translation round-trip lost multiline text, source or edits.");
            }
            var bad = Path.Combine(root.FullName, "bad.csv");
            await File.WriteAllTextAsync(bad, "key,NewTranslations\nOnly-one-field");
            try { document.Load(bad); throw new Exception("Invalid CSV accepted."); }
            catch (IOException) { }
            if (document.Edits()[key] != translated) throw new Exception("Failed load erased unsaved edits.");
            var zipPath = Path.Combine(root.FullName, "translations.zip");
            await document.PublishAsync(zipPath);
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                if (zip.Entries.Count != 1 || zip.Entries[0].FullName != "Resources.resw") throw new Exception("Wrong export format.");
                var xmlPath = Path.Combine(root.FullName, "Resources.resw");
                zip.Entries[0].ExtractToFile(xmlPath);
                if (Translations.Read(xmlPath)[key] != translated) throw new Exception("Export lost translation.");
            }
            var before = await File.ReadAllBytesAsync(zipPath);
            document.Rows[key] = document.Rows[key] with { NewTranslation = "\0" };
            try { await document.PublishAsync(zipPath); throw new Exception("Invalid XML character accepted."); }
            catch (ArgumentException) { }
            var failedExport = await File.ReadAllBytesAsync(zipPath);
            if (!before.SequenceEqual(failedExport)) throw new Exception("Failed serialization replaced previous export.");
            try { await document.SaveAsync(zipPath); throw new Exception("Unsupported draft format accepted."); }
            catch (IOException) { }
            document.Rows[key] = document.Rows[key] with { NewTranslation = "" };
            try { await document.PublishAsync(zipPath); throw new Exception("Empty export accepted."); }
            catch (IOException) { }
            var after = await File.ReadAllBytesAsync(zipPath);
            if (!before.SequenceEqual(after)) throw new Exception("Failed export replaced previous file.");
            if (Directory.GetFiles(root.FullName, "*.tmp").Length != 0) throw new Exception("Temporary exports remain.");
        }
        finally { root.Delete(true); }
    }
}
