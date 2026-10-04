using System;
using System.IO;
using System.Security.Cryptography;

namespace DLSS_Swapper.Extensions;

internal static class FileStreamExtensions
{
    internal static string GetMD5Hash(this Stream fileStream)
    {
        fileStream.Position = 0;

        return Convert.ToHexString(MD5.HashData(fileStream));
    }

}
