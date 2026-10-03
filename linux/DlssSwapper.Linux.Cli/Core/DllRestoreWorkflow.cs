using System.Diagnostics;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record DllRestorePreview(RestorePlanItem Item, string? InstalledHash, string OriginalHash,
    string InstalledVersion, string OriginalVersion);

public sealed record DllRestoreInspection(IReadOnlyList<DllRestorePreview> Files, IReadOnlyList<string> Warnings);

public static class DllRestoreWorkflow
{
    public static IReadOnlyList<DllRestorePreview> Preview(SelectedGame game) => Inspect(game).Files;

    public static DllRestoreInspection Inspect(SelectedGame game, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var plan = new DllScanner().PlanRestore(game, out var scanWarnings);
        var warnings = scanWarnings.ToList();
        var files = new List<DllRestorePreview>();
        foreach (var item in plan)
        {
            try
            {
                files.Add(new(item, File.Exists(item.TargetPath) ? DllScanner.ComputeMd5(item.TargetPath) : null,
                    DllScanner.ComputeMd5(item.BackupPath), Version(item.TargetPath, translations), Version(item.BackupPath, translations)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { warnings.Add(T("Linux_RestoreInspectFailed", "Could not inspect {0}: {1}", item.RelativeTargetPath, ex.Message)); }
        }
        return new(files, warnings);
    }

    public static IReadOnlyList<OperationResult> Apply(IReadOnlyList<DllRestorePreview> confirmed, CancellationToken token, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var results = new List<OperationResult>();
        foreach (var preview in confirmed)
        {
            var item = preview.Item;
            try
            {
                token.ThrowIfCancellationRequested();
                var current = File.Exists(item.TargetPath) ? DllScanner.ComputeMd5(item.TargetPath) : null;
                if (current != preview.InstalledHash || DllScanner.ComputeMd5(item.BackupPath) != preview.OriginalHash)
                    throw new IOException(T("Linux_RestoreFilesChanged", "Files changed since confirmation. Review the versions and try again."));
                results.AddRange(DllOperations.ApplyRestores([item], translations));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                results.Add(new(item.Game, item.Family.DisplayName, item.RelativeTargetPath, false,
                    ex is OperationCanceledException ? T("Linux_RestoreCancelled", "Not restored: cancelled.") : ex.Message,
                    ex is OperationCanceledException ? OperationOutcome.Cancelled : OperationOutcome.Failed));
            }
        }
        return results;
    }

    private static string Version(string path, Translations? translations)
    {
        if (!File.Exists(path)) return translations?.Get("Linux_Streamline_Missing", "Missing") ?? "Missing";
        var unknown = translations?.Get("Linux_Streamline_Unknown", "Unknown") ?? "Unknown";
        try { return DlssSwapper.Shared.PeVersionInfo.Read(path).FileVersion ?? unknown; }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception) { return unknown; }
    }
}
