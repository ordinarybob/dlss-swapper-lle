using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class PerformanceSettingsTests
{
    internal static void Run(string root)
    {
        var store = new LibraryStateStore(Path.Combine(root, "performance-state"));
        var library = new PersistentLibrary(store);
        library.UpdateState(state => { state.HddMode = true; state.CardSize = 7; });
        var window = new SettingsWindow(library); window.Show();
        Input(window, "UiBatchInput").Value = 10;
        Click(window, "Cancel");
        Check(library.State.UiCollectionBatchSize == 550, "Cancel changed UI limit");
        window = new SettingsWindow(library); window.Show();
        foreach (var name in new[] { "ScanConcurrencyInput", "ArtworkConcurrencyInput", "BatchConcurrencyInput" }) Input(window, name).Value = 3;
        Input(window, "UiBatchInput").Value = 20;
        Click(window, "Reset performance defaults");
        Check(Input(window, "ScanConcurrencyInput").Value == 15 && Input(window, "ArtworkConcurrencyInput").Value == 38
            && Input(window, "BatchConcurrencyInput").Value == 15 && Input(window, "UiBatchInput").Value == 550
            && window.FindControl<CheckBox>("HddModeCheckBox")!.IsChecked == true,
            "Full reset missed limits or changed HDD preference");
        Input(window, "UiBatchInput").Value = 10;
        Click(window, "Save");
        Check(!window.IsVisible && new PersistentLibrary(new LibraryStateStore(store.StateDirectory)).State.UiCollectionBatchSize == 10,
            "UI limit did not save/reopen");
        window = new SettingsWindow(library); window.Show();
        Input(window, "UiBatchInput").Value = 30;
        using (var held = new FileStream(Path.Combine(store.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Click(window, "Save");
            Check(window.IsVisible && Input(window, "UiBatchInput").Value == 30 && library.State.UiCollectionBatchSize == 10,
                "Failed save lost the draft or replaced committed limit");
        }
        Click(window, "Cancel");
        Console.WriteLine("PASS headless UI limit/save/cancel/failure/full performance reset (not native acceptance)");
    }
    private static NumericUpDown Input(Window window, string name) => window.FindControl<NumericUpDown>(name)!;
    private static void Click(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
