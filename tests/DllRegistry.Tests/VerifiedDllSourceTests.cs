using System.Security.Cryptography;
using DLSS_Swapper.Helpers;

internal static class VerifiedDllSourceTests
{
    internal static void Run()
    {
        var root = Directory.CreateTempSubdirectory("lle-batch-source-");
        try
        {
            var source = Path.Combine(root.FullName, "source.dll");
            var bytes = RandomNumberGenerator.GetBytes(200_003);
            File.WriteAllBytes(source, bytes);
            var md5 = Convert.ToHexString(MD5.HashData(bytes));
            var checks = 0;
            var validated = new VerifiedDllSource(source, md5, true, _ => { checks++; return true; });
            using (validated)
            {
                for (var index = 0; index < 20; index++)
                {
                    Check(validated.Matches(source, md5, true), "Reusable validation missing.");
                    var target = Path.Combine(root.FullName, $"target{index}.dll");
                    StagedFile.CopyVerified(source, target, md5);
                    Check(File.ReadAllBytes(target).SequenceEqual(bytes), "Copied bytes differ.");
                }
                Check(checks == 1, "Signature was rechecked per destination.");
                Check(!validated.Matches(source, new string('0', 32), true), "Different hash reused validation.");
                Check(!validated.Matches(Path.Combine(root.FullName, "other.dll"), md5, true), "Different path reused validation.");
                if (OperatingSystem.IsWindows())
                {
                    MustFail(() => File.WriteAllBytes(source, [1]));
                    MustFail(() => File.Move(source, source + ".moved"));
                }
            }
            Check(!validated.Matches(source, md5, true), "Disposed validation was accepted.");
            MustFail(() => { using var invalid = new VerifiedDllSource(source, new string('0', 32), true, _ => true); });
            MustFail(() => { using var invalid = new VerifiedDllSource(source, md5, true, _ => false); });
            MustFail(() => { using var invalid = new VerifiedDllSource(source, md5, true, _ => throw new IOException("Signature failure")); });
            using (var untrusted = new VerifiedDllSource(source, md5, false, _ => throw new Exception("Unexpected verification")))
            {
                Check(untrusted.Matches(source, md5, false), "Allowed untrusted source rejected.");
                Check(!untrusted.Matches(source, md5, true), "Untrusted source bypassed stricter policy.");
            }
            // All failure and success paths must release source handles.
            File.WriteAllBytes(source, [1, 2, 3]);
            Check(!Directory.EnumerateFiles(root.FullName, "*.tmp").Any(), "Staging files leaked.");
            Console.WriteLine("Batch source: 20 verified replacements, one signature check; changed/untrusted/disposed sources rejected and handles released.");
        }
        finally { root.Delete(true); }
    }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void MustFail(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        throw new Exception("Unsafe operation unexpectedly succeeded.");
    }
}
