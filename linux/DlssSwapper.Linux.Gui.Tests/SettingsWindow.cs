using Avalonia.Controls;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

// Visible test host for the embedded Settings page; production navigation does not create this window.
internal sealed class SettingsWindow : Window
{
    private readonly SettingsPage _page;
    public bool WasReset => _page.WasReset;
    public bool LibrarySelectionChanged => _page.LibrarySelectionChanged;
    public SettingsWindow(PersistentLibrary library, Func<string>? diagnostics = null)
    {
        Width = 820; Height = 760; MinWidth = 620; MinHeight = 520;
        _page = new SettingsPage(library, diagnostics);
        Content = _page;
        NameScope.SetNameScope(this, NameScope.GetNameScope(_page));
        _page.Finished += saved => Close(saved);
    }
    internal Task CopyBuildIdentityAsync(Func<string, Task> copy) => _page.CopyBuildIdentityAsync(copy);
}
