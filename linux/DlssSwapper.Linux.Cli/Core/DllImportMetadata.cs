using DlssSwapper.Shared;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record DllImportMetadata(string Version, ulong VersionNumber, bool IsDebug,
    string? InternalName, string? Description)
{
    // Metadata is descriptive only; this does not establish signature trust or validate the PE image.
    public static DllImportMetadata Read(string path)
    {
        var info = PeVersionInfo.Read(path);
        var number = ((ulong)(ushort)info.FileMajorPart << 48) | ((ulong)(ushort)info.FileMinorPart << 32)
            | ((ulong)(ushort)info.FileBuildPart << 16) | (ushort)info.FilePrivatePart;
        return new(info.FileVersion ?? "Unknown", number, info.IsDebug, info.InternalName, info.FileDescription);
    }
}
