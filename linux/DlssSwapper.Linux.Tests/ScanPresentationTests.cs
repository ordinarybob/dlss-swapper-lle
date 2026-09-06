using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class ScanPresentationTests
{
    public static Task RunAsync()
    {
        var empty = new ScanResult(new SelectedGame("Game", "fixture", null), [], []);
        var failed = empty with { Warnings = ["Access denied: fixture"] };
        if (ScanPresentation.Problem(empty, true) is not null
            || ScanPresentation.Problem(failed, true) != "Scan has warnings"
            || ScanPresentation.Problem(empty, false) != "Game folder unavailable"
            || !ScanPresentation.Detail(failed).Contains("Access denied: fixture"))
            throw new Exception("Empty, unavailable and warning scan states were conflated.");
        return Task.CompletedTask;
    }
}
