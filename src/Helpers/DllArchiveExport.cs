using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

namespace DLSS_Swapper.Helpers;

internal static class DllArchiveExport
{
    public static void Write(string destination, IReadOnlyList<(string SourceFileName, string EntryName)> files,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        using var staged = new StagedOutputFile(destination);
        var hashes = new List<byte[]>();
        using (var archive = new ZipArchive(staged.Stream, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = File.OpenRead(file.SourceFileName);
                hashes.Add(SHA256.HashData(source));
                source.Position = 0;
                using var target = archive.CreateEntry(file.EntryName).Open();
                source.CopyTo(target);
                progress?.Report(hashes.Count);
            }
        }
        staged.Stream.Position = 0;
        using (var archive = new ZipArchive(staged.Stream, ZipArchiveMode.Read, true))
        {
            if (archive.Entries.Count != files.Count) throw new InvalidDataException("Incomplete export archive.");
            for (var i = 0; i < files.Count; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var entry = archive.Entries[i].Open();
                if (!SHA256.HashData(entry).AsSpan().SequenceEqual(hashes[i]))
                    throw new InvalidDataException("Exported content does not match its source.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        staged.Commit();
    }
}
