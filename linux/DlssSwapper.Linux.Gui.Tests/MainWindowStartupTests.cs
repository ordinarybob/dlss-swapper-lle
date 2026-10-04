using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;
using DlssSwapper.Linux.Gui;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class MainWindowStartupTests
{
    internal static void Run(string root)
    {
        var stateRoot = Path.Combine(root, "startup-state");
        var gameRoot = Path.Combine(root, "startup-game");
        Directory.CreateDirectory(gameRoot);
        File.WriteAllBytes(Path.Combine(gameRoot, "nvngx_dlss.dll"), StreamlineMutationDialogTests.DllBytes("startup"));
        var library = new PersistentLibrary(new LibraryStateStore(stateRoot));
        library.UpdateState(state =>
        {
            state.HasSelectedStorageProfile = true;
            state.HasCompletedInitialDeepScan = true;
            state.GridView = false;
        });
        var game = new SteamGame("42", "Startup fixture", gameRoot, root, Path.Combine(root, "appmanifest_42.acf"));
        DiscoverySnapshot.Save(library, new([game], []), new([], []));
        DiscoverySnapshot.SaveScans(library, [new(new(game.Name, gameRoot, game.AppId),
            [new(DllType.Dlss, Path.Combine(gameRoot, "nvngx_dlss.dll"), "nvngx_dlss.dll", "", "2.0")], [])]);
        var complete = new DiscoverySourceOutcome("SteamLibrary", root, DiscoverySourceState.Complete);

        var original = StreamlineMutationDialogTests.DllBytes("original").Concat(new byte[512]).ToArray();
        File.WriteAllBytes(Path.Combine(gameRoot, "nvngx_dlss.dll.dlsss"), original);
        RunWindow(new([game], []) { Sources = [complete] }, (window, vm) =>
        {
            Check(vm.Games.Single().ScanResult?.CachedAtUtc is null, "Successful refresh remained cached");
            Check(vm.Games.Single().ScanResult!.Dlls.Count == 1, "Actual fixture DLL was not scanned");
            var originalRow = vm.Games.Single();
            window.FindControl<Button>("SettingsNavigationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var page = window.FindControl<ContentControl>("SettingsPageHost")!.Content as SettingsPage
                ?? throw new Exception("Settings navigation did not show its page.");
            Check(window.OwnedWindows.Count == 0, "Settings navigation opened a modal.");
            Check(!page.FindControl<StackPanel>("SettingsSaveActions")!.IsVisible, "Production settings still require a separate Save step.");
            page.FindControl<NumericUpDown>("BatchConcurrencyInput")!.Value = 4;
            TestUi.Flush();
            Check(new PersistentLibrary(new LibraryStateStore(stateRoot)).State.BatchSwapConcurrency == 4,
                "Changing a performance setting did not persist immediately.");
            var roots = page.FindControl<TextBox>("AdditionalSteamRootsTextBox")!;
            roots.Text = root;
            window.FindControl<Button>("GamesNavigationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            roots.RaiseEvent(new Avalonia.Input.FocusChangedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
            TestUi.Flush();
            Check(new PersistentLibrary(new LibraryStateStore(stateRoot)).State.AdditionalSteamRoots.Contains(root),
                "Leaving Settings discarded the text field edit.");
            Check(ReferenceEquals(originalRow, vm.Games.Single()), "Navigation rebuilt the Games state.");
            window.FindControl<Button>("SettingsNavigationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(ReferenceEquals(page, window.FindControl<ContentControl>("SettingsPageHost")!.Content)
                && page.FindControl<NumericUpDown>("BatchConcurrencyInput")!.Value == 4, "Navigation lost the Settings state.");
            window.FindControl<Button>("LibraryNavigationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var libraryPage = window.FindControl<ContentControl>("LibraryPageHost")!.Content as LibraryPage
                ?? throw new Exception("Library navigation did not show its page.");
            Check(window.OwnedWindows.Count == 0, "Library navigation opened a modal.");
            libraryPage.FindControl<TextBox>("SearchTextBox")!.Text = "retained filter";
            window.FindControl<Button>("GamesNavigationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.FindControl<Button>("LibraryNavigationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(ReferenceEquals(libraryPage, window.FindControl<ContentControl>("LibraryPageHost")!.Content)
                && libraryPage.FindControl<TextBox>("SearchTextBox")!.Text == "retained filter"
                && ReferenceEquals(originalRow, vm.Games.Single()), "Library navigation lost filters or Games state.");
            window.FindControl<Button>("GamesNavigationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            TestUi.Flush();
            MainWindowParityChecks.Run(window, vm);
            var card = window.GetVisualDescendants().OfType<Border>().First(control => control.Classes.Contains("row") && control.IsEffectivelyVisible);
            card.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
            PumpUntil(() => window.OwnedWindows.OfType<GameDetailsWindow>().Any());
            var details = window.OwnedWindows.OfType<GameDetailsWindow>().Single();
            details.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Tag, GameDetailsAction.Restore))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpUntil(() => details.OwnedWindows.OfType<DllRestoreWindow>().Any());
            var restore = details.OwnedWindows.OfType<DllRestoreWindow>().Single();
            PumpUntil(() => restore.GetVisualDescendants().OfType<CheckBox>().Any());
            restore.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
            Click(restore, "Restore selected families");
            PumpUntil(() => restore.OwnedWindows.OfType<ConfirmationDialog>().Any());
            Click(restore.OwnedWindows.OfType<ConfirmationDialog>().Single(), "Continue");
            PumpUntil(() => restore.Results.Count == 1);
            Check(restore.Results.Single().Success, "Main-window restore failed");
            PumpUntil(() => restore.GetVisualDescendants().OfType<Button>()
                .Single(button => Equals(button.Content, "Close")).IsEnabled);
            Click(restore, "Close");
            PumpUntil(() => !vm.IsBusy);
            Check(File.ReadAllBytes(Path.Combine(gameRoot, "nvngx_dlss.dll")).SequenceEqual(original),
                "Main-window restore did not restore the original bytes");
            Check(vm.Games.Single().ScanResult!.Dlls.Single().FileLength == original.Length,
                "Main-window row retained pre-restore file metadata");
            Check(DiscoverySnapshot.ReadScans(new PersistentLibrary(new LibraryStateStore(stateRoot)))
                .Single().Dlls.Single().FileLength == original.Length, "Post-restore cache was not persisted");
            details.Close();
            PumpUntil(() => !details.IsVisible);
        });
        Directory.Move(gameRoot, gameRoot + "-unavailable");
        RunWindow(new([game], []) { Sources = [complete] }, (_, vm) =>
        {
            var scan = vm.Games.Single().ScanResult!;
            Check(scan.CachedAtUtc is not null && scan.Dlls.Count == 1 && scan.Warnings.Count > 0,
                "Unavailable installation lost last-known DLLs or its current warning");
            Check(scan.Dlls.Single().FileLength == original.Length, "Reopened window lost the restored file metadata");
        });
        RunWindow(new([], []) { Sources = [complete] }, (_, vm) =>
        {
            Check(vm.GameCount == 0, "Cache save failure discarded fresh discovery results");
            Check(vm.AlertText.Contains("Scan results could not be saved"), "Cache save failure was hidden");
            Check(DiscoverySnapshot.ReadScans(new PersistentLibrary(new LibraryStateStore(stateRoot))).Count == 1,
                "Failed save corrupted the previous library snapshot");
        }, holdSaveLock: true);
        RunWindow(new([], []) { Sources = [complete] }, (_, vm) =>
            Check(vm.GameCount == 0, "Complete-empty discovery retained removed game"));
        Check(DiscoverySnapshot.ReadScans(new PersistentLibrary(new LibraryStateStore(stateRoot))).Count == 0,
            "Removed game remained visible after state reload");
        Console.WriteLine($"PASS actual headless MainWindow {("JSON")} cache-first/restore/rescan/unavailable/removal/reopen (not native Linux acceptance)");

        void RunWindow(SteamDiscoveryResult discovery, Action<MainWindow, MainWindowViewModel> verify, bool holdSaveLock = false)
        {
            var pending = new TaskCompletionSource<SteamDiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new MainWindow(new MainWindowServices(
                () => new(DllCatalog.Empty(), new PersistentLibrary(new LibraryStateStore(stateRoot)), null),
                token => pending.Task.WaitAsync(token), (_, _) => new([], []), () => null));
            try
            {
                window.Show();
                var vm = (MainWindowViewModel)window.DataContext!;
                PumpUntil(() => vm.IsBusy);
                Check(vm.GameCount == 1 && vm.Games.Single().ScanResult?.CachedAtUtc is not null,
                    "MainWindow did not display saved data before discovery completed");
                using var held = holdSaveLock
                    ? new FileStream(Path.Combine(stateRoot, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    : null;
                pending.SetResult(discovery);
                PumpUntil(() => !vm.IsBusy);
                Check(!vm.HasStartupError, "MainWindow reported a startup error");
                verify(window, vm);
            }
            finally { window.Close(); TestUi.Flush(); }
        }
    }

    private static void Click(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void PumpUntil(Func<bool> condition)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        { TestUi.Flush(); Thread.Sleep(1); }
        Check(condition(), "MainWindow startup did not reach the expected state");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
