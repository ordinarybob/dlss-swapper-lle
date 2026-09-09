using System;
using System.IO;

namespace DLSS_Swapper.Helpers;

// The destination is not touched until a complete sibling file has been flushed.
internal sealed class StagedOutputFile : IDisposable
{
    readonly string _destination;
    readonly string _staging;
    public FileStream Stream { get; }

    public StagedOutputFile(string destination)
    {
        _destination = Path.GetFullPath(destination);
        _staging = Path.Combine(Path.GetDirectoryName(_destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        Stream = new FileStream(_staging, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }

    public void Commit(bool overwrite = true)
    {
        Stream.Flush(true);
        Stream.Dispose();
        File.Move(_staging, _destination, overwrite);
    }

    public void Dispose()
    {
        Stream.Dispose();
        if (File.Exists(_staging)) File.Delete(_staging);
    }
}
