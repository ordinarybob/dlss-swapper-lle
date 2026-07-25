using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.UserControls;

public partial class BatchDllPickerControlModel : ObservableObject
{
    readonly WeakReference<EasyContentDialog> _parentDialogWeakReference;

    public List<BatchDllRowModel> Rows { get; } = [];

    bool CanUpdateDetectedDllsToLatest => Rows.Count > 0;

    public BatchDllPickerControlModelTranslationProperties TranslationProperties { get; }
        = new BatchDllPickerControlModelTranslationProperties();

    public BatchDllPickerControlModel(
        EasyContentDialog parentDialog,
        IReadOnlyList<Game> games)
    {
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
    }

    void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BatchDllRowModel.SelectedDllOption))
        {
            return;
        }

        if (_parentDialogWeakReference.TryGetTarget(out var dialog))
        {
            dialog.IsPrimaryButtonEnabled = Rows.Any(row => row.HasDllAction);
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
}
