using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Gui.ViewModels;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class LibraryPageLifecycleTests
{
    public static void Run(string root)
    {
        var handler = new PendingResponse();
        using var http = new HttpClient(handler);
        var page = new LibraryPage(DllCatalog.Empty(), null, new DownloadCache(cacheRoot: Path.Combine(root, "page-cache")), http);
        var host = new Window { Content = page, Width = 664, Height = 620 }; host.Show();
        Dispatcher.UIThread.RunJobs();
        foreach (var name in new[] { "ImportButton", "ExportAllButton", "DownloadLatestButton", "RefreshButton" })
        {
            var button = page.FindControl<Button>(name) ?? throw new Exception($"Missing Library action: {name}");
            var position = button.TranslatePoint(default, page) ?? throw new Exception($"Unattached Library action: {name}");
            if (position.X < 0 || position.Y < 0 || position.X + button.Bounds.Width > page.Bounds.Width
                || position.Y + button.Bounds.Height > page.Bounds.Height || button.Bounds.Height <= 0)
                throw new Exception($"Library action is clipped at the minimum window size: {name}");
        }
        if (page.FindControl<Grid>("LibraryDownloadProgressHost")!.IsVisible)
            throw new Exception("Idle Library shows an empty progress bar.");
        var row = new DllLibraryEntryViewModel(new DllCatalogEntry(DllType.Dlss, "310.9", 1, new string('a', 32), "", null, 1024, 0, true, false), false);
        page.FindControl<ListBox>("EntryListBox")!.ItemsSource = new[] { row };
        Dispatcher.UIThread.RunJobs();
        CheckCard(2); // Info and download.
        row.IsDownloading = true;
        CheckCard(2); // Info and cancel.
        if (!row.ShowDownload || row.ShowExport || row.ShowRemove) throw new Exception("Downloading card has incorrect actions.");
        row.IsCached = true;
        row.IsDownloading = false;
        CheckCard(3); // Info, export and remove.
        if (row.ShowDownload || !row.ShowExport || !row.ShowRemove) throw new Exception("Cached card has incorrect actions.");
        row.IsCached = false;
        CheckCard(2);
        var catalog = DllCatalog.Empty();
        catalog.AddImported(row.Entry with { IsImported = true });
        page.UpdateCatalog(catalog);
        var releases = page.FindControl<ListBox>("EntryListBox")!;
        row = releases.Items.Cast<DllLibraryEntryViewModel>().Single();
        row.IsCached = true;
        var families = page.FindControl<ListBox>("FamilyComboBox")!;
        families.SelectedIndex = 1;
        page.FindControl<TextBox>("SearchTextBox")!.Text = "310";
        releases.SelectedItem = row;
        Dispatcher.UIThread.RunJobs();
        LanguageAppearance.Apply("en-US", new Dictionary<string, string>
        {
            ["Linux_LibraryAllFamilies"] = "All fixture families",
            ["DllRecord_Imported"] = "Fixture imported"
        });
        Dispatcher.UIThread.RunJobs();
        if (!Equals(families.Items[0], "All fixture families") || families.SelectedIndex != 1
            || !ReferenceEquals(releases.SelectedItem, row) || page.FindControl<TextBox>("SearchTextBox")!.Text != "310"
            || !page.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Fixture imported"))
            throw new Exception("Changing language left stale Library labels or lost filtering/selection.");
        LanguageAppearance.Apply("en-US");
        page.Start();
        PumpUntil(() => handler.Started);
        var stopped = page.StopAsync();
        PumpUntil(() => stopped.IsCompleted);
        stopped.GetAwaiter().GetResult();
        if (!handler.Cancelled) throw new Exception("Library shutdown did not cancel its pending metadata request.");
        host.Close();
        Console.WriteLine("PASS Library page cancels pending metadata before disposal (not native download acceptance)");

        void CheckCard(int expectedActions)
        {
            Dispatcher.UIThread.RunJobs();
            var actions = page.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("releaseAction") && button.IsVisible).ToArray();
            if (actions.Length != expectedActions) throw new Exception("Library card did not refresh its visible actions.");
            foreach (var action in actions)
            {
                var card = action.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("card"));
                var point = action.TranslatePoint(default, card)!.Value;
                if (point.X < 0 || point.Y < 0 || point.X + action.Bounds.Width > card.Bounds.Width || point.Y + action.Bounds.Height > card.Bounds.Height)
                    throw new Exception("Compact Library card clips an action.");
            }
        }
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        if (!condition()) throw new Exception("Library lifecycle did not finish.");
    }
    private sealed class PendingResponse : HttpMessageHandler
    {
        public volatile bool Started;
        public volatile bool Cancelled;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Started = true;
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            throw new Exception("Pending response completed unexpectedly.");
        }
    }
}
