using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class DllRestoreDialogTests
{
    internal static void Run(Window owner, string fixtureRoot)
    {
        var root = Path.Combine(fixtureRoot, "offline-restore"); Directory.CreateDirectory(root);
        var target = Path.Combine(root, "nvngx_dlss.dll");
        var original = StreamlineMutationDialogTests.DllBytes("original");
        var installed = StreamlineMutationDialogTests.DllBytes("installed");
        File.WriteAllBytes(target, installed); File.WriteAllBytes(target + ".dlsss", original);
        var game = new SelectedGame("Offline fixture", root, "fixture-id");
        var window = new DllRestoreWindow(game); var closed = window.ShowDialog(owner);
        Button Action(Window dialog, string name) => dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
        void Click(Window dialog, string name) => Action(dialog, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Until(Func<bool> condition)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10)) { TestUi.Flush(); Thread.Sleep(1); }
            if (!condition()) throw new Exception("Offline restore timed out");
        }
        ConfirmationDialog Confirm()
        {
            Until(() => window.OwnedWindows.OfType<ConfirmationDialog>().Any());
            return window.OwnedWindows.OfType<ConfirmationDialog>().Single();
        }
        Until(() => window.GetVisualDescendants().OfType<CheckBox>().Any());
        window.Width = window.MinWidth; window.Height = window.MinHeight; TestUi.Flush();
        CheckActions(window);
        window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
        Click(window, "Restore selected families"); Click(Confirm(), "Cancel");
        Until(() => Action(window, "Restore selected families").IsEnabled);
        Check(File.ReadAllBytes(target).SequenceEqual(installed) && File.ReadAllBytes(target + ".dlsss").SequenceEqual(original), "Cancelled restore changed files");
        Click(window, "Restore selected families"); Click(Confirm(), "Continue");
        Until(() => window.Results.Count > 0 && Action(window, "Close").IsEnabled);
        Check(File.ReadAllBytes(target).SequenceEqual(original) && !File.Exists(target + ".dlsss"), "Offline restore failed");
        TestUi.Flush(); CheckActions(window);
        Check(window.Results.Count == 1 && window.Results[0].Success && window.Results[0].Game.SteamAppId == "fixture-id", "Restore result lost game identity");
        var scan = new DllScanner().Scan(game, DllCatalog.Empty());
        Check(scan.Dlls.Count == 1 && scan.Dlls[0].Md5 == DllScanner.ComputeMd5(target), "Catalog-free rescan failed");
        Click(window, "Close"); Check(closed.IsCompleted, "Restore failed to close");
        Console.WriteLine("PASS headless catalog-free original restore/cancel/rescan (not native Linux acceptance)");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void CheckActions(Window window)
    {
        foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(button => button.Content is string text
            && new[] { "Restore selected families", "Close" }.Contains(text)))
        {
            var point = button.TranslatePoint(default, window)!.Value;
            Check(point.Y >= 0 && point.Y + button.Bounds.Height <= window.ClientSize.Height + 1
                && point.X >= 0 && point.X + button.Bounds.Width <= window.ClientSize.Width + 1, "Restore action clipped: " + button.Content);
            Check(!button.GetVisualAncestors().OfType<ScrollViewer>().Any(), "Restore action is inside scrolling content");
        }
    }
}
