using DlssSwapper.Linux.Cli.Core;
using Avalonia.Media.Imaging;

namespace DlssSwapper.Linux.Gui.ViewModels;

public sealed class GameRowViewModel : ObservableObject
{
    private bool _isSelected;
    private string _scanSummary = LanguageAppearance.Get("Linux_NotScanned", "Not scanned");
    private string _scanDetail = LanguageAppearance.Get("Linux_ScanHint", "Select this game, then scan it.");
    private bool _hasCustomCover;
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

    public SelectedGame Game { get; private set; }

    public void SetTitle(string title)
    {
        Game = Game with { Name = title };
        if (ScanResult is not null) ScanResult = ScanResult with { Game = Game };
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ScanResult));
    }

    public string Name => Game.Name;
    public string CustomCoverActionText => _hasCustomCover
        ? LanguageAppearance.Get("Linux_RemoveCover", "Remove custom cover") : LanguageAppearance.Get("Linux_AddCover", "Add custom cover");

    public string RootPath => Game.RootPath;

    public string Source => Game.ProviderIdentity is { } identity ? $"{identity.Provider} · {identity.Id}" : Game.SteamAppId is null
        ? LanguageAppearance.Get("Linux_ExplicitPath", "Explicit path")
        : LanguageAppearance.Format("Linux_SteamApp", "Steam app {0}", Game.SteamAppId);

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

    public string FavoriteActionText => IsFavorite ? LanguageAppearance.Get("GamePage_Unfavorite", "Unfavorite") : LanguageAppearance.Get("GamePage_Favorite", "Favorite");

    public string HideActionText => IsHidden ? LanguageAppearance.Get("GamePage_Show", "Show") : LanguageAppearance.Get("GamePage_Hide", "Hide");

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(FavoriteActionText));
        OnPropertyChanged(nameof(HideActionText));
        OnPropertyChanged(nameof(CustomCoverActionText));
        OnPropertyChanged(nameof(Source));
    }

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
        _hasCustomCover = preference.CustomArtworkPath is not null;
        OnPropertyChanged(nameof(CustomCoverActionText));
    }

    public void SetArtwork(string path, Action? beforeApply = null)
    {
        var next = new Bitmap(path);
        try { beforeApply?.Invoke(); }
        catch { next.Dispose(); throw; }
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
        ArgumentNullException.ThrowIfNull(scan);
        ScanResult = scan;
        OnPropertyChanged(nameof(ScanResult));

        var primaryDll = scan.Dlls
            .Where(dll => dll.Type == DllType.Dlss)
            .OrderByDescending(dll => dll.Version, DlssSwapper.Shared.VersionTextComparer.Instance)
            .FirstOrDefault();
        CardDllLabel = "DLSS";
        CardDllVersion = FormatCardVersion(primaryDll?.Version);
        var problem = scan.CachedAtUtc is null ? ScanPresentation.Problem(scan, Directory.Exists(scan.Game.RootPath), LanguageAppearance.Current) : null;

        if (scan.Dlls.Count == 0)
        {
            ScanSummary = problem ?? LanguageAppearance.Get("Linux_NoDlls", "No supported DLLs found");
            if (problem is null && scan.StreamlineFiles.Count > 0)
            {
                ScanSummary = LanguageAppearance.Format("Linux_StreamlineFound", "{0} Streamline components found", scan.StreamlineFiles.Count);
                CardDllLabel = "Streamline"; CardDllVersion = LanguageAppearance.Format("Linux_ComponentCount", "{0} components", scan.StreamlineFiles.Count);
            }
            if (problem is not null) { CardDllLabel = LanguageAppearance.Get("Linux_GuiRemainingScan", "Scan"); CardDllVersion = problem; }
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
                        return count == 1
                            ? LanguageAppearance.Format("Linux_ScanOneFile", "{0}: {1} ({2} file)", DllTypes.Get(group.Key).DisplayName, versions, count)
                            : LanguageAppearance.Format("Linux_ScanFiles", "{0}: {1} ({2} files)", DllTypes.Get(group.Key).DisplayName, versions, count);
                    }));
        }

        ScanDetail = ScanPresentation.Detail(scan, LanguageAppearance.Current) + (scan.StreamlineFiles.Count == 0 ? "" : "\n" + LanguageAppearance.Get("Linux_StreamlineList", "Streamline components:") + "\n" + string.Join("\n", scan.StreamlineFiles));
        if (scan.CachedAtUtc is { } observed)
        {
            ScanSummary = LanguageAppearance.Format("Linux_LastKnown", "Last known — {0}", ScanSummary);
            CardDllVersion = LanguageAppearance.Format("Linux_LastKnownVersion", "{0} (last known)", CardDllVersion);
            ScanDetail = LanguageAppearance.Format("Linux_LastChecked", "Last checked {0:g}; not verified this session.\n{1}", observed.ToLocalTime(), ScanDetail);
        }
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
