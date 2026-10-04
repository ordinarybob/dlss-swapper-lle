using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Gui;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class MainWindowParityChecks
{
    internal static void Run(MainWindow window, MainWindowViewModel vm)
    {
        var toolbar = window.FindControl<StackPanel>("GameToolbar")!;
        var commands = toolbar.Children.OfType<Button>().ToArray();
        Check(commands.Length == 7, "Games toolbar lost a Windows command.");
        var refresh = commands.Single(button => AutomationProperties.GetName(button) == "Refresh");
        Check(refresh.Flyout is MenuFlyout { Items.Count: 2 }, "Refresh must offer preserve-excluded and restore-excluded actions.");
        var filter = commands.Single(button => AutomationProperties.GetName(button) == "Filter");
        filter.Flyout!.ShowAt(filter);
        TestUi.Flush();
        foreach (var name in new[] { "HideNonSwappableCheckBox", "ShowHiddenCheckBox", "GroupLibrariesCheckBox" })
            Check(window.FindControl<CheckBox>(name) is not null, "Missing Windows filter: " + name);
        var hidden = window.FindControl<CheckBox>("ShowHiddenCheckBox")!;
        hidden.IsChecked = true;
        filter.Flyout.Hide(); filter.Flyout.ShowAt(filter);
        Check(hidden.IsChecked == true, "Filter selection was not retained.");
        hidden.IsChecked = false; filter.Flyout.Hide();
        var sort = commands.Single(button => AutomationProperties.GetName(button) == "Sort");
        var sortItems = ((MenuFlyout)sort.Flyout!).Items.OfType<MenuItem>().ToArray();
        Check(sortItems.Select(item => item.Tag?.ToString()).SequenceEqual(new[] { "1", "2", "0" }),
            "Sort actions do not match DLSS newest, DLSS oldest, name.");
        sortItems[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        sortItems[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        var wasGrid = vm.IsGridView;
        var width = window.Width;
        vm.IsGridView = false; vm.IsBatchMode = true;
        foreach (var size in new[] { 620d, 720d, 800d, 1100d })
        {
            window.Width = size;
            TestUi.Flush();
            var header = window.FindControl<Grid>("GameHeaderStatus")!;
            var headerRect = Bounds(header, window); var toolbarRect = Bounds(toolbar, window);
            Check(!headerRect.Intersects(toolbarRect), "Games title overlaps commands at width " + size);
            foreach (var button in commands) InWindow(button, window);
            var selection = window.FindControl<Border>("SelectionCommandsHost")!;
            Check(!Bounds(selection, window).Intersects(toolbarRect), "Selection commands overlap the toolbar at width " + size);
            foreach (var button in selection.GetVisualDescendants().OfType<Button>()) InWindow(button, window);
            var row = window.GetVisualDescendants().OfType<Border>().First(control => control.Classes.Contains("row") && control.IsEffectivelyVisible);
            var checkbox = row.GetVisualDescendants().OfType<CheckBox>().Single();
            foreach (var text in row.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Bounds.Width > 0))
                Check(!Bounds(checkbox, row).Intersects(Bounds(text, row)), "Game selection checkbox overlaps game text.");
        }
        vm.IsBatchMode = false; vm.IsGridView = wasGrid; window.Width = width;
        TestUi.Flush();
        Console.WriteLine("PASS Games menu functions and non-overlapping headers, selection controls and game rows at 620–1100 DIPs");
    }

    private static Rect Bounds(Control control, Visual parent) => new(control.TranslatePoint(default, parent)!.Value, control.Bounds.Size);
    private static void InWindow(Control control, Window window)
    {
        var bounds = Bounds(control, window);
        Check(bounds.Width > 0 && bounds.Height > 0 && bounds.X >= 0 && bounds.Y >= 0
            && bounds.Right <= window.ClientSize.Width + 1 && bounds.Bottom <= window.ClientSize.Height + 1,
            "Clipped action: " + (AutomationProperties.GetName(control) ?? control.Name));
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
