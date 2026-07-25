using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.UserControls;

class BatchSwapSummaryControlModel
{
    public int SwappedCount { get; }
    public int AlreadyCurrentCount { get; }
    public int SkippedCount { get; }
    public int ErrorCount { get; }
    public string AdminMessage { get; } = string.Empty;
    public string ResultsText { get; } = string.Empty;
    public string ReportText { get; } = string.Empty;

    public BatchSwapSummaryControlModelTranslationProperties TranslationProperties { get; } = new();

    public BatchSwapSummaryControlModel(IReadOnlyList<BatchSwapResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        SwappedCount = results.Count(result => result.Status == BatchSwapStatus.Swapped);
        AlreadyCurrentCount = results.Count(result => result.Status == BatchSwapStatus.AlreadyCurrent);
        SkippedCount = results.Count(result => result.Status == BatchSwapStatus.Skipped);
        ErrorCount = results.Count(result => result.Status == BatchSwapStatus.Error);

        var adminGameCount = results
            .Where(result => result.PromptToRelaunchAsAdmin)
            .Select(result => result.GameIdentity)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (adminGameCount > 0)
        {
            AdminMessage = ResourceHelper.GetFormattedResourceTemplate(
                "GamesPage_Batch_Summary_NeedsAdminTemplate",
                adminGameCount);
        }

        ResultsText = string.Join(
            Environment.NewLine,
            results
                .Where(result =>
                    result.Status == BatchSwapStatus.Swapped
                    || result.Status == BatchSwapStatus.Skipped
                    || result.Status == BatchSwapStatus.Error)
                .Select(result => result.DisplayText));
        ReportText = BuildReportText();
    }

    string BuildReportText()
    {
        var report = new StringBuilder();
        report.AppendLine(ResourceHelper.GetString("GamesPage_Batch_Summary_Title"));
        report.AppendLine();
        report
            .Append(TranslationProperties.SwappedText)
            .Append(' ')
            .AppendLine(SwappedCount.ToString(CultureInfo.CurrentCulture));
        report
            .Append(TranslationProperties.AlreadyCurrentText)
            .Append(' ')
            .AppendLine(AlreadyCurrentCount.ToString(CultureInfo.CurrentCulture));
        report
            .Append(TranslationProperties.SkippedText)
            .Append(' ')
            .AppendLine(SkippedCount.ToString(CultureInfo.CurrentCulture));
        report
            .Append(TranslationProperties.ErrorsText)
            .Append(' ')
            .AppendLine(ErrorCount.ToString(CultureInfo.CurrentCulture));

        if (string.IsNullOrWhiteSpace(AdminMessage) == false)
        {
            report.AppendLine();
            report.AppendLine(AdminMessage);
        }

        if (string.IsNullOrWhiteSpace(ResultsText) == false)
        {
            report.AppendLine();
            report.AppendLine(TranslationProperties.ResultsText);
            report.AppendLine(ResultsText);
        }

        return report.ToString().TrimEnd();
    }
}
