using System.Text.Json;
using System.Text.Json.Serialization;

namespace DlssSwapper.Linux.Cli.Core;

public sealed class LibrarySelectionEntry
{
    public string Id { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public static class LibrarySelection
{
    public static readonly IReadOnlyList<string> DefaultOrder = new[]
        { "Steam", "GOG", "Epic Games Store", "Ubisoft Connect", "Xbox App", "Battle.net", "EA App", "Manually Added" };

    public static IReadOnlyList<LibrarySelectionEntry> Read(LinuxLibraryState state)
    {
        var entries = state.LibrarySelection ?? throw new InvalidDataException("Library selection is invalid.");
        if (entries.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.Id))
            || entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            throw new InvalidDataException("Library selection contains invalid or duplicate identities.");
        // Retain unknown identities and extension fields when newer saved state is edited.
        return entries.Concat(DefaultOrder.Where(id => !entries.Any(entry => entry.Id == id))
            .Select(id => new LibrarySelectionEntry { Id = id })).ToArray();
    }

    public static bool Enabled(LinuxLibraryState state, string id) => Read(state).FirstOrDefault(entry => entry.Id == id)?.IsEnabled ?? true;
    public static bool Includes(LinuxLibraryState state, SelectedGame game) => Enabled(state,
        GameViewPolicy.LibraryName(game, state.ManualGames.Any(manual => PathComparers.FileSystemPath.Equals(manual.RootPath, game.RootPath))));
}
