using Avalonia.Interactivity;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private async void BatchUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy || !TryGetCatalog(out var catalog) || !TryGetSelectedRows(out var rows)) return;
        await RunBusyAsync(LanguageAppearance.Get("Linux_BatchScanning", "Scanning selected games before planning updates…"), async () =>
        {
            // Resolve hashes now, so same-version/different-build selections are not treated as current.
            var scans = await Task.Run(() => rows.Select(row => _scanner.Scan(
                row.Game, catalog)).ToArray());
            var dialog = new BatchUpdateWindow(catalog, scans, downloadedOnly: _library?.State.OnlyShowDownloadedDlls == true, concurrency: _library?.State.BatchSwapConcurrency ?? 15);
            dialog.Applied += dialog.Close;
            _streamlineMutationActive = true;
            try { await dialog.ShowDialog(this); }
            finally { _streamlineMutationActive = false; }
            string? historyError = null;
            try { _library?.RecordOperationHistory(dialog.Results, "Batch update result"); }
            catch (Exception ex) { historyError = ex.Message; }
            foreach (var (row, scan) in await ScanRowsAsync(rows, catalog)) row.SetScanResult(scan);
            _viewModel.StatusText = dialog.Results.Count == 0 ? LanguageAppearance.Get("Linux_BatchClosed", "Batch closed without applying updates.")
                : LanguageAppearance.Format("Linux_BatchFinished", "Batch finished: {0} results, {1} failed or skipped.", dialog.Results.Count, dialog.Results.Count(result => !result.Success))
                    + (historyError is not null ? " " + LanguageAppearance.Format("Linux_OperationHistoryFailed", "Game operation results could not be saved to history: {0}", historyError) : "");
            if (dialog.Results.Count > 0)
                await new OperationReportWindow(LanguageAppearance.Get("GamesPage_Batch_Summary_Title", "Batch update summary"), dialog.Results).ShowDialog(this);
        });
    }
}
