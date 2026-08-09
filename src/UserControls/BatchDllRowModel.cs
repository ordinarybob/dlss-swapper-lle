using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.UserControls;

public sealed record BatchDllOption(
    string DisplayName,
    DLLRecord? Record);

public partial class BatchDllRowModel : ObservableObject
{
    public GameAssetType Type { get; }
    public string TypeName { get; }
    public List<BatchDllOption> DllOptions { get; }

    [ObservableProperty]
    public partial BatchDllOption? SelectedDllOption { get; set; }

    public bool HasDllAction => SelectedDllOption?.Record is not null;

    public BatchDllRowModel(
        GameAssetType type,
        string typeName,
        List<BatchDllOption> dllOptions)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(dllOptions);
        Type = type;
        TypeName = typeName;
        DllOptions = dllOptions;
        SelectedDllOption = dllOptions[0];
    }
}
