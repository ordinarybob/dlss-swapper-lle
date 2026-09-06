using System.Diagnostics;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record DllSignatureResult(bool IsValid, string Message);

public interface IDllSignatureVerifier
{
    Task<DllSignatureResult> VerifyAsync(string path, CancellationToken token);
}

/// <summary>Authenticode verification using explicitly packaged tools and code-signing roots.</summary>
public sealed class DllSignatureVerifier(string? executable = null, string? signerRoots = null,
    string? timestampRoots = null, Translations? translations = null) : IDllSignatureVerifier
{
    private string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
    public async Task<DllSignatureResult> VerifyAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var tool = executable ?? Path.Combine(AppContext.BaseDirectory, "tools", "osslsigncode");
        var signer = signerRoots ?? Path.Combine(AppContext.BaseDirectory, "trust", "code-signing.pem");
        var timestamp = timestampRoots ?? Path.Combine(AppContext.BaseDirectory, "trust", "timestamp-signing.pem");
        if (!File.Exists(tool) || !File.Exists(signer) || !File.Exists(timestamp))
            return new(false, T("Linux_VerifierMissing", "Signature verification is unavailable: the verifier or code-signing certificates are missing."));
        if (!Path.IsPathFullyQualified(tool) || !Path.IsPathFullyQualified(signer) || !Path.IsPathFullyQualified(timestamp))
            return new(false, T("Linux_VerifierPaths", "Signature verifier paths must be absolute."));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var process = new Process { StartInfo = new ProcessStartInfo
        {
            FileName = tool, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        } };
        // Only the packaged child uses these libraries; never alter the app or
        // host environment, or an explicitly supplied verifier's configuration.
        if (executable is null)
            process.StartInfo.Environment["LD_LIBRARY_PATH"] = Path.Combine(AppContext.BaseDirectory, "tools", "lib");
        foreach (var argument in new[] { "verify", "-CAfile", signer, "-TSA-CAfile", timestamp, "-in", Path.GetFullPath(path) })
            process.StartInfo.ArgumentList.Add(argument);
        try
        {
            if (!process.Start()) return new(false, T("Linux_VerifierStart", "Could not start the signature verifier."));
            var stdout = DrainAsync(process.StandardOutput);
            var stderr = DrainAsync(process.StandardError);
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return new(false, T("Linux_VerifierTimeout", "Signature verification timed out. The DLL was not verified."));
            }
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            return process.ExitCode == 0
                ? new(true, T("Linux_VerifierValid", "Embedded signature verified."))
                : new(false, T("Linux_VerifierInvalid", "The DLL signature or its certificate chain could not be verified."));
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException)
        { return new(false, T("Linux_VerifierFailed", "Signature verification failed: {0}", error.Message)); }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        // Drain both pipes without retaining unbounded external output in memory.
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer).ConfigureAwait(false) != 0) { }
    }
}
