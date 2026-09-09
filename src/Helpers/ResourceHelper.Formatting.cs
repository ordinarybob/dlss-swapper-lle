using System;
using System.Globalization;

namespace DLSS_Swapper.Helpers;

public partial class ResourceHelper
{
    internal static string FormatWithFallback(Func<string> template, Func<string> fallback,
        Action<Exception> report, params object[] args)
    {
        foreach (var getText in new[] { template, fallback })
        {
            try
            {
                var text = getText();
                if (!string.IsNullOrWhiteSpace(text))
                    return string.Format(CultureInfo.CurrentCulture, text, args);
            }
            catch (Exception error) { report(error); }
        }
        // If both resources are unusable, retain the supplied paths/error details.
        return args.Length == 0 ? "Unable to display this message."
            : "Unable to display this message. Details: " + string.Join("; ", args);
    }
}
