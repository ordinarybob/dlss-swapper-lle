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
            var sourceHash = Convert.ToHexString(MD5.HashData(input));
            if (expectedMd5 is not null && !sourceHash.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The source DLL changed before replacement.");
            input.Position = 0;
            input.CopyTo(output);
            output.Flush(true);
            output.Position = 0;
            if (!Convert.ToHexString(MD5.HashData(output)).Equals(sourceHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The staged DLL failed verification; the installed file was preserved.");
        }
        staged.Commit(overwrite);
    }
}
