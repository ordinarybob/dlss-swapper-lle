using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Cli.Platform;
using DlssSwapper.Shared;
using System.Text.Json;

namespace DlssSwapper.Linux.Tests;

internal static class LibrarySelectionTests
{
    public static Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "library-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(root, "state")));
            Check(LibrarySelection.Read(library.State).Select(entry => entry.Id).SequenceEqual(LibrarySelection.DefaultOrder), "Default order differs");
            Check(LibrarySelection.Read(library.State).All(entry => entry.IsEnabled), "Existing library disabled by default");
            var gameRoot = Path.Combine(root, "game"); Directory.CreateDirectory(gameRoot);
            library.AddManualGames([gameRoot]);
            library.UpdateGamePreference(gameRoot, preference => { preference.Notes = "Keep me"; preference.IsFavorite = true; });
            library.UpdateState(state => state.LibrarySelection = [new() { Id = "Manually Added", IsEnabled = false }, new() { Id = "Steam" }]);
            var game = new SelectedGame("Game", gameRoot, null);
            Check(!LibrarySelection.Includes(library.State, game), "Disabled manual game included");
            var reloaded = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
            Check(reloaded.State.ManualGames.Count == 1 && reloaded.FindGamePreference(gameRoot)!.Notes == "Keep me", "Disable erased data");
            Check(LibrarySelection.Read(reloaded.State)[0].Id == "Manually Added" && !LibrarySelection.Includes(reloaded.State, game), "Selection did not survive reload");
            var groups = GameGrouping.Build(new[] { "Steam", "Manually Added", "GOG" }, item => item == "Steam", item => item, true,
                LibrarySelection.Read(reloaded.State).Select(entry => entry.Id).ToArray());
            Check(groups.Select(group => group.Name).SequenceEqual(new[] { "Favourites", "Manually Added", "Steam", "GOG" }), "Saved group order or favourites changed");
            using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var rejected = false;
                try { library.UpdateState(state => state.LibrarySelection[0].IsEnabled = true); }
                catch (IOException) { rejected = true; }
                Check(rejected && !LibrarySelection.Includes(library.State, game), "Failed write changed selector");
            }
            library.UpdateState(state => state.LibrarySelection[0].IsEnabled = true);
            Check(LibrarySelection.Includes(library.State, game), "Re-enable did not restore membership");
            library.UpdateState(state =>
            {
                state.LibrarySelection = LibrarySelection.Read(state).Select(entry => new LibrarySelectionEntry { Id = entry.Id, IsEnabled = false }).ToList();
            });
            // Feed invalid metadata directly to the readers; saved-state normalization removes it.
            var discoveryState = new LinuxLibraryState { LibrarySelection = library.State.LibrarySelection, ProviderWinePrefixes = ["relative-prefix"] };
            var disabled = ProviderDiscovery.Discover(discoveryState, default, includeDefaults: false, respectLibrarySelection: true);
            Check(disabled.Games.Count == 0 && disabled.Warnings.Count == 0 && disabled.Sources.Count == 0, "Disabled provider reader ran");
            Check(ProviderDiscovery.Discover(discoveryState, default, includeDefaults: false).Warnings.Count > 0, "Explicit CLI discovery behavior changed");
            VerifySavedMetadata(library);
            VerifyIndependentProviders(root, gameRoot);
        }
        finally { Directory.Delete(root, true); }
        return Task.CompletedTask;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private static void VerifySavedMetadata(PersistentLibrary library)
    {
        library.UpdateState(state => state.LibrarySelection.Insert(0, new LibrarySelectionEntry
        {
            Id = "Future library", IsEnabled = false,
            AdditionalData = new() { ["futureSettings"] = JsonSerializer.SerializeToElement(new { value = "retain", options = new[] { 1, 2 } }) }
        }));
        library.UpdateState(state =>
        {
            var entries = LibrarySelection.Read(state).ToList();
            var steam = entries.Single(entry => entry.Id == "Steam");
            entries.Remove(steam); entries.Insert(0, steam); steam.IsEnabled = true;
            state.LibrarySelection = entries;
        });
        var loaded = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
        var unknown = loaded.State.LibrarySelection.Single(entry => entry.Id == "Future library");
        Check(!unknown.IsEnabled && unknown.AdditionalData!["futureSettings"].GetProperty("value").GetString() == "retain"
            && unknown.AdditionalData["futureSettings"].GetProperty("options").GetArrayLength() == 2, "Unknown selector data lost during reorder/save/reload");
        var path = Path.Combine(library.StateDirectory, "state.json");
        var before = File.ReadAllText(path);
        foreach (var invalid in new[] { "duplicate", "null", "empty" })
        {
            var rejected = false;
            try
            {
                library.UpdateState(state =>
                {
                    if (invalid == "null") state.LibrarySelection = null!;
                    else state.LibrarySelection.Add(new LibrarySelectionEntry { Id = invalid == "duplicate" ? "Steam" : "" });
                });
            }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllText(path) == before && LibrarySelection.Enabled(library.State, "Steam"), "Invalid selector state was saved or rollback failed");
        }
    }

    private static void VerifyIndependentProviders(string root, string gameRoot)
    {
        var heroic = Path.Combine(root, "heroic");
        var legendary = Path.Combine(heroic, "legendaryConfig", "legendary");
        Directory.CreateDirectory(legendary);
        Directory.CreateDirectory(Path.Combine(heroic, "gog_store"));
        File.WriteAllText(Path.Combine(legendary, "installed.json"), JsonSerializer.Serialize(new
        { epic = new { app_name = "epic", title = "Epic fixture", install_path = gameRoot } }));
        File.WriteAllText(Path.Combine(heroic, "gog_store", "installed.json"), JsonSerializer.Serialize(new
        { installed = new[] { new { appName = "gog", install_path = gameRoot, platform = "linux" } } }));
        var state = new LinuxLibraryState { HeroicConfigDirectories = [heroic], LibrarySelection = [new() { Id = "Epic Games Store", IsEnabled = false }] };
        var result = ProviderDiscovery.Discover(state, default, includeDefaults: false, respectLibrarySelection: true);
        Check(result.Games.Count == 1 && result.Games[0].Identity.Provider == GameProvider.Gog, "Disabling Epic also suppressed Heroic GOG");
        state.LibrarySelection = [new() { Id = "GOG", IsEnabled = false }];
        result = ProviderDiscovery.Discover(state, default, includeDefaults: false, respectLibrarySelection: true);
        Check(result.Games.Count == 1 && result.Games[0].Identity.Provider == GameProvider.Epic, "Disabling GOG also suppressed Heroic Epic");
    }
}
