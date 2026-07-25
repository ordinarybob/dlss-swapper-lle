using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// Integer editor with an independent text buffer and compact repeat buttons.
/// Text is committed only on Enter, focus loss, or a spin action.
/// </summary>
public sealed partial class PerformanceNumberEditor : UserControl
{
    bool _isInitialized;
    Button? _nativeDeleteButton;

    public PerformanceNumberEditor()
    {
        InitializeComponent();
        _isInitialized = true;
        SynchronizeTextWithValue();
        UpdateAccessibleNames();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(PerformanceNumberEditor),
        new PropertyMetadata(0d, OnValueChanged));

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(PerformanceNumberEditor),
        new PropertyMetadata(0, OnRangeChanged));

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(PerformanceNumberEditor),
        new PropertyMetadata(int.MaxValue, OnRangeChanged));

    public int SmallChange
    {
        get => (int)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(
        nameof(SmallChange), typeof(int), typeof(PerformanceNumberEditor),
        new PropertyMetadata(1, OnSmallChangeChanged));

    public string AccessibleName
    {
        get => (string)GetValue(AccessibleNameProperty);
        set => SetValue(AccessibleNameProperty, value);
    }

    public static readonly DependencyProperty AccessibleNameProperty = DependencyProperty.Register(
        nameof(AccessibleName), typeof(string), typeof(PerformanceNumberEditor),
        new PropertyMetadata(string.Empty, OnAccessibleNameChanged));

    static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is PerformanceNumberEditor editor && editor._isInitialized)
        {
            editor.SynchronizeTextWithValue();
        }
    }

    static void OnRangeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is PerformanceNumberEditor editor && editor._isInitialized)
        {
            editor.SynchronizeTextWithValue();
        }
    }

    static void OnSmallChangeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is PerformanceNumberEditor editor && editor._isInitialized)
        {
            editor.UpdateAccessibleNames();
        }
    }

    static void OnAccessibleNameChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is PerformanceNumberEditor editor && editor._isInitialized)
        {
            editor.UpdateAccessibleNames();
        }
    }

    void EditorTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        EditorTextBox.ApplyTemplate();
        SuppressNativeDeleteButton();
    }

    void EditorTextBox_LayoutUpdated(object? sender, object e)
    {
        if (_nativeDeleteButton is null
            || VisualTreeHelper.GetParent(_nativeDeleteButton) is null
            || _nativeDeleteButton.Visibility != Visibility.Collapsed)
        {
            SuppressNativeDeleteButton();
        }
    }

    void SuppressNativeDeleteButton()
    {
        _nativeDeleteButton = FindDescendantByName<Button>(EditorTextBox, "DeleteButton");
        if (_nativeDeleteButton is null)
        {
            return;
        }

        _nativeDeleteButton.Visibility = Visibility.Collapsed;
        _nativeDeleteButton.IsEnabled = false;
        _nativeDeleteButton.IsHitTestVisible = false;
        _nativeDeleteButton.IsTabStop = false;
        _nativeDeleteButton.Width = 0;
        _nativeDeleteButton.MinWidth = 0;
        _nativeDeleteButton.MaxWidth = 0;
        _nativeDeleteButton.Margin = new Thickness(0);
        _nativeDeleteButton.Padding = new Thickness(0);
    }

    static T? FindDescendantByName<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; ++index)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T matchingChild && matchingChild.Name == name)
            {
                return matchingChild;
            }

            var descendant = FindDescendantByName<T>(child, name);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    void EditorTextBox_LostFocus(object sender, RoutedEventArgs e) => CommitBufferedText();

    void EditorTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                CommitBufferedText();
                e.Handled = true;
                break;
            case VirtualKey.Up:
                Spin(1);
                e.Handled = true;
                break;
            case VirtualKey.Down:
                Spin(-1);
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                SynchronizeTextWithValue();
                e.Handled = true;
                break;
        }
    }

    void IncreaseButton_Click(object sender, RoutedEventArgs e) => Spin(1);

    void DecreaseButton_Click(object sender, RoutedEventArgs e) => Spin(-1);

    void Spin(int direction)
    {
        var baseline = TryParseBufferedText(out var parsedValue)
            ? Clamp(parsedValue)
            : GetCommittedValue();
        var change = Math.Max(1, SmallChange);
        var candidate = (long)baseline + ((long)direction * change);
        ApplyCommittedValue((int)Math.Clamp(candidate, Minimum, Maximum));
        EditorTextBox.Focus(FocusState.Programmatic);
        EditorTextBox.SelectAll();
    }

    void CommitBufferedText()
    {
        if (TryParseBufferedText(out var parsedValue))
        {
            ApplyCommittedValue(Clamp(parsedValue));
        }
        else
        {
            SynchronizeTextWithValue();
        }
    }

    bool TryParseBufferedText(out int value) => int.TryParse(
        EditorTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);

    void ApplyCommittedValue(int value)
    {
        var committedValue = Clamp(value);
        Value = committedValue;
        SetEditorText(committedValue);
    }

    void SynchronizeTextWithValue() => SetEditorText(GetCommittedValue());

    int GetCommittedValue()
    {
        if (double.IsNaN(Value) || double.IsInfinity(Value))
        {
            return Minimum;
        }

        var roundedValue = Math.Round(Value);
        if (roundedValue <= int.MinValue)
        {
            return Minimum;
        }
        if (roundedValue >= int.MaxValue)
        {
            return Maximum;
        }
        return Clamp((int)roundedValue);
    }

    int Clamp(int value) => Math.Clamp(value, Minimum, Maximum);

    void SetEditorText(int value) => EditorTextBox.Text = value.ToString(CultureInfo.CurrentCulture);

    void UpdateAccessibleNames()
    {
        var name = string.IsNullOrWhiteSpace(AccessibleName) ? "Numeric value" : AccessibleName;
        var change = Math.Max(1, SmallChange);
        AutomationProperties.SetName(EditorTextBox, name);
        AutomationProperties.SetName(IncreaseButton, $"{name}, increase by {change}");
        AutomationProperties.SetName(DecreaseButton, $"{name}, decrease by {change}");
        ToolTipService.SetToolTip(IncreaseButton, $"+{change}");
        ToolTipService.SetToolTip(DecreaseButton, $"−{change}");
    }
}
