using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class SettingsPage
{
    public bool LibrarySelectionChanged { get; private set; }
    private string? _draggedLibrary;

    private void LoadLibrarySelection()
    {
        if (_library is null) return;
        var panel = FindRequired<StackPanel>("LibrarySelectionPanel");
        panel.Children.Clear();
        var entries = LibrarySelection.Read(_library.State).ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var position = index;
            var row = new Grid { ColumnDefinitions = new("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
            var handle = new TextBlock { Text = "☰", VerticalAlignment = VerticalAlignment.Center, Padding = new Avalonia.Thickness(6),
                [!ToolTip.TipProperty] = new DynamicResourceExtension("Linux_LibraryReorderHint") };
            var enabled = new CheckBox { Content = entry.Id, IsChecked = entry.IsEnabled };
            // Keep unsupported/unknown saved identities without claiming a working adapter.
            enabled.IsEnabled = entry.Id != "Xbox App" && LibrarySelection.DefaultOrder.Contains(entry.Id);
            if (!enabled.IsEnabled) enabled[!ToolTip.TipProperty] = new DynamicResourceExtension("Linux_LibraryUnsupportedHint");
            var up = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("Linux_SettingsWindow_Libraries_178"), IsEnabled = index > 0, VerticalAlignment = VerticalAlignment.Center };
            var down = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("Linux_SettingsWindow_Libraries_177"), IsEnabled = index < entries.Length - 1, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(handle); Grid.SetColumn(enabled, 1); row.Children.Add(enabled); Grid.SetColumn(up, 2); row.Children.Add(up); Grid.SetColumn(down, 3); row.Children.Add(down);
            panel.Children.Add(row);
            enabled.IsCheckedChanged += (_, _) => ChangeLibrarySelection(list => list.Single(item => item.Id == entry.Id).IsEnabled = enabled.IsChecked == true);
            up.Click += (_, _) => ChangeLibrarySelection(list => { var value = list[position]; list.RemoveAt(position); list.Insert(position - 1, value); });
            down.Click += (_, _) => ChangeLibrarySelection(list => { var value = list[position]; list.RemoveAt(position); list.Insert(position + 1, value); });
            handle.PointerPressed += async (_, e) =>
            {
                if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
                _draggedLibrary = entry.Id;
                try
                {
                    var data = new DataTransfer(); data.Add(DataTransferItem.CreateText(entry.Id));
                    await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
                }
                catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _validation.Text = LanguageAppearance.Format("Linux_SettingsWindow_Libraries_176", "Could not reorder library: {0}", ex.Message); }
                finally { _draggedLibrary = null; }
            };
            DragDrop.SetAllowDrop(row, true);
            row.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.Handled = true; e.DragEffects = _draggedLibrary is not null && _draggedLibrary != entry.Id ? DragDropEffects.Move : DragDropEffects.None; });
            row.AddHandler(DragDrop.DropEvent, (_, e) =>
            {
                e.Handled = true;
                if (_draggedLibrary is not { } id || id == entry.Id) return;
                ChangeLibrarySelection(list =>
                {
                    var moving = list.Single(item => item.Id == id);
                    var target = list.FindIndex(item => item.Id == entry.Id);
                    list.Remove(moving); list.Insert(target, moving);
                });
            });
        }
    }

    private void ChangeLibrarySelection(Action<List<LibrarySelectionEntry>> change)
    {
        if (_library is null) return;
        try
        {
            _library.UpdateState(state =>
            {
                var entries = LibrarySelection.Read(state).ToList();
                change(entries);
                state.LibrarySelection = entries;
            });
            LibrarySelectionChanged = true;
            _validation.Text = LanguageAppearance.Get("Linux_SettingsWindow_Libraries_175", "Library selection saved. Disabling a library does not remove its saved data.");
        }
        catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _validation.Text = LanguageAppearance.Format("Linux_SettingsWindow_Libraries_174", "Could not save library selection: {0}", ex.Message); }
        LoadLibrarySelection();
    }
}
