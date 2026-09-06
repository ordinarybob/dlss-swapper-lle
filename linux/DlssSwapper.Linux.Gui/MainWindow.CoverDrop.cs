using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private static GameRowViewModel? CoverDropRow(DragEventArgs e) =>
        e.Source is Control control
            ? new[] { control }.Concat(control.GetVisualAncestors().OfType<Control>())
                .Select(item => item.DataContext).OfType<GameRowViewModel>().FirstOrDefault()
            : null;

    private void CoverDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        if (_viewModel.IsBusy || _lifetime.IsCancellationRequested) return;
        if (CoverDropRow(e) is null) return;
        e.Handled = true;
        try
        {
            CustomCoverWorkflow.ValidateSelection(e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).ToArray() ?? []);
            e.DragEffects = DragDropEffects.Copy;
        }
        catch (Exception ex) { _viewModel.StatusText = ex.Message; }
    }

    private async void CoverDrop(object? sender, DragEventArgs e)
    {
        var row = CoverDropRow(e);
        if (_viewModel.IsBusy || _lifetime.IsCancellationRequested) return;
        if (row is null || !TryGetLibrary(out var library)) return;
        e.Handled = true;
        try
        {
            var path = CustomCoverWorkflow.ValidateSelection(e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).ToArray() ?? []);
            await ApplyCustomCoverAsync(row, library, path);
        }
        catch (Exception ex) { _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage34", "Could not set cover: {0}", ex.Message); }
    }
}
