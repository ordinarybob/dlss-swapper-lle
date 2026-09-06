using System.Diagnostics;
using System.Net;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record ProxySettings(string Server, string Username = "", string? CredentialId = null)
{
    public Uri Address()
    {
        if (!Uri.TryCreate(Server, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "socks5")
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new IOException("Enter a proxy address such as http://proxy.example:8080, without credentials or a path.");
        return uri;
    }

    public IWebProxy CreateProxy(string? password)
    {
        if (CredentialId is not null && password is null) throw new IOException("The saved proxy password is unavailable.");
        return new WebProxy(Address()) { Credentials = Username.Length == 0 ? null : new NetworkCredential(Username, password ?? "") };
    }
}

/// <summary>Stores only this application's proxy secrets in the desktop Secret Service.</summary>
public static class ProxyKeyring
{
    public static Task<string> ReadAsync(string id, CancellationToken token) => RunAsync("lookup", id, null, token);
    public static Task<string> StoreAsync(string id, string password, CancellationToken token) => RunAsync("store", id, password, token);
    public static Task<string> ClearAsync(string id, CancellationToken token) => RunAsync("clear", id, null, token);

    private static async Task<string> RunAsync(string operation, string id, string? password, CancellationToken token)
    {
        if (!OperatingSystem.IsLinux()) throw new IOException("Saving proxy passwords requires a Linux Secret Service keyring and secret-tool.");
        using var process = new Process { StartInfo = new ProcessStartInfo("secret-tool")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add(operation);
        if (operation == "store") process.StartInfo.ArgumentList.Add("--label=DLSS Swapper LLE proxy");
        foreach (var argument in new[] { "application", "dlss-swapper-lle", "proxy-id", id }) process.StartInfo.ArgumentList.Add(argument);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                if (password is not null) await process.StandardInput.WriteAsync(password.AsMemory(), timeout.Token).ConfigureAwait(false);
                process.StandardInput.Close();
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(true);
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(output, error).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                throw new IOException("The proxy keyring request timed out. Check that the desktop keyring is unlocked.");
            }
            await error.ConfigureAwait(false);
            var result = await output.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException("The proxy keyring request failed. Check that secret-tool and an unlocked Secret Service keyring are available.");
            return result;
        }
        catch (System.ComponentModel.Win32Exception)
        { throw new IOException("Proxy passwords require secret-tool and a Secret Service keyring. No password was saved in the settings file."); }
    }
}
