namespace Microsoft.UI.Xaml
{
    public readonly record struct GridLength
    {
        public static GridLength Auto => new();
    }
    public readonly record struct Thickness(double Left, double Top, double Right, double Bottom);
}

namespace Microsoft.UI.Xaml.Controls
{
    public sealed class RowDefinition { public Microsoft.UI.Xaml.GridLength Height { get; set; } }
    public sealed class Button
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int ColumnSpan { get; set; }
        public Microsoft.UI.Xaml.Thickness Margin { get; set; }
    }
    public sealed class Grid
    {
        public List<Button> Children { get; } = [];
        public List<RowDefinition> RowDefinitions { get; } = [];
        public List<object> ColumnDefinitions { get; } = [];
        public static void SetRow(Button button, int value) => button.Row = value;
        public static void SetColumn(Button button, int value) => button.Column = value;
        public static void SetColumnSpan(Button button, int value) => button.ColumnSpan = value;
    }
}
