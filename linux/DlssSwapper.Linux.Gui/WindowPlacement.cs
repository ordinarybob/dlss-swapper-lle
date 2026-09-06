using Avalonia;
using Avalonia.Controls;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

internal static class WindowPlacement
{
    public static void Attach(Window window, PersistentLibrary library, Action<string> reportError)
    {
        var saved = library.State.WindowPlacement;
        if (saved is not null && double.IsFinite(saved.Width) && double.IsFinite(saved.Height)
            && saved.Width > 0 && saved.Height > 0)
        {
            var screen = window.Screens.All.FirstOrDefault(screen =>
                screen.WorkingArea.Contains(new PixelPoint(saved.X, saved.Y))) ?? window.Screens.Primary;
            var area = screen?.WorkingArea;
            var scale = screen?.Scaling ?? 1;
            window.Width = Math.Clamp(saved.Width, window.MinWidth, Math.Max(window.MinWidth, area?.Width / scale ?? saved.Width));
            window.Height = Math.Clamp(saved.Height, window.MinHeight, Math.Max(window.MinHeight, area?.Height / scale ?? saved.Height));
            if (area is { } bounds)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(
                    Math.Clamp(saved.X, bounds.X, Math.Max(bounds.X, bounds.Right - (int)(window.Width * scale))),
                    Math.Clamp(saved.Y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - (int)(window.Height * scale))));
            }
            if (saved.Maximized) window.WindowState = WindowState.Maximized;
        }
        var normal = new SavedWindowPlacement(window.Width, window.Height, window.Position.X, window.Position.Y, saved?.Maximized == true);
        var opened = false;
        void Capture()
        {
            if (!opened || window.WindowState != WindowState.Normal || window.ClientSize.Width <= 0 || window.ClientSize.Height <= 0) return;
            normal = new(window.ClientSize.Width, window.ClientSize.Height, window.Position.X, window.Position.Y, false);
        }
        window.Opened += (_, _) => { opened = true; Capture(); };
        window.PositionChanged += (_, _) => Capture();
        window.SizeChanged += (_, _) => Capture();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty && window.WindowState != WindowState.Minimized)
                normal = normal with { Maximized = window.WindowState == WindowState.Maximized };
        };
        window.Closed += (_, _) =>
        {
            if (!opened) return;
            try { library.UpdateState(state => state.WindowPlacement = normal); }
            catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); reportError("Window position could not be saved: " + error.Message); }
        };
    }
}
