using DLSS_Swapper.Helpers;

internal static class LibraryDownloadProgressTests
{
    public static void Run()
    {
        var progress = new LibraryDownloadProgress();
        var dll = Guid.NewGuid();
        var sdk = Guid.NewGuid();
        var state = progress.Update([new(dll, 50, 100), new(sdk, 0, 0)]);
        Check(state.Visible && state.Indeterminate, "Unknown SDK length must not fake a percentage.");
        state = progress.Update([new(dll, 100, 100, true), new(sdk, 100, 300)]);
        Check(!state.Indeterminate && state.Percent == 50, "Progress must be byte-weighted.");
        state = progress.Update([new(sdk, 200, 300)]);
        Check(state.Percent == 75, "Completed downloads must remain in the total.");
        state = progress.Update([new(sdk, 200, 300)], [new(dll, 100, 100, true)]);
        Check(state.Percent == 75, "Final counters must survive removal between UI ticks.");
        state = progress.Update([new(sdk, 300, 300, true)]);
        Check(state.Visible && state.Indeterminate && state.Text == "Preparing files…", "Keep preparation visible.");
        state = progress.Update([]);
        Check(!state.Visible, "Completion/failure/cancellation must clear the bar.");
        state = progress.Update([new(Guid.NewGuid(), 10, 100)]);
        Check(state.Percent == 10, "A new download must start with fresh totals.");
        Console.WriteLine("Library progress: unknown size, parallel totals, completion, preparation, and reset passed.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
