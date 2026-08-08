using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Collections;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.Messages;

namespace DLSS_Swapper.Data;

internal partial class GameGroup : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public GameLibrary? GameLibrary { get; init; }
    public AdvancedCollectionView Games { get; init; }
    public bool ShowGridDensityControls => GameLibrary == Interfaces.GameLibrary.Steam;
    public IReadOnlyList<double> GridColumnOptions { get; } = Enumerable.Range(
        Settings.MinGridViewPreferredColumns,
        Settings.MaxGridViewPreferredColumns - Settings.MinGridViewPreferredColumns + 1)
        .Select(value => (double)value)
        .ToArray();
    public IReadOnlyList<double> GridRowOptions { get; } = Enumerable.Range(
        Settings.MinGridViewPreferredRows,
        Settings.MaxGridViewPreferredRows - Settings.MinGridViewPreferredRows + 1)
        .Select(value => (double)value)
        .ToArray();

    [ObservableProperty]
    public partial double GridViewPreferredColumns { get; set; }

    [ObservableProperty]
    public partial double GridViewPreferredRows { get; set; }

    [ObservableProperty]
    public partial double GridDensityHeaderWidth { get; set; } = 364;

    public GameGroup(string name, GameLibrary? gameLibrary, AdvancedCollectionView games)
    {
        Name = name;
        GameLibrary = gameLibrary;
        Games = games;
        GridViewPreferredColumns = Settings.Instance.GridViewPreferredColumns;
        GridViewPreferredRows = Settings.Instance.GridViewPreferredRows;

        WeakReferenceMessenger.Default.Register<GridDensityChangedMessage>(this, (_, _) =>
        {
            GridViewPreferredColumns = Settings.Instance.GridViewPreferredColumns;
            GridViewPreferredRows = Settings.Instance.GridViewPreferredRows;
        });
    }

    partial void OnGridViewPreferredColumnsChanged(double value)
    {
        if (double.IsFinite(value))
        {
            Settings.Instance.GridViewPreferredColumns = (int)Math.Round(value);
        }
    }

    partial void OnGridViewPreferredRowsChanged(double value)
    {
        if (double.IsFinite(value))
        {
            Settings.Instance.GridViewPreferredRows = (int)Math.Round(value);
        }
    }
}
