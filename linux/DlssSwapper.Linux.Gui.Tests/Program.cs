using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // No desktop lifetime: App cannot construct MainWindow or discover real libraries.
        // Use real text layout: the dummy renderer stalls on blank-line confirmation text.
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        var root = Path.Combine(Path.GetTempPath(), "lle-ui-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            if (args.Contains("--streamline-ui-tests"))
            {
                var sdkOwner = new Window { Width = 1100, Height = 800 };
                sdkOwner.Show();
                try
                {
                    StreamlineDialogTests.Run(sdkOwner, root);
                    BatchDialogTests.Run(sdkOwner, root);
                    StreamlineLibraryHistoryTests.Run(root);
                    Console.WriteLine("PASS: targeted Streamline version selection and batch UI regressions (headless).");
                }
                finally { sdkOwner.Close(); }
                return 0;
            }
            var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "state")));
            library.UpdateState(state => { });
            var gameRoot = Path.Combine(root, "Folder name"); Directory.CreateDirectory(gameRoot);
            var cachedScan = new ScanResult(new("Cached fixture", gameRoot, null),
                [new(DllType.Dlss, Path.Combine(gameRoot, "nvngx_dlss.dll"), "nvngx_dlss.dll", "", "2.0")], [])
                { CachedAtUtc = DateTimeOffset.UtcNow };
            var cachedRow = new DlssSwapper.Linux.Gui.ViewModels.GameRowViewModel(cachedScan.Game);
            cachedRow.SetScanResult(cachedScan);
            Check(cachedRow.ScanSummary.StartsWith("Last known") && cachedRow.CardDllVersion.Contains("last known")
                && cachedRow.ScanDetail.Contains("not verified this session"), "Cached results were presented as freshly scanned");
            cachedRow.SetScanResult(cachedScan with { CachedAtUtc = null });
            Check(!cachedRow.ScanSummary.Contains("Last known") && !cachedRow.CardDllVersion.Contains("last known")
                && !cachedRow.ScanDetail.Contains("not verified this session"), "Fresh scan retained stale-cache labels");
            Console.WriteLine("PASS cached game-row display and fresh-scan label replacement (not native Linux acceptance)");
            var owner = new Window(); owner.Show();
            try
            {
                var dialog = new ManualGameImportDialog(library, gameRoot, () => []);
                var result = dialog.ShowDialog<ManualGameState?>(owner);
                dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
                CheckActionsVisible(dialog);
                var fields = dialog.GetVisualDescendants().OfType<TextBox>().ToArray();
                Check(fields.Single(field => !field.IsReadOnly).Text == "Folder name", "Name was not prepopulated");
                Check(fields.Single(field => field.IsReadOnly).Text == gameRoot, "Read-only installation path missing");
                fields.Single(field => !field.IsReadOnly).Text = "Edited title";
                dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Pump(result);
                Check(result.Result is null && library.State.ManualGames.Count == 0, "Cancel saved a draft game");

                dialog = new ManualGameImportDialog(library, gameRoot, () => []);
                result = dialog.ShowDialog<ManualGameState?>(owner);
                dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
                dialog.GetVisualDescendants().OfType<TextBox>().Single(field => !field.IsReadOnly).Text = "Edited title";
                using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Click(dialog, "Add game"); Dispatcher.UIThread.RunJobs();
                    Check(!result.IsCompleted && library.State.ManualGames.Count == 0, "Failed save closed dialog or registered game");
                    Check(dialog.GetVisualDescendants().OfType<TextBox>().Single(field => !field.IsReadOnly).Text == "Edited title", "Failed save lost edited name");
                    Check(dialog.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("Could not add game:") == true), "Save error not displayed");
                    CheckActionsVisible(dialog);
                }
                dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add game")).Focus();
                dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                if (dialog.IsVisible) dialog.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Pump(result);
                Check(result.Result?.Name == "Edited title", "Save did not return imported game");
                Check(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.ManualGames.Single().Name == "Edited title", "Save did not persist title");
                Console.WriteLine("PASS headless import cancel/save controls (not native Linux acceptance)");
                var settings = new SettingsWindow(library);
                var settingsResult = settings.ShowDialog<bool>(owner);
                CheckBox ManualToggle() => settings.GetVisualDescendants().OfType<CheckBox>().Single(control => Equals(control.Content, "Manually Added"));
                ManualToggle().IsChecked = false;
                Check(!LibrarySelection.Enabled(library.State, "Manually Added") && library.State.ManualGames.Count == 1, "Toggle did not disable while preserving game");
                var row = ManualToggle().GetVisualAncestors().OfType<Grid>().First();
                row.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Move up"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(LibrarySelection.Read(library.State)[^2].Id == "Manually Added", "Move-up action did not persist order");
                using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    ManualToggle().IsChecked = true;
                    Check(ManualToggle().IsChecked == false && !LibrarySelection.Enabled(library.State, "Manually Added"), "Failed selector save left wrong checkbox state");
                }
                Click(settings, "Cancel"); Pump(settingsResult);
                Check(settings.LibrarySelectionChanged && !LibrarySelection.Enabled(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State, "Manually Added"), "Cancel discarded immediate selector changes");
                Console.WriteLine("PASS headless library toggle/reorder/failed-save controls (not native Linux acceptance)");
                settings = new SettingsWindow(library); settingsResult = settings.ShowDialog<bool>(owner);
                settings.FindControl<CheckBox>("OnlyDownloadedCheckBox")!.IsChecked = true;
                settings.FindControl<NumericUpDown>("ScanConcurrencyInput")!.Value = 7;
                settings.FindControl<NumericUpDown>("ArtworkConcurrencyInput")!.Value = 9;
                settings.FindControl<NumericUpDown>("BatchConcurrencyInput")!.Value = 3;
                settings.FindControl<NumericUpDown>("UiBatchInput")!.Value = 30;
                Click(settings, "Cancel"); Pump(settingsResult);
                Check(library.State.UiCollectionBatchSize == 550, "Cancel saved UI batch limit");
                Check(library.State.BatchSwapConcurrency == 15, "Cancel saved batch concurrency");
                Check(!library.State.OnlyShowDownloadedDlls, "Cancelled settings saved downloaded-only preference");
                Check(library.State.ScanConcurrency is null && library.State.ArtworkConcurrency is null, "Cancelled settings saved concurrency overrides");
                settings = new SettingsWindow(library); settingsResult = settings.ShowDialog<bool>(owner);
                settings.FindControl<CheckBox>("OnlyDownloadedCheckBox")!.IsChecked = true;
                settings.FindControl<CheckBox>("HddModeCheckBox")!.IsChecked = true;
                Check(settings.FindControl<NumericUpDown>("UiBatchInput")!.Value == 550, "Storage profile did not reset UI batch limit");
                Check(settings.FindControl<NumericUpDown>("BatchConcurrencyInput")!.Value == 15, "HDD profile changed the batch default");
                Check(settings.FindControl<NumericUpDown>("ScanConcurrencyInput")!.Value == 2 && settings.FindControl<NumericUpDown>("ArtworkConcurrencyInput")!.Value == 1, "HDD defaults not applied");
                settings.FindControl<CheckBox>("HddModeCheckBox")!.IsChecked = false;
                settings.FindControl<NumericUpDown>("ScanConcurrencyInput")!.Value = 7;
                Click(settings, "Reset performance defaults");
                Check(settings.FindControl<NumericUpDown>("ScanConcurrencyInput")!.Value == 15 && settings.FindControl<NumericUpDown>("ArtworkConcurrencyInput")!.Value == 38, "Standard defaults not restored");
                settings.FindControl<NumericUpDown>("ScanConcurrencyInput")!.Value = 7;
                settings.FindControl<NumericUpDown>("ArtworkConcurrencyInput")!.Value = 9;
                settings.FindControl<NumericUpDown>("BatchConcurrencyInput")!.Value = 3;
                settings.FindControl<NumericUpDown>("UiBatchInput")!.Value = 30;
                Click(settings, "Save"); Pump(settingsResult);
                Check(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.UiCollectionBatchSize == 30,
                    "UI batch limit did not persist");
                Check(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.BatchSwapConcurrency == 3, "Batch concurrency did not persist");
                Check(settingsResult.Result && new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.OnlyShowDownloadedDlls, "Downloaded-only preference did not persist");
                settings = new SettingsWindow(library); settingsResult = settings.ShowDialog<bool>(owner);
                settings.FindControl<NumericUpDown>("UiBatchInput")!.Value = 80;
                using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Click(settings, "Save"); Dispatcher.UIThread.RunJobs();
                    Check(!settingsResult.IsCompleted && library.State.UiCollectionBatchSize == 30,
                        "Failed UI batch save closed settings or changed committed value");
                    Check(settings.FindControl<NumericUpDown>("UiBatchInput")!.Value == 80, "Failed save discarded edited batch limit");
                }
                Click(settings, "Cancel"); Pump(settingsResult);
                var savedPerformance = new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.Performance;
                Check(savedPerformance.ScanConcurrency == 7 && savedPerformance.ArtworkConcurrency == 9, "Concurrency overrides did not persist");
                Check(library.State.HasSelectedStorageProfile, "Saving performance settings did not mark the storage profile selected");
                Check(new LinuxLibraryState { ScanConcurrency = -5, ArtworkConcurrency = 100 }.Performance == new PerformanceLimits(1, 64), "Concurrency bounds not enforced");
                Console.WriteLine("PASS headless downloaded-only settings cancel/save/reload (not native Linux acceptance)");
            StreamlineDialogTests.Run(owner, root);
            StreamlineLibraryHistoryTests.Run(root);
                BatchDialogTests.Run(owner, root);
                DllRestoreDialogTests.Run(owner, root);
                GameDllPickerDialogTests.Run(owner, root);
                ManualLaunchDialogTests.Run(owner, root);
                ThemeDialogTests.Run(owner, root);
                MainWindowStartupTests.Run(root);
                DiagnosticsTests.Run(owner, root);
                MainWindowBatchingTests.Run(root);
                PerformanceSettingsTests.Run(root);
                LanguageDialogTests.Run(owner, library);
                TranslationToolboxTests.Run(owner);
                LifecycleTests.Run(owner, root);
                StartupNoticeTests.Run(root);
                IgnoredPathSettingsTests.Run(owner, root);
                AppLogTests.Run(owner, root);
                AcknowledgementsTests.Run(owner);
                LibraryPageLifecycleTests.Run(root);
            }
            finally { owner.Close(); }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }

    private static void Click(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void CheckActionsVisible(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        var error = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Text?.StartsWith("Could not add game:") == true);
        if (error is not null)
        {
            var cover = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add cover (optional)"));
            var coverOrigin = cover.TranslatePoint(default, window)!.Value;
            var errorOrigin = error.TranslatePoint(default, window)!.Value;
            Check(coverOrigin.Y + cover.Bounds.Height <= errorOrigin.Y, "Cover overlaps save error at minimum dialog size");
        }
        foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(button => button.Content is string text && text is "Add game" or "Cancel"))
        {
            var origin = button.TranslatePoint(default, window) ?? throw new Exception("Action is detached");
            Check(button.Bounds.Width > 0 && button.Bounds.Height > 0 && origin.X >= 0 && origin.Y >= 0
                && origin.X + button.Bounds.Width <= window.ClientSize.Width + 1
                && origin.Y + button.Bounds.Height <= window.ClientSize.Height + 1, "Action is clipped at minimum dialog size");
        }
    }
    private static void Pump(Task task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(5))
        { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        Check(task.IsCompleted, "Dialog did not complete");
        task.GetAwaiter().GetResult();
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
