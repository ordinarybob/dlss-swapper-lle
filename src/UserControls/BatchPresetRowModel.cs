using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DLSS_Swapper.UserControls;

public enum BatchPresetKind
{
    Dlss,
    DlssD,
    DlssG,
}

public sealed record BatchPresetOption(
    string DisplayName,
    uint? Value);

internal readonly record struct BatchPresetSelection(
    BatchPresetKind Kind,
    string DisplayName,
    uint Value);

public partial class BatchPresetRowModel : ObservableObject
{
    public BatchPresetKind Kind { get; }
    public string PresetName { get; }
    public List<BatchPresetOption> PresetOptions { get; }

    [ObservableProperty]
    public partial BatchPresetOption? SelectedPresetOption { get; set; }

    public bool HasPresetAction => SelectedPresetOption?.Value is not null;

    public BatchPresetRowModel(
        BatchPresetKind kind,
        string presetName,
        List<BatchPresetOption> presetOptions)
    {
        ArgumentNullException.ThrowIfNull(presetName);
        ArgumentNullException.ThrowIfNull(presetOptions);
        Kind = kind;
        PresetName = presetName;
        PresetOptions = presetOptions;
        SelectedPresetOption = presetOptions[0];
    }
}
