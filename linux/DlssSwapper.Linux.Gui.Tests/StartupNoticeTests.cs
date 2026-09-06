using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class StartupNoticeTests
{
    public static void Run(string root)
    {
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "startup-notice")));
        library.UpdateState(state => state.HasCompletedInitialDeepScan = true);
        MainWindow Create() => new(new MainWindowServices(() => new(DllCatalog.Empty(), library, null),
            _ => Task.FromResult(new DlssSwapper.Linux.Cli.Platform.SteamDiscoveryResult([], [])),
            (_, _) => new([], []), () => null));
        var window = Create(); window.Show();
        try
        {
            Until(() => window.OwnedWindows.OfType<StorageProfileDialog>().Any());
            window.OwnedWindows.OfType<StorageProfileDialog>().Single().Close();
            Until(() => !((MainWindowViewModel)window.DataContext!).IsBusy);
            if (library.State.HasSelectedStorageProfile) throw new Exception("Dismissing first-run notice saved a choice.");
        }
        finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
        window = Create(); window.Show();
        try
        {
            Until(() => window.OwnedWindows.OfType<StorageProfileDialog>().Any());
            var notice = window.OwnedWindows.OfType<StorageProfileDialog>().Single();
            notice.FindControl<CheckBox>("HddModeCheckBox")!.IsChecked = true;
            notice.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Apply"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => library.State.HasSelectedStorageProfile);
            if (!library.State.HddMode) throw new Exception("First-run HDD choice did not save.");
        }
        finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
        library = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
        window = Create(); window.Show();
        try
        {
            Until(() => !((MainWindowViewModel)window.DataContext!).IsBusy);
            if (window.OwnedWindows.OfType<StorageProfileDialog>().Any() || !library.State.HddMode)
                throw new Exception("Saved storage choice was lost or prompted again.");
        }
        finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
        Console.WriteLine("PASS startup notice dismissal, explicit choice and reopen (not native Linux acceptance)");
    }

    private static void Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        if (!condition()) throw new Exception("Startup notice did not reach expected state.");
    }
}
