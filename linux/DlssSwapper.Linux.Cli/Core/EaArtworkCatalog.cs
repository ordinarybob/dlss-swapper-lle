using System.Collections.Frozen;
using System.Text.Json;
using DLSS_Swapper.Data.EAApp;

namespace DlssSwapper.Linux.Cli.Core;

public sealed class EaArtworkCatalog
{
    private readonly FrozenSet<GameSearchResult> _games;
    public string? Warning { get; }
    private EaArtworkCatalog(IEnumerable<GameSearchResult> games, string? warning = null)
    { _games = games.ToFrozenSet(); Warning = warning; }

    public static EaArtworkCatalog Read(Stream source)
    {
        var games = JsonSerializer.Deserialize<List<GameSearchResult>>(source)
            ?? throw new InvalidDataException("EA artwork catalog is empty.");
        return new(games.Where(game => game is not null && !string.IsNullOrWhiteSpace(game.Title)));
    }

    public static EaArtworkCatalog LoadDefault()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ea_app_titles.json");
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new IOException("EA artwork catalog exceeds 8 MiB.");
            using var input = File.OpenRead(path);
            return Read(input);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { return new([], "EA artwork catalog could not be loaded: " + error.Message); }
    }

    public string? FindCover(string title)
    {
        if (_games.Count == 0 || string.IsNullOrWhiteSpace(title)) return null;
        var match = FuzzySharp.Process.ExtractOne(new GameSearchResult { Title = title }, _games, game => game.Title);
        if (match is null || match.Score < 60) return null;
        return new[] { match.Value.PackArtImage?.Path, match.Value.KeyArtImage?.Path, match.Value.LogoImage?.Path }
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
    }
}
