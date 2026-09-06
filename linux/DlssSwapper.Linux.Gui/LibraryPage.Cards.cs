using Avalonia.Controls;
using Avalonia.Interactivity;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public sealed partial class LibraryPage
{
    private bool SelectCard(object? sender)
    {
        if (_isBusy || sender is not Control { DataContext: DllLibraryEntryViewModel row }) return false;
        _entryListBox.SelectedItem = row;
        return true;
    }

    private void CardInfo_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectCard(sender)) Info_Click(sender, e);
    }

    private void CardDownload_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectCard(sender)) Download_Click(sender, e);
    }

    private void CardRemove_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectCard(sender)) Remove_Click(sender, e);
    }

    private void CardExport_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectCard(sender)) Export_Click(sender, e);
    }
}
