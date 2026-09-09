using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using DLSS_Swapper.UserControls;

internal static class DllApplyTests
{
    public static async Task RunAsync()
    {
        void Check(bool value)
        {
            if (!value) throw new Exception("DLL Apply command contract failed.");
        }
        var model = new DLLPickerControlModel();
        var selected = new DLLRecord();
        var download = new TaskCompletionSource<(bool Success, string Message, bool Cancelled)>();
        selected.Download = () => download.Task;
        model.SelectedDLLRecord = selected;
        var applied = new List<DLLRecord>();
        model.Game.Update = record =>
        {
            applied.Add(record);
            return Task.FromResult((true, "", false));
        };
        var apply = model.SwapDllCommand.ExecuteAsync(null);
        Check(applied.Count == 0 && !model.CanSwap && !model.Dialog.IsSecondaryButtonEnabled);
        model.SelectedDLLRecord = new DLLRecord();
        await model.SwapDllCommand.ExecuteAsync(null);
        Check(applied.Count == 0);
        download.SetResult((true, "", false));
        await apply;
        Check(applied.Count == 1 && ReferenceEquals(applied[0], selected));
        Check(model.Dialog.Hidden && model.CanCloseParentDialog && model.CanSwap && model.Dialog.IsSecondaryButtonEnabled);

        foreach (var cancelled in new[] { false, true })
        {
            model = new DLLPickerControlModel();
            model.SelectedDLLRecord = new DLLRecord { Download = () => Task.FromResult((false, "failed", cancelled)) };
            var count = 0;
            model.Game.Update = _ => { count++; return Task.FromResult((true, "", false)); };
            await model.SwapDllCommand.ExecuteAsync(null);
            Check(count == 0 && !model.Dialog.Hidden && model.CanSwap);
        }
        model = new DLLPickerControlModel();
        model.SelectedDLLRecord = new DLLRecord
        {
            LocalRecord = new LocalRecord { IsDownloaded = true },
            Download = () => throw new Exception("Cached DLL was downloaded again.")
        };
        model.Game.Update = _ => Task.FromResult((false, "install failed", false));
        await model.SwapDllCommand.ExecuteAsync(null);
        Check(!model.Dialog.Hidden && !model.CanCloseParentDialog && model.Messages.Contains("install failed"));
        model.Game.Update = _ => Task.FromResult((true, "", false));
        await model.SwapDllCommand.ExecuteAsync(null);
        Check(model.Dialog.Hidden);
        Console.WriteLine("DLL Apply: waits for download, captures selection, prevents duplicate application, handles download/cancel/install failure and retry.");
    }
}

// The command body is production code; only dialog, model data and I/O are doubled.
namespace DLSS_Swapper.UserControls
{
    public partial class DLLPickerControlModel : ObservableObject
    {
        bool _applying;
        readonly WeakReference<ApplyDialog> _parentDialogWeakReference;
        internal ApplyDialog Dialog { get; } = new();
        internal DLLRecord? SelectedDLLRecord { get; set; }
        internal Game Game { get; } = new("apply-fixture", () => Task.CompletedTask);
        public bool CanSwap { get; set; } = true;
        public bool CanCloseParentDialog { get; set; }
        public IAsyncRelayCommand ResetDllCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
        public List<string> Messages { get; } = [];
        public DLLPickerControlModel() => _parentDialogWeakReference = new(Dialog);
        void ShowTempInfoBar(string title, string message,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational)
            => Messages.Add(message);
    }
    internal sealed class ApplyDialog
    {
        public bool IsSecondaryButtonEnabled { get; set; } = true;
        public bool Hidden { get; private set; }
        public void Hide() => Hidden = true;
    }
}
namespace Microsoft.UI.Xaml.Controls
{
    public enum InfoBarSeverity { Informational, Error }
}
namespace DLSS_Swapper.Helpers
{
    public partial class ResourceHelper
    {
        public static string GetString(string key) => key;
        public static string GetFormattedResourceTemplate(string key, params object[] args) => key;
    }
}
