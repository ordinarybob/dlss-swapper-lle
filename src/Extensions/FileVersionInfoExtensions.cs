using System;
using System.Diagnostics;
using System.IO;

namespace DLSS_Swapper.Extensions;

internal static class FileVersionInfoExtensions
{
    const int HashReadBufferSize = 1024 * 1024;

    internal static string GetMD5Hash(this FileVersionInfo fileVersionInfo)
    {
        try
        {
            using (var fileStream = new FileStream(
                fileVersionInfo.FileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                HashReadBufferSize,
                FileOptions.SequentialScan))
            {
                return fileStream.GetMD5Hash();
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"{fileVersionInfo.FileName}");
            DebuggerHelper.BreakIfAttached();
        }

        return string.Empty;
    }

    internal static string GetFormattedFileVersion(this FileVersionInfo fileVersionInfo)
    {
        return $"{fileVersionInfo.FileMajorPart}.{fileVersionInfo.FileMinorPart}.{fileVersionInfo.FileBuildPart}.{fileVersionInfo.FilePrivatePart}";
    }

    internal static ulong GetFileVersionNumber(this FileVersionInfo fileVersionInfo)
    {
        return ((ulong)fileVersionInfo.FileMajorPart << 48) +
                ((ulong)fileVersionInfo.FileMinorPart << 32) +
                ((ulong)fileVersionInfo.FileBuildPart << 16) +
                ((ulong)fileVersionInfo.FilePrivatePart);
    }
}
