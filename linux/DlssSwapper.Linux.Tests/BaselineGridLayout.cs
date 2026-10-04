using System;

namespace DlssSwapper.Linux.Tests;

/// <summary>Frozen Windows layout used to check shared-layout compatibility.</summary>
internal static class BaselineGridLayout
{
    // GridViewItem uses three DIPs of margin and two DIPs of padding per side.
    internal const double HorizontalContainerChrome = 10;
    internal const double VerticalContainerChrome = 10;

    // Cover cards keep a 2:3 width-to-height ratio.
    internal const double CardAspectRatio = 1.5;

    // Card-size levels 1-10 target ten through one columns at this reference width.
    internal const double ReferenceUsableWidth = 732;

    internal const double MinCardWidth = 44;

    internal static BaselineGridMetrics Calculate(
        double viewportWidth,
        double horizontalPadding,
        int cardSize,
        double rasterizationScale)
    {
        var normalizedScale = double.IsFinite(rasterizationScale) && rasterizationScale > 0
            ? rasterizationScale
            : 1d;
        var normalizedViewport = double.IsFinite(viewportWidth) && viewportWidth > 0
            ? viewportWidth
            : MinCardWidth + HorizontalContainerChrome;
        var normalizedPadding = double.IsFinite(horizontalPadding) && horizontalPadding > 0
            ? horizontalPadding
            : 0d;

        var usableDip = Math.Max(1d, normalizedViewport - normalizedPadding);

        // One physical pixel is held back so a complete row can never exceed the
        // panel's arranged width after WinUI layout rounding at fractional scale
        // factors, which would wrap the final card.
        var usablePixels = Math.Max(1L, (long)Math.Floor(usableDip * normalizedScale) - 1L);

        var normalizedCardSize = Math.Clamp(
            cardSize,
            1,
            10);
        var referenceColumns = 11 - normalizedCardSize;
        var targetCellWidth = ReferenceUsableWidth / referenceColumns;
        var columnCount = Math.Max(
            1,
            (int)Math.Round(
                usableDip / targetCellWidth,
                MidpointRounding.AwayFromZero));

        var maxColumns = Math.Max(1, (int)Math.Floor(usableDip / (MinCardWidth + HorizontalContainerChrome)));
        columnCount = Math.Clamp(columnCount, 1, maxColumns);

        // Divide the complete physical viewport among the chosen columns instead
        // of flooring every cell independently. WinUI's layout rounding then
        // distributes fractional pixels across the row, so the final column ends
        // at the viewport edge rather than leaving a cumulative right gutter.
        var cellWidth = usablePixels / (double)columnCount / normalizedScale;
        var cardWidth = Math.Max(MinCardWidth, cellWidth - HorizontalContainerChrome);
        var cardHeight = cardWidth * CardAspectRatio;
        return new BaselineGridMetrics(
            columnCount,
            cellWidth,
            cardWidth,
            cardHeight,
            cardHeight + VerticalContainerChrome);
    }
}

internal readonly record struct BaselineGridMetrics(
    int ColumnCount,
    double CellWidth,
    double CardWidth,
    double CardHeight,
    double CellHeight);
