using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class DllLibraryEntryViewModel : ObservableObject
{
    private bool _isCached;
    private bool _isDownloading;
    private string? _transferStatus;
    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (!SetProperty(ref _isDownloading, value)) return;
            OnPropertyChanged(nameof(DownloadAction));
            RefreshActions();
        }
    }
    public string DownloadAction => IsDownloading ? LanguageAppearance.Get("General_Cancel", "Cancel") : LanguageAppearance.Get("General_Download", "Download");
    public bool ShowDownload => !IsCached || IsDownloading;
    public bool ShowExport => IsCached && !IsDownloading;
    public bool ShowRemove => (IsCached || Entry.IsImported) && !IsDownloading;
    public string Details => $"{Family} {Version}\n{Build} · {Size}\n{Status}";
    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(DownloadAction));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Details));
    }
    private void RefreshActions()
    {
        OnPropertyChanged(nameof(ShowDownload));
        OnPropertyChanged(nameof(ShowExport));
        OnPropertyChanged(nameof(ShowRemove));
    }
    public string? TransferStatus
    {
        get => _transferStatus;
        set { if (SetProperty(ref _transferStatus, value)) { OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(Details)); } }
    }

    public DllLibraryEntryViewModel(DllCatalogEntry entry, bool isCached)
    {
        Entry = entry;
        _isCached = isCached;
    }

    public DllCatalogEntry Entry { get; }

    public string Family => DllTypes.Get(Entry.Type).DisplayName;

    public string Version => DllReleaseDisplay.Name(Entry);

    public string Build => Entry.Md5[..8];

    public string Size => LanguageAppearance.Format("Linux_LibrarySize", "{0:F1} MiB", Entry.FileSize / 1024d / 1024d);

    public bool IsCached
    {
        get => _isCached;
        set
        {
            if (SetProperty(ref _isCached, value))
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(Details));
                RefreshActions();
            }
        }
    }

    public string Status => TransferStatus ?? (Entry.IsImported
        ? (IsCached ? LanguageAppearance.Get("DllRecord_Imported", "Imported") : LanguageAppearance.Get("Linux_ReimportNeeded", "Reimport needed"))
        : (IsCached ? LanguageAppearance.Get("Linux_Downloaded", "Downloaded") : LanguageAppearance.Get("Linux_Available", "Available")));
}
