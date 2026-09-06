namespace DlssSwapper.Linux.Gui;

public sealed class DiagnosticsWindow : OperationReportWindow
{
    public DiagnosticsWindow(string report) : this(report, null) { }
    internal DiagnosticsWindow(string report, Func<string, Task>? copyText)
        : base("Diagnostics", report, false, copyText) { }
}
