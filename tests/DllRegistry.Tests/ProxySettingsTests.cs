using DLSS_Swapper;
using Windows.Security.Credentials;

internal static class ProxySettingsTests
{
    public static void Run()
    {
        void Check(bool value)
        {
            if (!value) throw new Exception("Proxy settings contract failed.");
        }
        var settings = Settings.ProxySettings = new ProxySettings();
        Check(settings.SaveIfRequired("http://proxy.example:8080", "user", "secret"));
        var saved = PasswordVault.Stored;
        foreach (var invalid in new[] { "", "proxy", "ftp://proxy.example", "http://user:pass@proxy.example", "http://proxy.example/path", "http://proxy.example?q=1", "http://proxy.example/#fragment" })
            Check(!settings.SaveIfRequired(invalid, null, null));
        Check(!settings.SaveIfRequired("https://proxy.example", null, "secret"));
        Check(ReferenceEquals(saved, PasswordVault.Stored));
        PasswordVault.Fail = true;
        Check(!settings.SaveIfRequired("https://other.example", null, null));
        Check(!settings.SaveIfRequired(null, null, null));
        Check(settings.Server == "http://proxy.example:8080" && settings.Username == "user" && settings.Password == "secret");
        PasswordVault.Fail = false;
        Settings.ProxySettings = new ProxySettings();
        Settings.ProxySettings.LoadIfNeeded();
        Check(Settings.ProxySettings.Server == settings.Server && Settings.ProxySettings.Password == "secret");
        Check(Settings.ProxySettings.SaveIfRequired(null, null, null));
        Check(Settings.ProxySettings.Server == "" && PasswordVault.Stored is null);
        Check(Settings.ProxySettings.SaveIfRequired(null, null, null));
        Console.WriteLine("Proxy settings: validation, rejected saves, credential-store failure preservation, load and removal passed.");
    }
}

// Test-only in-memory vault. No Windows credential APIs are loaded or called.
namespace Windows.Security.Credentials
{
    public sealed class PasswordCredential(string resource, string userName, string password)
    {
        public string Resource => resource;
        public string UserName => userName;
        public string Password => password;
        public void RetrievePassword() { }
    }
    public sealed class PasswordVault
    {
        internal static PasswordCredential? Stored;
        internal static bool Fail;
        public PasswordCredential Retrieve(string resource, string userName)
        {
            if (Fail) throw new IOException("Fixture vault failure.");
            return Stored ?? throw new System.Runtime.InteropServices.COMException("Not found", -2147023728);
        }
        public void Add(PasswordCredential credential)
        {
            if (Fail) throw new IOException("Fixture vault failure.");
            Stored = credential;
        }
        public void Remove(PasswordCredential credential)
        {
            if (Fail) throw new IOException("Fixture vault failure.");
            Stored = null;
        }
    }
}
