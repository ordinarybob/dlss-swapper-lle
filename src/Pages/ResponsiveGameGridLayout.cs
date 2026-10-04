using System;

namespace DLSS_Swapper.Pages;

/// <summary>Windows adapter for the shared card-grid layout.</summary>
internal static class ResponsiveGameGridLayout
{
    internal const double HorizontalContainerChrome = DlssSwapper.Shared.ResponsiveGridLayout.HorizontalContainerChrome;
    internal const double VerticalContainerChrome = DlssSwapper.Shared.ResponsiveGridLayout.VerticalContainerChrome;

    internal const double CardAspectRatio = DlssSwapper.Shared.ResponsiveGridLayout.CardAspectRatio;

    internal const double ReferenceUsableWidth = DlssSwapper.Shared.ResponsiveGridLayout.ReferenceUsableWidth;

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
