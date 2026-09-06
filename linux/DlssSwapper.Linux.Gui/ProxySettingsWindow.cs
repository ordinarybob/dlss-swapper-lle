using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class ProxySettingsWindow : Window
{
    public ProxySettingsWindow(PersistentLibrary library)
    {
        Title = LanguageAppearance.Get("Linux_ProxySettingsWindow_169", "Proxy settings"); Width = 560; Height = 400; MinWidth = 400; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var server = new TextBox { Text = library.State.Proxy?.Server, PlaceholderText = "http://proxy.example:8080" };
        var username = new TextBox { Text = library.State.Proxy?.Username };
        var password = new TextBox { PasswordChar = '●', PlaceholderText = LanguageAppearance.Get("Linux_ProxySettingsWindow_168", "Leave blank to retain the saved password") };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap,
            Text = LanguageAppearance.Get("Linux_ProxySettingsWindow_167", "Changes take effect after restarting the app. Passwords are stored in the desktop keyring. Clear restores the system proxy settings.") };
        var layout = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        var fields = new StackPanel { Spacing = 8 };
        foreach (var pair in new[] { ("Server", server), ("Username", username), ("Password", password) })
        { fields.Children.Add(new TextBlock { [!TextBlock.TextProperty] = new DynamicResourceExtension("ProxySettings_" + pair.Item1) }); fields.Children.Add(pair.Item2); }
        fields.Children.Add(status); layout.Children.Add(new ScrollViewer { Content = fields });
        var actions = new WrapPanel();
        var save = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Save") }; var clear = new Button { Content = LanguageAppearance.Get("Linux_ProxySettingsWindow_166", "Clear proxy and password") }; var cancel = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Cancel") };
        foreach (var button in new[] { save, clear, cancel }) { button.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(button); }
        Grid.SetRow(actions, 1); layout.Children.Add(actions); Content = layout;
        var busy = false;
        Closing += (_, e) => e.Cancel = busy;
        cancel.Click += (_, _) => Close();
        save.Click += async (_, _) => await SaveAsync(false);
        clear.Click += async (_, _) => await SaveAsync(true);

        async Task SaveAsync(bool remove)
        {
            if (busy) return;
            busy = true; actions.IsEnabled = false;
            string? newId = null;
            var prior = library.State.Proxy;
            var committed = false;
            try
            {
                ProxySettings? next = null;
                if (!remove)
                {
                    next = new ProxySettings(server.Text?.Trim() ?? "", username.Text ?? "", prior?.CredentialId);
                    _ = next.Address();
                    if (!string.IsNullOrEmpty(password.Text))
                    {
                        if (next.Username.Length == 0) throw new IOException(LanguageAppearance.Get("Linux_ProxyUsernameRequired", "Enter the username for this password."));
                        newId = Guid.NewGuid().ToString("N");
                        await ProxyKeyring.StoreAsync(newId, password.Text, default);
                        next = next with { CredentialId = newId };
                    }
                    else if (next.Username.Length == 0) next = next with { CredentialId = null };
                }
                library.UpdateState(state => state.Proxy = next);
                committed = true;
                if (prior?.CredentialId is { } oldId && oldId != next?.CredentialId)
                    await ProxyKeyring.ClearAsync(oldId, default);
                busy = false; Close();
            }
            catch (Exception error)
            { AppLog.Write(ApplicationLogLevel.Error, error.Message);
                status.Text = error.Message;
                if (committed)
                {
                    try { library.UpdateState(state => state.Proxy = prior); committed = false; }
                    catch { status.Text += LanguageAppearance.Get("Linux_ProxySettingsWindow_165", " Settings rollback failed; the old keyring entry remains."); }
                }
                if (!committed && newId is not null)
                {
                    try { await ProxyKeyring.ClearAsync(newId, default); }
                    catch { status.Text += LanguageAppearance.Get("Linux_ProxySettingsWindow_164", " The newly stored keyring entry could not be removed."); }
                }
            }
            finally { busy = false; actions.IsEnabled = true; }
        }
    }
}
