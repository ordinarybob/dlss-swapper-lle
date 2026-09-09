using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var root = new XamlRoot();
var batch = new EasyContentDialog(root);
var download = new EasyContentDialog(root);
var summary = new EasyContentDialog(root);
var batchTask = batch.ShowAsync();
await batch.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
var downloadTask = download.ShowAsync();
var summaryTask = summary.ShowAsync();
Check(!download.Started.Task.IsCompleted && !summary.Started.Task.IsCompleted,
    "Background completion opened over the batch dialog.");
batch.BeginClose();
Check(!download.Started.Task.IsCompleted, "Closed event released the slot before native dismissal completed.");
batch.Complete(ContentDialogResult.Primary);
Check(await batchTask == ContentDialogResult.Primary, "Apply result was lost.");
await download.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
Check(!summary.Started.Task.IsCompleted, "Batch summary collided with the download result.");
download.Hide();
Check(await downloadTask == ContentDialogResult.None, "Notification close result changed.");
await summary.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
summary.Hide();
await summaryTask;

var blocker = new EasyContentDialog(root);
var blockerTask = blocker.ShowAsync();
await blocker.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
var progress = new EasyContentDialog(root);
var progressTask = progress.ShowAsync();
try { _ = progress.ShowAsync(); throw new Exception("Duplicate show was accepted."); }
catch (InvalidOperationException) { }
progress.Hide();
Check(await progressTask.WaitAsync(TimeSpan.FromSeconds(5)) == ContentDialogResult.None
    && !progress.Started.Task.IsCompleted && !blockerTask.IsCompleted,
    "Hiding queued progress either showed it or dismissed the active dialog.");
var next = new EasyContentDialog(root);
var nextTask = next.ShowAsync();
Check(!next.Started.Task.IsCompleted, "Cancelling a waiter released another dialog's slot.");
blocker.Hide();
await blockerTask;
await next.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
next.Hide();
await nextTask;

foreach (var synchronous in new[] { true, false })
{
    var failing = new EasyContentDialog(root) { FailOnShow = synchronous };
    var failed = failing.ShowAsync();
    if (!synchronous)
    {
        await failing.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        failing.Fail();
    }
    try { await failed; throw new Exception("Native failure was swallowed."); }
    catch (IOException) { }
    var retry = new EasyContentDialog(root);
    var retryTask = retry.ShowAsync();
    await retry.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    retry.Hide();
    await retryTask;
}

Check(ContentDialog.Peak == 1, "Native dialogs overlapped.");
Console.WriteLine("PASS: production dialog queue defers download notification and batch summary; waits through dismissal; cancels obsolete progress; preserves Apply result; releases slots after failures (native UI doubled).");

namespace DLSS_Swapper
{
    internal sealed class Settings
    {
        internal static Settings Instance { get; } = new();
        internal int AppTheme => 0;
    }
}

namespace Microsoft.UI.Xaml { public sealed class XamlRoot; }

namespace Microsoft.UI.Xaml.Controls
{
    public enum ContentDialogResult { None, Primary }
    public class ContentDialog
    {
        static int active;
        public static int Peak;
        public XamlRoot? XamlRoot { get; set; }
        public int RequestedTheme { get; set; }
        public bool FailOnShow { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource<ContentDialogResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? Closed;
        public Task<ContentDialogResult> ShowAsync()
        {
            if (FailOnShow) throw new IOException("Injected native ShowAsync failure.");
            var count = Interlocked.Increment(ref active);
            Peak = Math.Max(Peak, count);
            if (count != 1) throw new Exception("Only a single ContentDialog can be open at any time.");
            Started.TrySetResult();
            return completion.Task;
        }
        public void BeginClose() => Closed?.Invoke(this, EventArgs.Empty);
        public void Complete(ContentDialogResult result)
        {
            Interlocked.Decrement(ref active);
            completion.SetResult(result);
        }
        public void Hide() { BeginClose(); Complete(ContentDialogResult.None); }
        public void Fail()
        {
            Interlocked.Decrement(ref active);
            completion.SetException(new IOException("Injected native completion failure."));
        }
    }
}
