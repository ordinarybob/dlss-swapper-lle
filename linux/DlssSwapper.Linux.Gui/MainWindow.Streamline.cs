using Avalonia.Interactivity;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private bool _streamlineMutationActive;

    private async void StreamlineInspect_Click(object? sender, RoutedEventArgs e) => await OpenStreamlineAsync(sender);
    private async void StreamlineUpdate_Click(object? sender, RoutedEventArgs e) => await OpenStreamlineAsync(sender);
    private async void StreamlineRestore_Click(object? sender, RoutedEventArgs e) => await OpenStreamlineAsync(sender);
    private async void StreamlineRecover_Click(object? sender, RoutedEventArgs e) => await OpenStreamlineAsync(sender);

    private async Task OpenStreamlineAsync(object? sender)
    {
        if (_viewModel.IsBusy || !TryGetMenuGame(sender, out var row)) return;
        await RunBusyAsync($"Streamline: {row.Name}", async () =>
        {
            var window = new StreamlineGameWindow(row.RootPath, row.Name, _library);
            _streamlineMutationActive = true;
            try { await window.ShowDialog(this); }
            finally { _streamlineMutationActive = false; }
            foreach (var (updatedRow, scan) in await ScanRowsAsync([row], _catalog ?? DllCatalog.Empty()))
                updatedRow.SetScanResult(scan);
        });
    }
}
