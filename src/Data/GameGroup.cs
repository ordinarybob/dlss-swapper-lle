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
    public IReadOnlyList<double> GridCardSizeOptions { get; } = Enumerable.Range(
        Settings.MinGridViewCardSize,
        Settings.MaxGridViewCardSize - Settings.MinGridViewCardSize + 1)
        .Select(value => (double)value)
        .ToArray();

    [ObservableProperty]
    public partial double GridViewCardSize { get; set; }

    [ObservableProperty]
    public partial double GridDensityHeaderWidth { get; set; } = 364;

    public GameGroup(string name, GameLibrary? gameLibrary, AdvancedCollectionView games)
    {
        Name = name;
        GameLibrary = gameLibrary;
        Games = games;
        GridViewCardSize = Settings.Instance.GridViewCardSize;

        WeakReferenceMessenger.Default.Register<GridDensityChangedMessage>(this, (_, _) =>
        {
            GridViewCardSize = Settings.Instance.GridViewCardSize;
        });
    }

    partial void OnGridViewCardSizeChanged(double value)
    {
        if (double.IsFinite(value))
        {
            Settings.Instance.GridViewCardSize = (int)Math.Round(value);
        }
    }
}
