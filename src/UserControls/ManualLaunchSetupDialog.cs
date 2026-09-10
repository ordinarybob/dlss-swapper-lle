using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

// Keep the bulk action in the native, non-scrolling footer, below its existing buttons.
internal sealed class ManualLaunchSetupDialog(XamlRoot root, Button bulkAction) : EasyContentDialog(root)
{
    Grid? _footer;
    RowDefinition? _bulkRow;

    protected override void OnApplyTemplate()
    {
        _footer?.Children.Remove(bulkAction);
        if (_bulkRow is not null) _footer?.RowDefinitions.Remove(_bulkRow);
        base.OnApplyTemplate();
        _footer = GetTemplateChild("CommandSpace") as Grid;
        if (_footer is null) return;
        if (_footer.RowDefinitions.Count == 0)
            _footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var row = _footer.RowDefinitions.Count;
        _bulkRow = new RowDefinition { Height = GridLength.Auto };
        _footer.RowDefinitions.Add(_bulkRow);
        Grid.SetRow(bulkAction, row);
        Grid.SetColumn(bulkAction, 0);
        Grid.SetColumnSpan(bulkAction, _footer.ColumnDefinitions.Count);
        bulkAction.Margin = new Thickness(0, 12, 0, 0);
        _footer.Children.Add(bulkAction);
    }
}
