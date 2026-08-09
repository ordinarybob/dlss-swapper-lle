using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class DllLibraryEntryViewModel : ObservableObject
{
    private bool _isCached;

    public DllLibraryEntryViewModel(DllCatalogEntry entry, bool isCached)
    {
        Entry = entry;
        _isCached = isCached;
    }

    public DllCatalogEntry Entry { get; }

    public string Family => DllTypes.Get(Entry.Type).DisplayName;

    public string Version => Entry.Version;

    public string Build => Entry.Md5[..8];

    public string Size => $"{Entry.FileSize / 1024d / 1024d:F1} MiB";

    public bool IsCached
    {
        get => _isCached;
        set
        {
            if (SetProperty(ref _isCached, value))
            {
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public string Status => IsCached ? "Downloaded" : "Available";
}
