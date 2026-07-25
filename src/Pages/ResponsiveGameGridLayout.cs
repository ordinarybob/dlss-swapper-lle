using System;

namespace DLSS_Swapper.Pages;

/// <summary>
/// Divides the grid viewport into equal physical-pixel cells while leaving the
/// user's chosen card size unchanged. Equal cells keep every row balanced and
/// avoid a persistent trailing strip at fractional Windows scale factors.
/// </summary>
internal static class ResponsiveGameGridLayout
{
    // GridViewItem uses three DIPs of margin and two DIPs of padding per side.
    internal const double HorizontalContainerChrome = 10;

    internal static ResponsiveGameGridMetrics Calculate(
        double viewportWidth,
        double horizontalPadding,
        double cardWidth,
        double rasterizationScale)
    {
        var normalizedScale = double.IsFinite(rasterizationScale) && rasterizationScale > 0
            ? rasterizationScale
            : 1d;
        var normalizedCardWidth = double.IsFinite(cardWidth) && cardWidth > 0
            ? cardWidth
            : Settings.MinGridViewItemWidth;
        var normalizedViewport = double.IsFinite(viewportWidth) && viewportWidth > 0
            ? viewportWidth
            : normalizedCardWidth + HorizontalContainerChrome;
        var normalizedPadding = double.IsFinite(horizontalPadding) && horizontalPadding > 0
            ? horizontalPadding
            : 0d;

        var usableDip = Math.Max(1d, normalizedViewport - normalizedPadding);
        var usablePixels = Math.Max(1L, (long)Math.Floor(usableDip * normalizedScale));
        var minimumCellPixels = Math.Max(
            1L,
            (long)Math.Ceiling(
                (normalizedCardWidth + HorizontalContainerChrome) * normalizedScale)
                + 1L);
        var columnCount = Math.Max(1L, usablePixels / minimumCellPixels);

        // Divide the complete physical viewport among the chosen columns instead
        // of flooring every cell independently. WinUI's layout rounding then
        // distributes fractional pixels across the row, so the final column ends
        // at the viewport edge rather than leaving a cumulative right gutter.
        var cellPixels = (double)usablePixels / columnCount;
        return new ResponsiveGameGridMetrics(
            columnCount >= int.MaxValue ? int.MaxValue : (int)columnCount,
            cellPixels / normalizedScale);
    }
}

internal readonly record struct ResponsiveGameGridMetrics(
    int ColumnCount,
    double CellWidth);
