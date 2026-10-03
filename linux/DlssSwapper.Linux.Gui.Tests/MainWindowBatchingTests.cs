using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;
using DlssSwapper.Linux.Gui;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class MainWindowBatchingTests
{
    internal static void Run(string root)
    {
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "large-view-state")));
        var games = Enumerable.Range(0, 200).Select(i => new SteamGame(i.ToString(), $"Game {i:000}",
            Path.Combine(root, $"cached-game-{i:000}"), root, Path.Combine(root, $"appmanifest_{i}.acf"))).ToArray();
        library.UpdateState(state =>
        {
            state.HasSelectedStorageProfile = true; state.HasCompletedInitialDeepScan = true;
            state.UiCollectionBatchSize = 10; state.GridView = false;
        });
        library.UpdateGamePreference(games[1].InstallDirectory, preference => preference.IsFavorite = true);
        DiscoverySnapshot.Save(library, new(games, []), new([], []));
        DiscoverySnapshot.SaveScans(library, games.Select(game => new ScanResult(new(game.Name, game.InstallDirectory, game.AppId),
            [new(DllType.Dlss, Path.Combine(game.InstallDirectory, "nvngx_dlss.dll"), "nvngx_dlss.dll", "", "2.0")], [])).ToArray());
        var pending = new TaskCompletionSource<SteamDiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new MainWindow(new MainWindowServices(
            () => new(DllCatalog.Empty(), new PersistentLibrary(new LibraryStateStore(library.StateDirectory)), null),
            token => pending.Task.WaitAsync(token), (_, _) => new([], []), () => null));
        try
        {
            window.Show();
            var vm = (MainWindowViewModel)window.DataContext!;
            Pump(() => vm.IsBusy && !vm.IsPublishingView);
            vm.StatusText = new string('W', 200);
            foreach (var width in new[] { 720, 800, 1100 })
            {
                window.Width = width; Dispatcher.UIThread.RunJobs();
                var header = window.FindControl<Grid>("GameHeaderStatus")!;
                var toolbar = window.FindControl<StackPanel>("GameToolbar")!;
                var progress = window.FindControl<ProgressBar>("GameHeaderProgress")!;
                var status = window.FindControl<TextBlock>("GameHeaderStatusText")!;
                var right = header.TranslatePoint(default, window)!.Value.X + header.Bounds.Width;
                Check(right <= toolbar.TranslatePoint(default, window)!.Value.X && header.ClipToBounds
                    && progress.Bounds.Width <= 20 && status.Bounds.Right <= header.Bounds.Width + 1,
                    "Busy header overlaps toolbar at width " + width);
            }
            Check(vm.Games.Count == 200 && vm.GameGroups.Sum(group => group.Items.Count) == 201,
                "Batched startup lost rows or favourite duplication");
            var selected = vm.Games.Single(row => row.Name == "Game 001"); selected.IsSelected = true;
            var search = window.FindControl<TextBox>("SearchTextBox")!;
            search.Text = "Game 019";
            Dispatcher.UIThread.RunJobs();
            search.Text = "Game 001";
            Pump(() => !vm.IsPublishingView && vm.Games.Count == 1 && vm.Games[0].Name == "Game 001");
            Check(vm.Games[0].IsSelected && vm.SelectedCount == 1
                && vm.GameGroups.All(group => group.Items.All(row => row.Name == "Game 001")),
                "Latest filter lost selection or retained stale rendered rows");
            search.Text = "";
            Pump(() => !vm.IsPublishingView && vm.Games.Count == 200);
            Check(vm.Games.Single(row => row.Name == "Game 001").IsSelected, "Clearing filter discarded selected root");
            vm.IsGridView = true;
            Dispatcher.UIThread.RunJobs();
            Check(vm.GameGroups.Sum(group => group.Items.Count) == 201, "Grid mode changed grouped membership");
            var changedDuringRefresh = false;
            vm.Games.CollectionChanged += (_, change) =>
            {
                if (!changedDuringRefresh && vm.IsBusy
                    && change.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                {
                    changedDuringRefresh = true;
                    search.Text = "Game 019";
                }
            };
            pending.SetResult(new(games, []));
            Pump(() => !vm.IsBusy && !vm.IsPublishingView);
            Check(changedDuringRefresh && vm.Games.Count == 1 && vm.Games[0].Name == "Game 019"
                && vm.GameGroups.All(group => group.Items.All(row => row.Name == "Game 019")),
                "Refresh ignored the filter changed during row preparation");
            search.Text = "";
            Pump(() => !vm.IsPublishingView && vm.Games.Count == 200);
            Check(vm.Games.Count == 200 && vm.Games.Single(row => row.Name == "Game 001").IsSelected,
                "Refresh row preparation lost cached membership or selection");
        }
        finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
        Console.WriteLine($"PASS actual MainWindow {("JSON")} 200-row batching/filter/selection/grid/refresh (not native responsiveness acceptance)");
    }
    private static void Pump(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(20))
        { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        Check(condition(), "Batched main-window view did not finish");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
