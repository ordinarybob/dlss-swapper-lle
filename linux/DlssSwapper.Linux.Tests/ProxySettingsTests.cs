using System.Net;
using System.Text.Json;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class ProxySettingsTests
{
    public static Task RunAsync()
    {
        var settings = new ProxySettings("http://localhost:8181", "fixture-user", "fixture-id");
        var proxy = settings.CreateProxy("fixture-password");
        if (proxy.GetProxy(new Uri("https://ngx.download.nvidia.com/")) != settings.Address()
            || proxy.Credentials?.GetCredential(settings.Address(), "Basic")?.Password != "fixture-password")
            throw new Exception("Proxy routing or credentials were lost.");
        foreach (var invalid in new[] { "", "localhost:8181", "file:///tmp/proxy", "http://user:pass@localhost:8181", "http://localhost/path", "http://localhost/?query" })
        {
            try { _ = new ProxySettings(invalid).Address(); throw new Exception("Invalid proxy accepted."); }
            catch (IOException) { }
        }
        try { settings.CreateProxy(null); throw new Exception("Missing saved password was silently ignored."); }
        catch (IOException) { }
        var root = Directory.CreateTempSubdirectory("lle-proxy-fixture-");
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(root.FullName));
            library.UpdateState(state => state.Proxy = settings);
            var reopened = new PersistentLibrary(new LibraryStateStore(root.FullName));
            if (reopened.State.Proxy != settings || JsonSerializer.Serialize(reopened.State).Contains("fixture-password"))
                throw new Exception("Proxy persistence lost settings or exposed a password.");
            reopened.UpdateState(state => state.Proxy = null);
            if (new PersistentLibrary(new LibraryStateStore(root.FullName)).State.Proxy is not null)
                throw new Exception("Cleared proxy persisted after reopening.");
        }
        finally { root.Delete(true); }
        return Task.CompletedTask;
    }
}
