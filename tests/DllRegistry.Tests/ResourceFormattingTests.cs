using DLSS_Swapper.Helpers;
using System.Globalization;

internal static class ResourceFormattingTests
{
    public static void Run()
    {
        var errors = 0;
        string Format(string text, string fallback, params object[] args) =>
            ResourceHelper.FormatWithFallback(() => text, () => fallback, _ => errors++, args);
        void Check(bool value)
        {
            if (!value) throw new Exception("Resource formatting contract failed.");
        }
        Check(Format("Localized {0}", "English {0}", "path") == "Localized path");
        Check(Format("Broken {", "Could not save {0}", "path") == "Could not save path");
        Check(Format("{9}", "Could not save {0}: {1}", "path", "disk full") == "Could not save path: disk full");
        Check(Format("", "Fallback {0}", "path") == "Fallback path");
        Check(Format("{", "{", "path", "disk full").EndsWith("path; disk full"));
        Check(Format("", "") == "Unable to display this message.");
        Check(ResourceHelper.FormatWithFallback(() => throw new InvalidOperationException(),
            () => "Fallback {0}", _ => errors++, "details") == "Fallback details");
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(Format("{0:0.0}", "", 1.5) == "1,5");
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Check(errors == 5);
        Console.WriteLine("Resource formatting: localized text, malformed/missing resources, English fallback, error details and culture passed.");
    }
}
