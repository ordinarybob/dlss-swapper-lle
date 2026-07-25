using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace DLSS_Swapper.UserControls;

public sealed partial class BatchSwapSummaryControl : UserControl
{
    BatchSwapSummaryControlModel ViewModel { get; }

    public BatchSwapSummaryControl(IReadOnlyList<BatchSwapResult> results)
    {
        InitializeComponent();
        ViewModel = new BatchSwapSummaryControlModel(results);
        DataContext = ViewModel;
    }

    void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(ViewModel.ReportText);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            ShowFeedback(
                ViewModel.TranslationProperties.SuccessText,
                ResourceHelper.GetString("GamesPage_Batch_Summary_CopySuccess"),
                InfoBarSeverity.Success);
        }
        catch (Exception err)
        {
            ShowFeedback(
                ViewModel.TranslationProperties.ErrorText,
                ResourceHelper.GetFormattedResourceTemplate(
                    "GamesPage_Batch_Summary_CopyFailedTemplate",
                    err.Message),
                InfoBarSeverity.Error);
        }
    }

    async void SaveReportButton_Click(object sender, RoutedEventArgs e)
    {
        SaveReportButton.IsEnabled = false;

        try
        {
            var fileFilters = new List<FileSystemHelper.FileFilter>
            {
                new("Text files", "*.txt"),
                new("All files", "*.*"),
            };
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);
            var defaultFileName = $"dlss-swapper-batch-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            var outputPath = FileSystemHelper.SaveFile(
                hWnd,
                fileFilters,
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                defaultFileName,
                "txt");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            await File.WriteAllTextAsync(
                outputPath,
                ViewModel.ReportText,
                new UTF8Encoding(false));
            ShowFeedback(
                ViewModel.TranslationProperties.SuccessText,
                ResourceHelper.GetFormattedResourceTemplate(
                    "GamesPage_Batch_Summary_SaveSuccessTemplate",
                    outputPath),
                InfoBarSeverity.Success);
        }
        catch (Exception err)
        {
            ShowFeedback(
                ViewModel.TranslationProperties.ErrorText,
                ResourceHelper.GetFormattedResourceTemplate(
                    "GamesPage_Batch_Summary_SaveFailedTemplate",
                    err.Message),
                InfoBarSeverity.Error);
        }
        finally
        {
            SaveReportButton.IsEnabled = true;
        }
    }

    void ShowFeedback(string title, string message, InfoBarSeverity severity)
    {
        FeedbackInfoBar.Title = title;
        FeedbackInfoBar.Message = message;
        FeedbackInfoBar.Severity = severity;
        FeedbackInfoBar.IsOpen = true;
    }
}
