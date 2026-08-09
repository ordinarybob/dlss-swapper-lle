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
    private double _cardWidth = 108;
    private double _cardHeight = 170;
    private string _cardDllLabel = "DLSS";
    private string _cardDllVersion = "N/A";

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

    public string CardDllLabel
    {
        get => _cardDllLabel;
        private set => SetProperty(ref _cardDllLabel, value);
    }

    public string CardDllVersion
    {
        get => _cardDllVersion;
        private set => SetProperty(ref _cardDllVersion, value);
    }

    public double CardWidth
    {
        get => _cardWidth;
        private set => SetProperty(ref _cardWidth, value);
    }

    public double CardHeight
    {
        get => _cardHeight;
        private set => SetProperty(ref _cardHeight, value);
    }

    public void SetCardSize(double width, double height)
    {
        CardWidth = width;
        CardHeight = height;
    }

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

        var primaryDll = scan.Dlls
            .Where(dll => dll.Type == DllType.Dlss)
            .OrderByDescending(dll => dll.Version, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        CardDllLabel = "DLSS";
        CardDllVersion = FormatCardVersion(primaryDll?.Version);

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

    private static string FormatCardVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "N/A";
        }

        var normalized = version.Trim();
        if (normalized.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith('v'))
        {
            return normalized;
        }

        return $"v{normalized}";
    }
}
