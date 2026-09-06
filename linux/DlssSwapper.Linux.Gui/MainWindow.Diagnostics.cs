using Avalonia.Interactivity;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private async void Diagnostics_Click(object? sender, RoutedEventArgs e) =>
        await new DiagnosticsWindow(DiagnosticsReport.Capture(_library, _allRows.Select(row => row.Game).ToArray())).ShowDialog(this);
}
