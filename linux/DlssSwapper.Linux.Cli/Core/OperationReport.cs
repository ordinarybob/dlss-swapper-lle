namespace DlssSwapper.Linux.Cli.Core;

public static class OperationReport
{
    public static async Task SaveLocalAsync(string path, string report, CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".report-{Guid.NewGuid():N}.tmp");
        var ownsTemporary = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                await using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true))
                {
                    await writer.WriteAsync(report.AsMemory(), cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (ownsTemporary && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string Describe(IReadOnlyList<OperationResult> results, Translations? translations = null)
    {
        string Format(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        string Outcome(OperationOutcome outcome) => translations?.Get("Linux_Outcome_" + outcome, outcome.ToString()) ?? outcome.ToString();
        if (results.Count == 0) return translations?.Get("Linux_NoResults", "No operation results.") ?? "No operation results.";
        return Format("Linux_ReportCount", "{0} file or family results\n", results.Count)
            + string.Join(" · ", Enum.GetValues<OperationOutcome>().Select(outcome => $"{Outcome(outcome)}: {results.Count(result => result.EffectiveOutcome == outcome)}")) + "\n\n"
            + string.Join("\n\n", results.Select(result => Format("Linux_ReportRow", "{0} — {1}\nGame folder: {2}\nTarget: {3}\n{4}: {5}", result.Game.Name, result.Family, result.Game.RootPath, result.Target, Outcome(result.EffectiveOutcome), result.Message)));
    }
}
