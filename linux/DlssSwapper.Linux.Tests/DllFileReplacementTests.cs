using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class DllFileReplacementTests
{
    internal static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "lle-replacement-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.dll"); var target = Path.Combine(root, "target.dll");
            File.WriteAllBytes(source, [4,5,6]); File.WriteAllBytes(target, [1,2,3]);
            var hash = DllScanner.ComputeMd5(source);
            foreach (var failure in new[] { "hash", "validate", "cancel" })
            {
                using var cancellation = new CancellationTokenSource();
                var validations = 0;
                try
                {
                    await DllFileReplacement.CopyAsync(source, target, failure == "hash" ? new string('a', 32) : hash, true, () =>
                    {
                        if (++validations != 2) return;
                        if (failure == "validate") throw new IOException("Injected publication validation failure");
                        if (failure == "cancel") { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); }
                    }, cancellation.Token);
                    throw new Exception("Replacement unexpectedly succeeded");
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
                if (!File.ReadAllBytes(target).SequenceEqual(new byte[] {1,2,3}) || Directory.EnumerateFiles(root, "*.incoming-*").Any())
                    throw new Exception("Failed replacement changed destination or left staging files");
            }
            await DllFileReplacement.CopyAsync(source, target, hash, true, () => {}, CancellationToken.None);
            if (!File.ReadAllBytes(target).SequenceEqual(new byte[] {4,5,6})) throw new Exception("Verified replacement did not publish");
            try
            {
                await DllFileReplacement.CopyAsync(source, target, hash, false, () => {}, CancellationToken.None);
                throw new Exception("Existing backup was overwritten");
            }
            catch (IOException) { }
        }
        finally { Directory.Delete(root, true); }
    }
}
