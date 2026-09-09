using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class TranslationsTests
{
    public static Task RunAsync()
    {
        if (Translations.Languages.Count != 24) throw new Exception("Translation bundle count changed.");
        foreach (var language in Translations.Languages)
        {
            var text = new Translations(language);
            if (text.Get("General_Cancel", "missing") == "missing" || text.Values.Count == 0)
                throw new Exception($"Missing packaged translation: {language}");
            if (!text.Values.Keys.Any(key => key.StartsWith("Linux_"))) throw new Exception("Linux message fallbacks were not packaged.");
            if (text.RightToLeft != (language is "ar-SA" or "ar-SY" or "fa-IR")) throw new Exception("Wrong layout direction.");
        }
        var fallback = new Translations("unsupported");
        var hostCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            if (new Translations("fr-FR").Format("Linux_LibrarySize", "missing", 1.5) != "1,5 MiB"
                || new Translations("en-US").Format("Linux_LibrarySize", "missing", 1.5) != "1.5 MiB")
                throw new Exception("Library size formatting ignored the selected language.");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = hostCulture; }
        var fixture = new Translations("en-US");
        if (fixture.Get("Linux_Xaml_30", "") != "Add\nGames"
            || fixture.Get("Linux_Xaml_33", "") != "Deep\nScan")
            throw new Exception("Toolbar labels must contain real line breaks, not escaped XML text.");
        var fixtureValues = (Dictionary<string, string>)fixture.Values;
        fixtureValues["Linux_ImportSummary"] = "accepted={0}; rejected={1}\n";
        fixtureValues["Linux_NotImported"] = "REJECTED";
        fixtureValues["Linux_NoDownloads"] = "NOTHING ACQUIRED";
        fixtureValues["Linux_NoResults"] = "EMPTY REPORT";
        var import = DllImportSources.Describe([new("fixture.dll", false, "failure detail")], fixture);
        if (import != "accepted=0; rejected=1\nREJECTED: fixture.dll: failure detail"
            || !LibraryDownloadWorkflow.Describe([], translations: fixture).StartsWith("NOTHING ACQUIRED")
            || OperationReport.Describe([], fixture) != "EMPTY REPORT"
            || OperationReport.Describe([]) != "No operation results.")
            throw new Exception("Summary localization changed counts/details or default CLI behavior.");
        if (fallback.Format("GamesPage_SelectionMode_CountTemplate", "{0} selected", 7) != "7 selected"
            || fallback.Format("GamesPage_SelectionMode_CountTemplate", "Readable fallback") != "Readable fallback")
            throw new Exception("Formatted translations lost values or readable fallback.");
        if (fallback.Language != "en-US" || fallback.Get("General_Cancel", "missing") != "Cancel"
            || fallback.Get("missing-key", "Readable fallback") != "Readable fallback") throw new Exception("Fallback failed.");
        var root = Directory.CreateTempSubdirectory("lle-translations-");
        try
        {
            var library = new PersistentLibrary(new LibraryStateStore(root.FullName));
            library.UpdateState(state => state.Language = "ar-SA");
            if (new PersistentLibrary(new LibraryStateStore(root.FullName)).State.Language != "ar-SA")
                throw new Exception("Language did not survive reopening.");
        }
        finally { root.Delete(true); }
        return Task.CompletedTask;
    }
}
