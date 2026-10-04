using Avalonia.Threading;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class TestUi
{
    internal static bool Native { get; set; }
    internal static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        if (!Native) return;
        var frame = new DispatcherFrame();
        using var stop = DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(16));
        Dispatcher.UIThread.PushFrame(frame);
    }
}
