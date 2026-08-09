namespace DlssSwapper.Linux.Cli.Core;

/// <summary>
/// Mirrors the Windows checkpoint's viewport-driven 2:3 card layout. A single
/// 1-10 card-size setting chooses an approximate target size, width fill is
/// authoritative, and one physical pixel is retained to prevent the final
/// card from wrapping at fractional display scales.
/// </summary>
internal static class ResponsiveGridLayout
{
    internal const double ContainerChrome = 10;
    internal const double CardAspectRatio = 1.5;
    internal const double MinimumCardWidth = 44;
    internal const double ReferenceUsableWidth = 732;
    internal const int DefaultCardSize = 5;
    internal const int MinimumCardSize = 1;
    internal const int MaximumCardSize = 10;

    internal static ResponsiveGridMetrics Calculate(
        double viewportWidth,
        int cardSize,
        double rasterizationScale)
    {
        var width = double.IsFinite(viewportWidth) && viewportWidth > 0
            ? viewportWidth
            : MinimumCardWidth + ContainerChrome;
        var scale = double.IsFinite(rasterizationScale) && rasterizationScale > 0
            ? rasterizationScale
            : 1d;
        var usablePixels = Math.Max(1L, (long)Math.Floor(width * scale) - 1L);
        var normalizedCardSize = Math.Clamp(cardSize, MinimumCardSize, MaximumCardSize);
        var referenceColumns = 11 - normalizedCardSize;
        var targetCellWidth = ReferenceUsableWidth / referenceColumns;
        var columnCount = Math.Max(
            1,
            (int)Math.Round(
                width / targetCellWidth,
                MidpointRounding.AwayFromZero));

        var maximumColumns = Math.Max(
            1,
            (int)Math.Floor(width / (MinimumCardWidth + ContainerChrome)));
        columnCount = Math.Clamp(columnCount, 1, maximumColumns);
        var cellWidth = usablePixels / (double)columnCount / scale;
        var cardWidth = Math.Max(MinimumCardWidth, cellWidth - ContainerChrome);
        var cardHeight = cardWidth * CardAspectRatio;
        return new ResponsiveGridMetrics(
            columnCount,
            cellWidth,
            cardWidth,
            cardHeight,
            cardHeight + ContainerChrome);
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
