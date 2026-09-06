using Avalonia.Interactivity;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private async void GameDllVersions_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy || !TryGetMenuGame(sender, out var row) || !TryGetCatalog(out var catalog)) return;
        await RunBusyAsync(LanguageAppearance.Format("Linux_InspectDllVersions", "Inspecting DLL versions for {0}…", row.Name), async () =>
        {
            var dialog = new GameDllPickerWindow(row.Game, catalog, downloadedOnly: _library?.State.OnlyShowDownloadedDlls == true);
            _streamlineMutationActive = true;
            try { await dialog.ShowDialog(this); }
            finally { _streamlineMutationActive = false; }
            string? historyError = null;
            foreach (var result in dialog.Results)
            {
                try { _library?.RecordHistory(row.RootPath, "DLL operation", result.Family, detail: result.Message); }
                catch (Exception ex) { historyError = ex.Message; }
            }
            foreach (var (updatedRow, scan) in await ScanRowsAsync([row], catalog)) updatedRow.SetScanResult(scan);
            _viewModel.StatusText = dialog.Results.Count == 0 ? LanguageAppearance.Get("Linux_DllPickerClosed", "DLL picker closed without applying changes.") : OperationReport.Describe(dialog.Results, LanguageAppearance.Current);
            if (historyError is not null) _viewModel.StatusText += "\n" + LanguageAppearance.Format("Linux_HistorySaveFailed", "History could not be saved: {0}", historyError);
        });
    }

    private async void GameRestoreDlls_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy || !TryGetMenuGame(sender, out var row)) return;
        var catalog = _catalog ?? DllCatalog.Empty();
        await RunBusyAsync(LanguageAppearance.Format("Linux_InspectOriginalDlls", "Inspecting original DLLs for {0}…", row.Name), async () =>
        {
            var dialog = new DllRestoreWindow(row.Game);
            _streamlineMutationActive = true;
            try { await dialog.ShowDialog(this); }
            finally { _streamlineMutationActive = false; }
            string? historyError = null;
            foreach (var result in dialog.Results)
            {
                try { _library?.RecordHistory(row.RootPath, "DLL restore result", result.Family, detail: result.Message); }
                catch (Exception ex) { historyError = ex.Message; }
            }
            foreach (var (updatedRow, scan) in await ScanRowsAsync([row], catalog)) updatedRow.SetScanResult(scan);
            _viewModel.StatusText = dialog.Results.Count == 0 ? LanguageAppearance.Get("Linux_RestoreClosed", "Restore closed without changing files.")
                : LanguageAppearance.Format("Linux_RestoreFinished", "Restore finished: {0} restored, {1} failed.", dialog.Results.Count(result => result.Success), dialog.Results.Count(result => !result.Success))
                    + (historyError is null ? "" : " " + LanguageAppearance.Format("Linux_HistorySaveFailed", "History could not be saved: {0}", historyError));
        });
    }
}
