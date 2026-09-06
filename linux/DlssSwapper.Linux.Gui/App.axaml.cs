using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace DlssSwapper.Linux.Gui;

public sealed partial class App : Application
{
    internal static void ActivateMainWindow()
    {
        if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }) return;
        if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
            window.WindowState = window is MainWindow main ? main.StateBeforeMinimizing : Avalonia.Controls.WindowState.Normal;
        if (!window.IsVisible) window.Show();
        while (window.OwnedWindows.LastOrDefault(child => child.IsVisible) is { } child) window = child;
        window.Activate();
    }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LanguageAppearance.Apply("en-US");
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            AppLog.Write(ApplicationLogLevel.Error, e.Exception.ToString());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
