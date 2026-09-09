using System;
using System.IO;
using System.Security.Cryptography;

namespace DLSS_Swapper.Helpers;

internal static class StagedFile
{
    public static void CopyVerified(string source, string destination, string? expectedMd5 = null, bool overwrite = true)
    {
        using var staged = new StagedOutputFile(destination);
        using (var input = File.OpenRead(source))
        {
            var output = staged.Stream;
            // Hash during the copy rather than reading the entire source twice.
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    output.Write(buffer, 0, read);
                }
            }
            finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
            var sourceHash = Convert.ToHexString(hash.GetHashAndReset());
            if (expectedMd5 is not null && !sourceHash.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The source DLL changed before replacement.");
            // Commit performs the durable flush after staged bytes are verified.
            output.Flush();
            output.Position = 0;
            if (!Convert.ToHexString(MD5.HashData(output)).Equals(sourceHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The staged DLL failed verification; the installed file was preserved.");
        }
        staged.Commit(overwrite);
    }
}
