using Avalonia.Markup.Xaml.MarkupExtensions;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public class OperationReportWindow : Window
{
    public OperationReportWindow(string title, IReadOnlyList<OperationResult> results)
        : this(title, OperationReport.Describe(results, LanguageAppearance.Current), true)
    {
        AppLog.Write(results.Any(result => !result.Success) ? ApplicationLogLevel.Error : ApplicationLogLevel.Info,
            title + Environment.NewLine + OperationReport.Describe(results));
    }

    internal OperationReportWindow(string title, string report, bool allowSave, Func<string, Task>? copyText = null)
    {
        Title = title; Width = 850; Height = 650; MinWidth = 480; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("*,Auto,Auto"), Margin = new Thickness(18), RowSpacing = 10 };
        layout.Children.Add(new TextBox { Text = report, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap }; Grid.SetRow(status, 1); layout.Children.Add(status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var copy = new Button { Content = allowSave ? LanguageAppearance.Get("GamesPage_Batch_Summary_CopyAll", "Copy all") : LanguageAppearance.Get("Linux_OperationReportWindow_154", "Copy report") }; var save = new Button { Content = LanguageAppearance.Get("Linux_OperationReportWindow_153", "Save report…") }; var close = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Close") };
        actions.Children.Add(copy); if (allowSave) actions.Children.Add(save); actions.Children.Add(close); Grid.SetRow(actions, 2); layout.Children.Add(actions); Content = layout;
        close.Click += (_, _) => Close();
        copy.Click += async (_, _) =>
        {
            copy.IsEnabled = false;
            try
            {
                if (copyText is not null) await copyText(report);
                else
                {
                    if (Clipboard is null) throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingClipboard", "Clipboard is unavailable."));
                    await Clipboard.SetTextAsync(report);
                }
                status.Text = LanguageAppearance.Get("Linux_OperationReportWindow_152", "Report copied.");
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); status.Text = LanguageAppearance.Format("Linux_OperationReportWindow_151", "Could not copy report: {0}", ex.Message); }
            finally { copy.IsEnabled = true; }
        };
        save.Click += async (_, _) =>
        {
            save.IsEnabled = false;
            try
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = LanguageAppearance.Get("Linux_OperationReportWindow_150", "Save operation report"), SuggestedFileName = "DLSS-Swapper-report.txt", DefaultExtension = "txt" });
                if (file is null) return;
                if (file.TryGetLocalPath() is { } path) { await OperationReport.SaveLocalAsync(path, report); status.Text = LanguageAppearance.Get("Linux_OperationReportWindow_149", "Report saved."); return; }
                await using var stream = await file.OpenWriteAsync();
                stream.SetLength(0);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteAsync(report); await writer.FlushAsync();
                status.Text = LanguageAppearance.Get("Linux_OperationReportWindow_149", "Report saved.");
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); status.Text = LanguageAppearance.Format("Linux_OperationReportWindow_148", "Could not save report: {0}", ex.Message); }
            finally { save.IsEnabled = true; }
        };
    }
}
