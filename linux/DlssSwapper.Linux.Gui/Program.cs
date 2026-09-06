using Avalonia;

namespace DlssSwapper.Linux.Gui;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = new SingleInstance();
        if (!instance.IsPrimary)
        {
            try { instance.ActivateExisting(); }
            catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); Console.Error.WriteLine("Could not activate the running application: " + error.Message); Environment.ExitCode = 1; }
            return;
        }
        instance.Listen(() => Avalonia.Threading.Dispatcher.UIThread.Post(App.ActivateMainWindow));
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
