using System.Diagnostics;

namespace DLSS_Swapper;

internal static class DebuggerHelper
{
    [Conditional("DEBUG")]
    internal static void BreakIfAttached()
    {
        if (Debugger.IsAttached)
        {
            Debugger.Break();
        }
    }
}
