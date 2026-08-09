namespace DlssSwapper.Linux.Cli.Core;

/// <summary>
/// Mirrors the Windows checkpoint's viewport-driven 2:3 card layout. Column
/// and row preferences are density floors, width fill is authoritative, and a
/// single physical pixel is retained to prevent the final card from wrapping
/// at fractional display scales.
/// </summary>
internal static class ResponsiveGridLayout
{
    internal const double ContainerChrome = 10;
    internal const double CardAspectRatio = 1.5;
    internal const double MinimumCardWidth = 44;

    internal static ResponsiveGridMetrics Calculate(
        double viewportWidth,
        double viewportHeight,
        int preferredColumns,
        int preferredRows,
        double rasterizationScale)
    {
        var width = double.IsFinite(viewportWidth) && viewportWidth > 0
            ? viewportWidth
            : MinimumCardWidth + ContainerChrome;
        var height = double.IsFinite(viewportHeight) && viewportHeight > 0
            ? viewportHeight
            : 0;
        var scale = double.IsFinite(rasterizationScale) && rasterizationScale > 0
            ? rasterizationScale
            : 1d;
        var usablePixels = Math.Max(1L, (long)Math.Floor(width * scale) - 1L);
        var columnCount = Math.Max(1, preferredColumns);

        if (height > 0 && preferredRows > 0)
        {
            var cardHeightLimit = (height / preferredRows) - ContainerChrome;
            if (cardHeightLimit > MinimumCardWidth * CardAspectRatio)
            {
                var cellWidthLimit = (cardHeightLimit / CardAspectRatio)
                    + ContainerChrome;
                var columnsForRows = (int)Math.Ceiling(width / cellWidthLimit);
                columnCount = Math.Max(columnCount, columnsForRows);
            }
        }

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
}

internal readonly record struct ResponsiveGridMetrics(
    int ColumnCount,
    double CellWidth,
    double CardWidth,
    double CardHeight,
    double CellHeight);
