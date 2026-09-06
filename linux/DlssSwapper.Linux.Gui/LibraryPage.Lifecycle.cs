using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class LibraryPage
{
    public event Action<DllCatalogEntry>? UseRequested;
    public event Action<DllCatalog>? CatalogChanged;
    private Window DialogOwner => TopLevel.GetTopLevel(this) as Window
        ?? throw new InvalidOperationException("Library must be attached to a window.");
    private IStorageProvider StorageProvider => DialogOwner.StorageProvider;
    private readonly TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _started;
    private DllCatalog? _pendingCatalog;
    public void Start()
    {
        if (_started) return;
        _started = true;
        _versionCheck = RefreshSdkVersionAsync();
    }

    public async Task StopAsync()
    {
        _closeRequested = true;
        LanguageAppearance.Changed -= RefreshLanguage;
        _lifetime.Cancel();
        FinishStopping();
        await _idle.Task;
        await _versionCheck;
        _lifetime.Dispose(); _cache.Dispose(); _sdkHttp.Dispose();
    }

    private void FinishStopping()
    {
        if (_closeRequested && !_isBusy && !HasRecordDownloads) _idle.TrySetResult();
    }

    private void RefreshLanguage()
    {
        if (_familyComboBox.ItemsSource is not string[] families || families.Length == 0) return;
        var index = _familyComboBox.SelectedIndex;
        var selected = _entryListBox.SelectedItem;
        var labels = families.ToArray();
        labels[0] = LanguageAppearance.Get("Linux_LibraryAllFamilies", "All families");
        _familyComboBox.ItemsSource = labels;
        _familyComboBox.SelectedIndex = index;
        foreach (var row in _allEntries) row.RefreshLanguage();
        ApplyFilter();
        _entryListBox.SelectedItem = selected;
    }

    public void UpdateCatalog(DllCatalog catalog)
    {
        _pendingCatalog = catalog;
        ApplyPendingCatalog();
    }

    private void ApplyPendingCatalog()
    {
        if (_isBusy || HasRecordDownloads || _pendingCatalog is not { } catalog) return;
        _pendingCatalog = null;
        var selected = (_entryListBox.SelectedItem as ViewModels.DllLibraryEntryViewModel)?.Entry;
        _catalog = catalog;
        var prior = _allEntries.ToDictionary(row => (row.Entry.Type, row.Entry.Md5));
        _allEntries = catalog.GetEntries().Select(entry => prior.TryGetValue((entry.Type, entry.Md5), out var row)
            && row.Entry == entry ? row : new ViewModels.DllLibraryEntryViewModel(entry, _cache.IsCached(entry))).ToArray();
        ApplyFilter();
        if (selected is not null) _entryListBox.SelectedItem = _allEntries.FirstOrDefault(row => row.Entry.Type == selected.Type && row.Entry.Md5 == selected.Md5);
    }
}
