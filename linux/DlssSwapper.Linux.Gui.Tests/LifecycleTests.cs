using Avalonia.Controls;
using Avalonia.Threading;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class LifecycleTests
{
    public static void Run(Window owner, string root)
    {
        var identity = "fixture-" + Guid.NewGuid().ToString("N");
        using (var primary = new SingleInstance(identity))
        {
            if (!primary.IsPrimary) throw new Exception("First instance did not acquire ownership.");
            var activations = 0;
            primary.Listen(() => Interlocked.Increment(ref activations));
            for (var index = 0; index < 2; index++)
            {
                Exception? failure = null;
                var secondaryThread = new Thread(() =>
                {
                    try
                    {
                        using var secondary = new SingleInstance(identity);
                        if (secondary.IsPrimary) throw new Exception("Duplicate instance acquired ownership.");
                        secondary.ActivateExisting();
                    }
                    catch (Exception error) { failure = error; }
                });
                secondaryThread.Start();
                if (!secondaryThread.Join(TimeSpan.FromSeconds(8))) throw new Exception("Activation did not finish.");
                if (failure is not null) throw new Exception("Secondary activation failed.", failure);
            }
            if (activations != 2) throw new Exception("Repeated activation was lost.");
        }
        using (var reopened = new SingleInstance(identity))
            if (!reopened.IsPrimary) throw new Exception("Closed instance retained ownership.");

        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "window-placement")));
        var errors = new List<string>();
        var window = new Window { Width = 780, Height = 660, MinWidth = 600, MinHeight = 400 };
        WindowPlacement.Attach(window, library, errors.Add);
        window.Show(owner); Dispatcher.UIThread.RunJobs();
        window.Width = 810; window.Height = 680; Dispatcher.UIThread.RunJobs();
        window.WindowState = WindowState.Maximized; Dispatcher.UIThread.RunJobs();
        window.WindowState = WindowState.Minimized; Dispatcher.UIThread.RunJobs();
        window.Close(); Dispatcher.UIThread.RunJobs();
        var saved = new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.WindowPlacement;
        if (saved is not { Width: 810, Height: 680, Maximized: true } || errors.Count != 0)
            throw new Exception("Normal geometry/maximized state did not survive closing minimized: " + saved);
        var restored = new Window { MinWidth = 600, MinHeight = 400 };
        WindowPlacement.Attach(restored, library, errors.Add);
        if (restored.Width != 810 || restored.Height != 680 || restored.WindowState != WindowState.Maximized)
            throw new Exception("Saved geometry was not restored.");
        restored.Close();
        Console.WriteLine("PASS exclusive activation/release and saved normal/maximized geometry (not native Linux acceptance)");
    }
}
