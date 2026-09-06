using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class AcknowledgementsTests
{
    public static void Run(Window owner)
    {
        var notices = AcknowledgementsWindow.LoadNotices();
        var expected = new[] { "You", "DLSS", "Streamline", "FidelityFX-SDK", "XeSS", "YamlDotNet", "Avalonia", "FuzzySharp", "Google.Protobuf", "DLSS Swapper LLE", "SkiaSharp", "HarfBuzzSharp", ".NET Runtime", "MicroCom.Runtime", "Tmds.DBus.Protocol", "osslsigncode", "libssl3t64", "libzstd1", "zlib1g" };
        foreach (var dependency in new[] { "SkiaSharp", "HarfBuzzSharp", ".NET Runtime" })
            if (new FileInfo(Path.Combine(AppContext.BaseDirectory, "Acknowledgements", dependency, "third-party-notices.txt")).Length == 0)
                throw new Exception("Transitive license notices were not packaged for " + dependency);
        if (!notices.Select(notice => notice.Name).Order().SequenceEqual(expected.Order())
            || notices.Where(notice => notice.Name != "You").Any(notice => string.IsNullOrWhiteSpace(notice.License)))
            throw new Exception("Required packaged acknowledgement/license is missing.");
        var window = new AcknowledgementsWindow(); window.Show(owner);
        window.Width = window.MinWidth; window.Height = window.MinHeight;
        try
        {
            var choices = window.GetVisualDescendants().OfType<ComboBox>().Single();
            foreach (var notice in notices)
            {
                choices.SelectedItem = notice; Dispatcher.UIThread.RunJobs();
                var text = window.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "LicenseText");
                if (text.Text != notice.License) throw new Exception("License text was changed or selection did not update.");
                if (notice.Name is "osslsigncode" or "libssl3t64" or "libzstd1" or "zlib1g")
                {
                    var copyright = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Acknowledgements", notice.Name, "copyright"));
                    if (!text.Text!.Contains(copyright, StringComparison.Ordinal)
                        || !window.GetVisualDescendants().OfType<Button>().Any(button => ToolTip.GetTip(button) is string link && link.StartsWith("https://", StringComparison.Ordinal)))
                        throw new Exception("Verifier dependency copyright or source link was omitted.");
                }
                var close = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Close"));
                var point = close.TranslatePoint(default, window)!.Value;
                if (point.Y < 0 || point.Y + close.Bounds.Height > window.ClientSize.Height + 1)
                    throw new Exception("Acknowledgements close action clipped at minimum height.");
            }
            Console.WriteLine("PASS packaged acknowledgements, exact license display and footer (not native link-opening acceptance)");
        }
        finally { window.Close(); }
    }
}
