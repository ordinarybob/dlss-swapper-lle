using DlssSwapper.Linux.Cli.Core;
using Avalonia.Media.Imaging;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class GameRowViewModel : ObservableObject
{
    private bool _isSelected;
    private string _scanSummary = "Not scanned";
    private string _scanDetail = "Select this game, then scan it.";
    private Bitmap? _coverImage;
    private string? _artworkPath;
    private bool _isFavorite;
    private bool _isHidden;

    public GameRowViewModel(SelectedGame game)
    {
        Game = game;
    }

    public SelectedGame Game { get; }

    public string Name => Game.Name;

    public string RootPath => Game.RootPath;

    public string Source => Game.SteamAppId is null
        ? "Explicit path"
        : $"Steam app {Game.SteamAppId}";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string ScanSummary
    {
        get => _scanSummary;
        private set => SetProperty(ref _scanSummary, value);
    }

    public string ScanDetail
    {
        get => _scanDetail;
        private set => SetProperty(ref _scanDetail, value);
    }

    public ScanResult? ScanResult { get; private set; }

    public Bitmap? CoverImage
    {
        get => _coverImage;
        private set => SetProperty(ref _coverImage, value);
    }

    public string? ArtworkPath
    {
        get => _artworkPath;
        private set => SetProperty(ref _artworkPath, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteActionText));
                OnPropertyChanged(nameof(FavoriteMarker));
            }
        }
    }

    public bool IsHidden
    {
        get => _isHidden;
        private set
        {
            if (SetProperty(ref _isHidden, value))
            {
                OnPropertyChanged(nameof(HideActionText));
            }
        }
    }

    public string FavoriteActionText => IsFavorite ? "Unfavorite" : "Favorite";

    public string HideActionText => IsHidden ? "Show" : "Hide";

    public string FavoriteMarker => IsFavorite ? "★" : string.Empty;

    public void ApplyPreference(GamePreferenceState preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        IsFavorite = preference.IsFavorite;
        IsHidden = preference.IsHidden;
    }

    public void SetArtwork(string path)
    {
        var next = new Bitmap(path);
        var previous = CoverImage;
        ArtworkPath = path;
        CoverImage = next;
        previous?.Dispose();
    }

    public void ClearArtwork()
    {
        var previous = CoverImage;
        ArtworkPath = null;
        CoverImage = null;
        previous?.Dispose();
    }

    public void SetScanResult(ScanResult scan)
    {
        ScanResult = scan;
        OnPropertyChanged(nameof(ScanResult));

        if (scan.Dlls.Count == 0)
        {
            ScanSummary = "No supported DLLs found";
        }
        else
        {
            ScanSummary = string.Join(
                " • ",
                scan.Dlls
                    .GroupBy(dll => dll.Type)
                    .OrderBy(group => DllTypes.Get(group.Key).DisplayName, StringComparer.Ordinal)
                    .Select(group =>
                    {
                        var versions = string.Join(
                            ", ",
                            group.Select(dll => dll.Version)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(version => version, StringComparer.OrdinalIgnoreCase));
                        var count = group.Count();
                        return $"{DllTypes.Get(group.Key).DisplayName}: {versions} ({count} file{(count == 1 ? string.Empty : "s")})";
                    }));
        }

        ScanDetail = scan.Warnings.Count == 0
            ? $"{scan.Dlls.Count} supported DLL file{(scan.Dlls.Count == 1 ? string.Empty : "s")} scanned."
            : $"{scan.Dlls.Count} supported DLL files; {scan.Warnings.Count} warning{(scan.Warnings.Count == 1 ? string.Empty : "s")}.";
    }
}
