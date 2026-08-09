using System;

namespace DLSS_Swapper.Pages;

/// <summary>
/// Divides the grid viewport into equal physical-pixel cells and sizes cards to
/// fill those cells at a fixed 2:3 cover aspect ratio. Equal cells keep every
/// row balanced and avoid a persistent trailing strip at fractional Windows
/// scale factors. A single 1-10 card-size setting chooses an approximate target
/// size; the viewport chooses the responsive column count and remains the sole
/// authority for the final cell width.
/// </summary>
internal static class ResponsiveGameGridLayout
{
    // GridViewItem uses three DIPs of margin and two DIPs of padding per side.
    internal const double HorizontalContainerChrome = 10;
    internal const double VerticalContainerChrome = 10;

    // Cover cards keep a 2:3 width-to-height ratio.
    internal const double CardAspectRatio = 1.5;

    // Six 122-DIP cells reproduce the validated default-window geometry at
    // card size 5. The other size levels target ten through one columns across
    // that same reference width, while arbitrary window sizes remain fluid.
    internal const double ReferenceUsableWidth = 732;

    // Hard floor so extreme preferences or tiny windows cannot produce
    // unusable slivers of cards.
    internal const double MinCardWidth = 44;

    internal static ResponsiveGameGridMetrics Calculate(
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
            Settings.MinGridViewCardSize,
            Settings.MaxGridViewCardSize);
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
        return new ResponsiveGameGridMetrics(
            columnCount,
            cellWidth,
            cardWidth,
            cardHeight,
            cardHeight + VerticalContainerChrome);
    }
}

internal readonly record struct ResponsiveGameGridMetrics(
    int ColumnCount,
    double CellWidth,
    double CardWidth,
    double CardHeight,
    double CellHeight);
