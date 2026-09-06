using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Data.Streamline;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.DLSS;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.UserControls;

public partial class BatchDllPickerControlModel : ObservableObject
{
    readonly WeakReference<EasyContentDialog> _parentDialogWeakReference;
    readonly IReadOnlyList<Game> _games;
    bool _streamlineLoaded;
    readonly CancellationTokenSource _closed = new();
    internal IReadOnlyList<StreamlineBatchGame> StreamlineGames { get; private set; } = [];
    public ObservableCollection<BatchStreamlineRowModel> StreamlineRows { get; } = [];
    [ObservableProperty] public partial bool IncludeStreamline { get; set; }
    [ObservableProperty] public partial bool IsStreamlineLoading { get; set; }
    [ObservableProperty] public partial string StreamlineStatus { get; set; } = "Updates only components already installed in each game.";

    partial void OnIncludeStreamlineChanged(bool value)
    {
        UpdateDetectedDllsToLatestCommand.NotifyCanExecuteChanged();
        UpdateApplyButton();
        if (value && !_streamlineLoaded && !IsStreamlineLoading) _ = LoadStreamlineAsync();
    }

    async Task LoadStreamlineAsync()
    {
        IsStreamlineLoading = true;
        UpdateApplyButton();
        StreamlineStatus = "Finding installed Streamline components and checking the latest SDK…";
        try
        {
            var discovery = StreamlineBatchUpdateWorkflow.DiscoverAsync(_games);
            var metadata = ReadStreamlineVersionAsync();
            StreamlineGames = await discovery;
            var version = await metadata;
            if (_closed.IsCancellationRequested) return;
            foreach (var name in StreamlineGames.SelectMany(game => game.Paths).Select(Path.GetFileName)
                .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name))
            {
                var row = new BatchStreamlineRowModel(name) { IsSelected = true };
                row.PropertyChanged += Row_PropertyChanged;
                StreamlineRows.Add(row);
            }
            _streamlineLoaded = true;
            StreamlineStatus = $"{version} · {StreamlineGames.Count(game => game.Paths.Count > 0)} games with Streamline." +
                (Settings.Instance.OnlyShowDownloadedDlls ? " Downloaded-only filtering is on: the cached SDK will be used." : string.Empty) +
                (StreamlineGames.Any(game => game.Error is not null) ? " Some game folders could not be inspected." : string.Empty);
        }
        catch (Exception exception) { StreamlineStatus = exception.Message; }
        finally
        {
            IsStreamlineLoading = false;
            UpdateApplyButton();
            UpdateDetectedDllsToLatestCommand.NotifyCanExecuteChanged();
        }
    }

    async Task<string> ReadStreamlineVersionAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { return "Latest SDK: " + (await StreamlineReleaseManager.FetchLatestAsync(timeout.Token)).Tag; }
        catch (Exception) { return "Latest SDK unavailable; Apply will retry"; }
    }

    internal List<string> PlannedStreamlineActions => IncludeStreamline
        ? StreamlineRows.Where(row => row.IsSelected).Select(row => row.FileName).ToList() : [];

    public List<BatchDllRowModel> Rows { get; } = [];

    public List<BatchPresetRowModel> PresetRows { get; } = [];

    public bool HasPresetRows => PresetRows.Count > 0;

    bool CanUpdateDetectedDllsToLatest => Rows.Count > 0 || (IncludeStreamline && StreamlineRows.Count > 0);

    public BatchDllPickerControlModelTranslationProperties TranslationProperties { get; }
        = new BatchDllPickerControlModelTranslationProperties();

    public BatchDllPickerControlModel(
        EasyContentDialog parentDialog,
        IReadOnlyList<Game> games)
    {
        ArgumentNullException.ThrowIfNull(parentDialog);
        ArgumentNullException.ThrowIfNull(games);
        _parentDialogWeakReference = new WeakReference<EasyContentDialog>(parentDialog);
        _games = games;
        parentDialog.Closed += (_, _) => _closed.Cancel();
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
            && e.PropertyName != nameof(BatchPresetRowModel.SelectedPresetOption)
            && e.PropertyName != nameof(BatchStreamlineRowModel.IsSelected))
        {
            return;
        }

        UpdateApplyButton();
    }

    void UpdateApplyButton()
    {
        if (_parentDialogWeakReference.TryGetTarget(out var dialog))
        {
            dialog.IsPrimaryButtonEnabled =
                !(IncludeStreamline && IsStreamlineLoading) && (Rows.Any(row => row.HasDllAction)
                || PresetRows.Any(row => row.HasPresetAction)
                || PlannedStreamlineActions.Count > 0);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpdateDetectedDllsToLatest))]
    void UpdateDetectedDllsToLatest()
    {
        if (IncludeStreamline)
            foreach (var row in StreamlineRows) row.IsSelected = true;
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
