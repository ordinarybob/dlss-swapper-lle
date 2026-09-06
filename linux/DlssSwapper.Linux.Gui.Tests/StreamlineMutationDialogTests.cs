using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DLSS_Swapper.Data.Streamline;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class StreamlineMutationDialogTests
{
    internal static void Run(Window owner, string fixtureRoot)
    {
        var root = Path.Combine(fixtureRoot, "streamline-mutation");
        Directory.CreateDirectory(root);
        var names = new[] { "sl.common.dll", "sl.dlss_g.dll", "sl.interposer.dll", "sl.reflex.dll" };
        foreach (var name in names) File.WriteAllBytes(Path.Combine(root, name), DllBytes("old:" + name));
        var network = new PackageHandler();
        var cache = Path.Combine(fixtureRoot, "sdk-mutation-cache");
        var window = new StreamlineGameWindow(root, "Mutation fixture", null, network, cache);
        var closed = window.ShowDialog(owner);
        Until(() => Action(window, "Apply all").IsEnabled);
        var header = window.GetVisualDescendants().OfType<CheckBox>().Single(box => Equals(box.Content, "Select all components"));
        header.IsChecked = true;
        Click(window, "Apply selected");
        var confirmation = Confirm(window);
        Check(network.Packages == 1, "Apply did not acquire exactly one package");
        Check(names.All(name => IsOriginal(root, name)), "Download changed game files before confirmation");
        Click(confirmation, "Continue");
        Until(() => Action(window, "Restore all originals").IsEnabled);
        Check(names.All(name => File.ReadAllBytes(Path.Combine(root, name)).SequenceEqual(DllBytes("new:" + name))), "Apply downloaded but did not replace every selected component");
        Check(names.All(name => File.ReadAllBytes(Path.Combine(root, name + StreamlineComponentSet.BackupSuffix)).SequenceEqual(DllBytes("old:" + name))), "Apply failed to preserve originals");
        Check(window.GetVisualDescendants().OfType<CheckBox>().All(box => box.IsChecked == true), "Apply lost selection");
        Click(window, "Close"); Check(closed.IsCompleted, "Updated dialog failed to close");

        window = new StreamlineGameWindow(root, "Reopened fixture", null, new PackageHandler(), cache);
        closed = window.ShowDialog(owner);
        Until(() => Action(window, "Restore all originals").IsEnabled);
        Check(!Action(window, "Apply all").IsEnabled, "Reopened dialog reports changes after completed update");
        var components = window.GetVisualDescendants().OfType<CheckBox>().Where(box => !Equals(box.Content, "Select all components")).ToArray();
        components[0].IsChecked = true;
        LanguageAppearance.Apply("en-US", new Dictionary<string, string>
        {
            ["Linux_StreamlineRestoreTitle"] = "Fixture restore confirmation",
            ["Linux_StreamlinePartialWarning"] = "Fixture warning: partial sets are not recommended."
        });
        Click(window, "Restore selected");
        confirmation = Confirm(window); CheckWarning(confirmation);
        Check(confirmation.Title == "Fixture restore confirmation"
            && confirmation.FindControl<TextBlock>("WarningText")!.Text!.Contains("Fixture warning"),
            "Streamline confirmation did not use translated title and partial-set warning");
        Click(confirmation, "Cancel");
        Until(() => Action(window, "Restore selected").IsEnabled);
        LanguageAppearance.Apply("en-US");
        Check(!names.Any(name => IsOriginal(root, name)), "Cancelled restore changed files");
        Click(window, "Restore selected"); confirmation = Confirm(window); CheckWarning(confirmation);
        Click(confirmation, "Continue");
        Until(() => Action(window, "Apply selected").IsEnabled);
        Check(names.Count(name => IsOriginal(root, name)) == 1 && IsOriginal(root, names[0]), "Selected restore affected wrong components");
        Click(window, "Apply selected"); confirmation = Confirm(window); CheckWarning(confirmation);
        Click(confirmation, "Continue");
        Until(() => Action(window, "Restore selected").IsEnabled && !Action(window, "Apply selected").IsEnabled);
        Check(!names.Any(name => IsOriginal(root, name)), "Partial apply did not restore the updated set");
        Click(window, "Restore all originals"); confirmation = Confirm(window);
        Click(confirmation, "Continue");
        Until(() => Action(window, "Apply all").IsEnabled);
        Check(names.All(name => IsOriginal(root, name)), "Restore all did not restore exact original bytes");
        Click(window, "Close"); Check(closed.IsCompleted, "Restored dialog failed to close");
        Console.WriteLine("PASS headless Streamline acquisition/apply/reopen/partial warnings/restore (not native Linux acceptance)");
    }
    private static bool IsOriginal(string root, string name) => File.ReadAllBytes(Path.Combine(root, name)).SequenceEqual(DllBytes("old:" + name));
    private static Button Action(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
    private static void Click(Window window, string name) => Action(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static ConfirmationDialog Confirm(Window window)
    {
        Until(() => window.OwnedWindows.OfType<ConfirmationDialog>().Any());
        return window.OwnedWindows.OfType<ConfirmationDialog>().Single();
    }
    private static void CheckWarning(ConfirmationDialog window) => Check(window.FindControl<TextBlock>("WarningText")?.Text?.Contains("not recommended") == true, "Partial operation omitted mixed-version warning");
    private static void Until(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        Check(condition(), "Timed out waiting for Streamline operation");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    // Synthetic x64 PE images, matching the engine safety fixtures; production validation remains enabled.
    internal static byte[] DllBytes(string label)
    {
        var bytes = new byte[1024];
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        U16(0, 0x5a4d); U32(60, 128); U32(128, 0x4550);
        U16(132, 0x8664); U16(134, 1); U16(148, 240); U16(150, 0x2022);
        U16(152, 0x20b); U32(184, 4096); U32(188, 512); U32(208, 8192); U32(212, 512);
        U16(220, 3); U32(260, 16);
        Encoding.ASCII.GetBytes(".text").CopyTo(bytes, 392);
        U32(400, 512); U32(404, 4096); U32(408, 512); U32(412, 512); U32(428, 0x60000020);
        Encoding.UTF8.GetBytes(label).CopyTo(bytes, 512);
        return bytes;
    }
    internal sealed class PackageHandler : HttpMessageHandler
    {
        internal int Packages;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpContent content;
            if (request.RequestUri?.Host == "api.github.com")
                content = new StringContent("{\"tag_name\":\"v2.12.0\",\"assets\":[{\"name\":\"streamline-sdk-test.zip\",\"browser_download_url\":\"https://fixture.invalid/sdk.zip\"}]}");
            else
            {
                Interlocked.Increment(ref Packages);
                using var stream = new MemoryStream();
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                    foreach (var name in StreamlineComponentSet.FileNames)
                    { using var entry = zip.CreateEntry("bin/x64/" + name).Open(); entry.Write(DllBytes("new:" + name)); }
                content = new ByteArrayContent(stream.ToArray());
            }
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { RequestMessage = request, Content = content });
        }
    }
}
