using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml.Controls;

internal static class NotesRemovalTests
{
    public static async Task RunAsync()
    {
        void Check(bool value)
        {
            if (!value) throw new Exception("Notes/removal command contract failed.");
        }
        var model = new GameControlModel();
        model.Game.Notes = "saved notes";
        EasyContentDialog.Interact = async dialog =>
        {
            var content = (StackPanel)dialog.Content!;
            var editor = (TextBox)content.Children[0];
            editor.Text = "draft notes";
            Check(await dialog.ClickSaveAsync());
            Check(model.Game.Notes == "saved notes" && editor.Text == "draft notes");
            Check(((TextBlock)content.Children[1]).Visibility == Microsoft.UI.Xaml.Visibility.Visible);
            model.Game.SaveResult = true;
            Check(!await dialog.ClickSaveAsync());
            Check(model.Game.Notes == "draft notes");
            return ContentDialogResult.Primary;
        };
        await model.EditNotesCommand.ExecuteAsync(null);
        EasyContentDialog.Interact = _ => Task.FromResult(ContentDialogResult.Primary);
        GameManager.Instance.Removed = false;
        model = new GameControlModel();
        await model.RemoveCommand.ExecuteAsync(null);
        Check(!GameManager.Instance.Removed && !model.Control.Hidden && model.Errors.Count == 1);
        model.Game.DeleteResult = true;
        await model.RemoveCommand.ExecuteAsync(null);
        Check(GameManager.Instance.Removed && model.Control.Hidden);
        model = new GameControlModel { IsManuallyAdded = false };
        model.Game.IsHidden = null;
        await model.RemoveCommand.ExecuteAsync(null);
        Check(model.Game.IsHidden is null && !model.Control.Hidden);
        model.Game.SaveResult = true;
        await model.RemoveCommand.ExecuteAsync(null);
        Check(model.Game.IsHidden == true && model.Control.Hidden);
        EasyContentDialog.Interact = null;
        Console.WriteLine("Notes/removal commands: failed-save draft retention, retry, failed deletion retention and failed/successful exclusion passed (dialog doubles).");
    }
}
namespace Microsoft.UI.Xaml
{
    public enum TextWrapping { Wrap }
    public enum Visibility { Visible, Collapsed }
}
namespace Microsoft.UI.Xaml.Controls
{
    public sealed class ProgressRing { public bool IsIndeterminate { get; set; } }
    public enum ContentDialogButton { Primary }
    public enum ContentDialogResult { Primary, None }
    public sealed class TextBox
    {
        public double MinHeight { get; set; }
        public Microsoft.UI.Xaml.TextWrapping TextWrapping { get; set; }
        public bool AcceptsReturn { get; set; }
        public string Text { get; set; } = "";
    }
    public sealed class TextBlock
    {
        public string Text { get; set; } = "";
        public Microsoft.UI.Xaml.TextWrapping TextWrapping { get; set; }
        public Microsoft.UI.Xaml.Visibility Visibility { get; set; }
    }
    public sealed class StackPanel
    {
        public double Spacing { get; set; }
        public List<object> Children { get; } = [];
    }
    public sealed class TestClickArgs
    {
        public bool Cancel { get; set; }
        public TaskCompletionSource Completion { get; } = new();
        public TestDeferral GetDeferral() => new(Completion);
    }
    public sealed class TestDeferral(TaskCompletionSource completion)
    {
        public void Complete() => completion.SetResult();
    }
}
namespace DLSS_Swapper.Helpers
{
    public sealed class EasyContentDialog(object root)
    {
        public static Func<EasyContentDialog, Task<ContentDialogResult>>? Interact { get; set; }
        public object Root { get; } = root;
        public string Title { get; set; } = "";
        public string PrimaryButtonText { get; set; } = "";
        public string CloseButtonText { get; set; } = "";
        public ContentDialogButton DefaultButton { get; set; }
        public object? Content { get; set; }
        public Dictionary<string, object> Resources { get; } = [];
        public event EventHandler<TestClickArgs>? PrimaryButtonClick;
        public bool Hidden { get; private set; }
        public void Hide() => Hidden = true;
        public Task<ContentDialogResult> ShowAsync() => Interact!(this);
        public async Task<bool> ClickSaveAsync()
        {
            var args = new TestClickArgs();
            PrimaryButtonClick?.Invoke(this, args);
            await args.Completion.Task;
            return args.Cancel;
        }
    }
}
namespace DLSS_Swapper.UserControls
{
    internal sealed class GameControl(Game game)
    {
        internal static int Opened { get; set; }
        internal Game Game { get; } = game;
        public Task ShowAsync() { Opened++; return Task.CompletedTask; }
    }
    internal sealed class FakeGameControl
    {
        public object XamlRoot { get; } = new();
        public bool Hidden { get; private set; }
        public void Hide() => Hidden = true;
    }
}
namespace DLSS_Swapper.Data
{
    internal sealed class GameManager
    {
        public static GameManager Instance { get; } = new();
        public bool Removed { get; set; }
        public void RemoveGame(Game game) => Removed = true;
    }
}
