namespace DlssSwapper.Linux.Cli.Core;

/// <summary>
/// Mirrors the Windows checkpoint's viewport-driven 2:3 card layout. A single
/// 1-10 card-size setting chooses an approximate target size, width fill is
/// authoritative, and one physical pixel is retained to prevent the final
/// card from wrapping at fractional display scales.
/// </summary>
internal static class ResponsiveGridLayout
{
    internal const double ContainerChrome = DlssSwapper.Shared.ResponsiveGridLayout.HorizontalContainerChrome;
    internal const double CardAspectRatio = DlssSwapper.Shared.ResponsiveGridLayout.CardAspectRatio;
    internal const double MinimumCardWidth = DlssSwapper.Shared.ResponsiveGridLayout.MinCardWidth;
    internal const double ReferenceUsableWidth = DlssSwapper.Shared.ResponsiveGridLayout.ReferenceUsableWidth;
    internal const int DefaultCardSize = 5;
    internal const int MinimumCardSize = 1;
    internal const int MaximumCardSize = 10;

    internal static ResponsiveGridMetrics Calculate(
        double viewportWidth,
        int cardSize,
        double rasterizationScale)
    {
        var metrics = DlssSwapper.Shared.ResponsiveGridLayout.Calculate(
            viewportWidth, 0, cardSize, rasterizationScale);
        return new ResponsiveGridMetrics(metrics.ColumnCount, metrics.CellWidth, metrics.CardWidth, metrics.CardHeight, metrics.CellHeight);
    }

    internal static int ConvertLegacyColumnsToCardSize(int columns) => columns > 0
        ? Math.Clamp(11 - columns, MinimumCardSize, MaximumCardSize)
        : DefaultCardSize;
}

internal readonly record struct ResponsiveGridMetrics(
    int ColumnCount,
    double CellWidth,
    double CardWidth,
    double CardHeight,
    double CellHeight);
