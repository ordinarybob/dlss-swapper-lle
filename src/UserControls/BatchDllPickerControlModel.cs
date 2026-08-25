using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.DLSS;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.UserControls;

public partial class BatchDllPickerControlModel : ObservableObject
{
    readonly WeakReference<EasyContentDialog> _parentDialogWeakReference;

    public List<BatchDllRowModel> Rows { get; } = [];

    public List<BatchPresetRowModel> PresetRows { get; } = [];

    public bool HasPresetRows => PresetRows.Count > 0;

    bool CanUpdateDetectedDllsToLatest => Rows.Count > 0;

    public BatchDllPickerControlModelTranslationProperties TranslationProperties { get; }
        = new BatchDllPickerControlModelTranslationProperties();

    public BatchDllPickerControlModel(
        EasyContentDialog parentDialog,
        IReadOnlyList<Game> games)
    {
        ArgumentNullException.ThrowIfNull(parentDialog);
        ArgumentNullException.ThrowIfNull(games);
        _parentDialogWeakReference = new WeakReference<EasyContentDialog>(parentDialog);
        parentDialog.IsPrimaryButtonEnabled = false;

        var detectedTypes = games
            .SelectMany(game => game.GameAssets)
            .Select(asset => asset.AssetType)
            .Distinct()
            .OrderBy(type => (int)type);

        foreach (var type in detectedTypes)
        {
            var records = DllUpdateWorkflow.GetEligibleRecords(type);
            if (records.Count == 0)
            {
                continue;
            }

            records.Sort();
            var options = new List<BatchDllOption>
            {
                new BatchDllOption(
                    TranslationProperties.DontChangeText,
                    null),
            };
            options.AddRange(records.Select(record =>
                new BatchDllOption(record.DisplayName, record)));

            var row = new BatchDllRowModel(
                type,
                DLLManager.Instance.GetAssetTypeName(type),
                options);
            row.PropertyChanged += Row_PropertyChanged;
            Rows.Add(row);
        }

        AddPresetRows(games);
    }

    void AddPresetRows(IReadOnlyList<Game> games)
    {
        if (NVAPIHelper.Instance.IsSupported == false)
        {
            return;
        }

        var detectedTypes = games
            .SelectMany(game => game.GameAssets)
            .Select(asset => asset.AssetType)
            .ToHashSet();

        if (detectedTypes.Contains(GameAssetType.DLSS))
        {
            AddPresetRow(
                BatchPresetKind.Dlss,
                TranslationProperties.DlssPresetText,
                NVAPIHelper.Instance.DlssPresetOptions);
        }

        if (detectedTypes.Contains(GameAssetType.DLSS_D))
        {
            AddPresetRow(
                BatchPresetKind.DlssD,
                TranslationProperties.DlssDPresetText,
                NVAPIHelper.Instance.DlssDPresetOptions);
        }

        if (detectedTypes.Contains(GameAssetType.DLSS_G))
        {
            AddPresetRow(
                BatchPresetKind.DlssG,
                TranslationProperties.DlssGPresetText,
                NVAPIHelper.Instance.DlssGPresetOptions);
        }
    }

    void AddPresetRow(
        BatchPresetKind kind,
        string presetName,
        IReadOnlyList<PresetOption> availableOptions)
    {
        var options = new List<BatchPresetOption>
        {
            new(TranslationProperties.DontChangeText, null),
        };
        options.AddRange(availableOptions.Select(option =>
            new BatchPresetOption(option.Name, option.Value)));

        var row = new BatchPresetRowModel(kind, presetName, options);
        row.PropertyChanged += Row_PropertyChanged;
        PresetRows.Add(row);
    }

    void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BatchDllRowModel.SelectedDllOption)
            && e.PropertyName != nameof(BatchPresetRowModel.SelectedPresetOption))
        {
            return;
        }

        if (_parentDialogWeakReference.TryGetTarget(out var dialog))
        {
            dialog.IsPrimaryButtonEnabled =
                Rows.Any(row => row.HasDllAction)
                || PresetRows.Any(row => row.HasPresetAction);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpdateDetectedDllsToLatest))]
    void UpdateDetectedDllsToLatest()
    {
        foreach (var row in Rows)
        {
            var latest = DllUpdateWorkflow.FindLatestRecord(
                row.DllOptions
                    .Where(option => option.Record is not null)
                    .Select(option => option.Record!));
            if (latest is null)
            {
                continue;
            }

            row.SelectedDllOption = row.DllOptions.First(option =>
                ReferenceEquals(option.Record, latest));
        }
    }

    internal List<DllUpdateSelection> PlannedDllActions =>
        Rows
            .Where(row => row.HasDllAction)
            .Select(row => new DllUpdateSelection(
                row.Type,
                row.SelectedDllOption!.Record!))
            .ToList();

    internal List<BatchPresetSelection> PlannedPresetActions =>
        PresetRows
            .Where(row => row.HasPresetAction)
            .Select(row => new BatchPresetSelection(
                row.Kind,
                row.SelectedPresetOption!.DisplayName,
                row.SelectedPresetOption.Value!.Value))
            .ToList();
}
