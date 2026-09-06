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
    internal const double HorizontalContainerChrome = DlssSwapper.Shared.ResponsiveGridLayout.HorizontalContainerChrome;
    internal const double VerticalContainerChrome = DlssSwapper.Shared.ResponsiveGridLayout.VerticalContainerChrome;

    // Cover cards keep a 2:3 width-to-height ratio.
    internal const double CardAspectRatio = DlssSwapper.Shared.ResponsiveGridLayout.CardAspectRatio;

    // Six 122-DIP cells reproduce the validated default-window geometry at
    // card size 5. The other size levels target ten through one columns across
    // that same reference width, while arbitrary window sizes remain fluid.
    internal const double ReferenceUsableWidth = DlssSwapper.Shared.ResponsiveGridLayout.ReferenceUsableWidth;

    // Hard floor so extreme preferences or tiny windows cannot produce
    // unusable slivers of cards.
    internal const double MinCardWidth = DlssSwapper.Shared.ResponsiveGridLayout.MinCardWidth;

    internal static ResponsiveGameGridMetrics Calculate(
        double viewportWidth,
        double horizontalPadding,
        int cardSize,
        double rasterizationScale)
    {
        var metrics = DlssSwapper.Shared.ResponsiveGridLayout.Calculate(
            viewportWidth, horizontalPadding, cardSize, rasterizationScale);
        return new ResponsiveGameGridMetrics(metrics.ColumnCount, metrics.CellWidth, metrics.CardWidth, metrics.CardHeight, metrics.CellHeight);
    }
}

internal readonly record struct ResponsiveGameGridMetrics(
    int ColumnCount,
    double CellWidth,
    double CardWidth,
    double CardHeight,
    double CellHeight);
