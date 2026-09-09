using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.UserControls;

internal static class GameMetadataTests
{
    public static async Task RunAsync()
    {
        void Check(bool value)
        {
            if (!value) throw new Exception("Game metadata rollback contract failed.");
        }
        var model = new GameControlModel { GameTitle = "Draft title" };
        var game = model.Game;
        game.Title = "Saved title";
        await model.SaveTitleCommand.ExecuteAsync(null);
        Check(game.Title == "Saved title" && model.GameTitle == "Draft title" && model.GameTitleHasChanged);
        Check(game.LastBypassBatch == true && model.Errors.Count == 1);
        game.SaveResult = true;
        await model.SaveTitleCommand.ExecuteAsync(null);
        Check(game.Title == "Draft title" && !model.GameTitleHasChanged);
        foreach (var favourite in new[] { true, false })
        {
            game.IsFavourite = favourite;
            game.SaveResult = false;
            await model.FavouriteCommand.ExecuteAsync(null);
            Check(game.IsFavourite == favourite && game.LastBypassBatch == true);
            game.SaveResult = true;
            await model.FavouriteCommand.ExecuteAsync(null);
            Check(game.IsFavourite == !favourite);
        }
        foreach (bool? hidden in new bool?[] { null, false, true })
        {
            game.IsHidden = hidden;
            game.SaveResult = false;
            await model.ShowHideGameCommand.ExecuteAsync(null);
            Check(game.IsHidden == hidden && game.LastBypassBatch == true);
            game.SaveResult = true;
            await model.ShowHideGameCommand.ExecuteAsync(null);
            Check(game.IsHidden == (hidden is null ? true : !hidden.Value));
        }
        Console.WriteLine("Game metadata commands: title draft preservation, favourite/nullable visibility rollback, immediate saves and successful retries passed.");
    }
}

namespace DLSS_Swapper.UserControls
{
    public partial class GameControlModel : ObservableObject
    {
        internal Game Game { get; } = new("metadata-fixture", () => Task.CompletedTask);
        public string GameTitle { get; set; } = "";
        public bool GameTitleHasChanged => GameTitle != Game.Title;
        public List<string> Errors { get; } = [];
        public bool IsManuallyAdded { get; set; } = true;
        internal FakeGameControl Control { get; } = new();
        void Close() => Control.Hide();
        bool TryGetActionHost(out FakeGameControl host) { host = Control; return true; }
        bool TryGetGameControl(out FakeGameControl control) { control = Control; return true; }
        Task ShowPersistenceErrorAsync(string key) { Errors.Add(key); return Task.CompletedTask; }
    }
}
