using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace DlssSwapper.Linux.Gui;

public sealed partial class SettingsPage
{
    internal void EnableImmediateSettings()
    {
        if (_liveSettings) return;
        _liveSettings = true;
        FindRequired<StackPanel>("SettingsSaveActions").IsVisible = false;
        foreach (var control in this.GetLogicalDescendants().OfType<Control>())
        {
            if (control is NumericUpDown number)
                number.ValueChanged += (_, _) => SaveLive();
            else if (control is CheckBox box && box.Name is not null)
                box.IsCheckedChanged += (_, _) => SaveLive();
            else if (control is ComboBox combo)
                combo.SelectionChanged += (_, _) => SaveLive();
            else if (control is TextBox { IsReadOnly: false } text)
                text.LostFocus += (_, _) => SaveLive();
        }
    }

    private void SaveLive()
    {
        if (!_liveSettings || _savingLive || !IsEffectivelyVisible) return;
        // Save a profile change and its reset controls in one transaction.
        _savingLive = true;
        Dispatcher.UIThread.Post(() =>
        {
            try { if (TopLevel.GetTopLevel(this) is { IsVisible: true }) Save_Click(null, new Avalonia.Interactivity.RoutedEventArgs()); }
            finally { _savingLive = false; }
        });
    }
}
