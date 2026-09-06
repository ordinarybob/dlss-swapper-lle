using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

internal static class DiagnosticsReport
{
    internal static string BuildIdentity() => Collect([
        ("DLSS Swapper LLE", () => typeof(MainWindow).Assembly.GetName().Version?.ToString()),
        (LanguageAppearance.Get("Linux_Diagnostic0", "Recorded version/revision"), () => typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion),
        (LanguageAppearance.Get("Linux_Diagnostic1", "Assembly ID"), () => typeof(MainWindow).Assembly.ManifestModule.ModuleVersionId.ToString()),
        (LanguageAppearance.Get("Linux_Diagnostic2", "Application"), () => LanguageAppearance.Get("Linux_Diagnostic3", "Linux GUI")),
        (LanguageAppearance.Get("Linux_Diagnostic4", "Build target"), () => BuildMetadata("LleRuntimeIdentifier") ?? LanguageAppearance.Get("Linux_Diagnostic5", "Portable .NET target")),
        (LanguageAppearance.Get("Linux_Diagnostic6", "Package format"), () => BuildMetadata("LlePackageFormat") ?? LanguageAppearance.Get("Linux_Diagnostic7", "Not recorded by this build")),
        (LanguageAppearance.Get("Linux_Diagnostic8", "Self-contained runtime"), () => BuildMetadata("LleSelfContained") ?? LanguageAppearance.Get("Linux_Diagnostic7", "Not recorded by this build"))
    ]);

    internal static string Capture(PersistentLibrary? library, IReadOnlyList<SelectedGame>? games)
    {
        var fields = new (string, Func<string?>)[]
        {
            ("DLSS Swapper LLE", () => typeof(MainWindow).Assembly.GetName().Version?.ToString()),
            (LanguageAppearance.Get("Linux_Diagnostic2", "Application"), () => LanguageAppearance.Get("Linux_Diagnostic3", "Linux GUI")),
            (LanguageAppearance.Get("Linux_Diagnostic6", "Package format"), () => BuildMetadata("LlePackageFormat") ?? LanguageAppearance.Get("Linux_Diagnostic7", "Not recorded by this build")),
            (LanguageAppearance.Get("Linux_Diagnostic8", "Self-contained runtime"), () => BuildMetadata("LleSelfContained") ?? LanguageAppearance.Get("Linux_Diagnostic7", "Not recorded by this build")),
            (LanguageAppearance.Get("Linux_Diagnostic4", "Build target"), () => BuildMetadata("LleRuntimeIdentifier") ?? LanguageAppearance.Get("Linux_Diagnostic5", "Portable .NET target")),
            (LanguageAppearance.Get("Linux_Diagnostic9", "OS"), () => RuntimeInformation.OSDescription),
            (LanguageAppearance.Get("Linux_Diagnostic10", "OS version"), () => Environment.OSVersion.VersionString),
            (LanguageAppearance.Get("Linux_Diagnostic11", "OS architecture"), () => RuntimeInformation.OSArchitecture.ToString()),
            (LanguageAppearance.Get("Linux_Diagnostic12", "Process architecture"), () => RuntimeInformation.ProcessArchitecture.ToString()),
            (LanguageAppearance.Get("Linux_Diagnostic13", "Runtime"), () => RuntimeInformation.FrameworkDescription),
            (LanguageAppearance.Get("Linux_Diagnostic14", "Privileged process"), () => Environment.IsPrivilegedProcess.ToString()),
            (LanguageAppearance.Get("Linux_Diagnostic15", "Library storage"), () => library?.StateDirectory),
            (LanguageAppearance.Get("Linux_Diagnostic16", "Working directory"), () => Environment.CurrentDirectory),
            (LanguageAppearance.Get("Linux_Diagnostic17", "Application directory"), () => AppContext.BaseDirectory),
            (LanguageAppearance.Get("Linux_Diagnostic18", "Assembly location"), () => typeof(MainWindow).Assembly.Location),
            (LanguageAppearance.Get("Linux_Diagnostic19", "Process path"), () => Environment.ProcessPath),
        };
        var report = new StringBuilder(Collect(fields)).AppendLine().AppendLine(LanguageAppearance.Get("Linux_Diagnostic20", "Libraries"));
        if (library is null) return report.AppendLine(LanguageAppearance.Get("Linux_Diagnostic21", "Unavailable: library initialization did not complete.")).ToString();
        try
        {
            foreach (var entry in LibrarySelection.Read(library.State))
            {
                report.Append(entry.Id).Append(": ").AppendLine(entry.IsEnabled ? LanguageAppearance.Get("Linux_Diagnostic22", "Enabled") : LanguageAppearance.Get("Linux_Diagnostic23", "Disabled"));
                if (entry.IsEnabled)
                    report.Append(LanguageAppearance.Get("Linux_Diagnostic24", "Games: ")).AppendLine(games is null ? LanguageAppearance.Get("Linux_Diagnostic25", "Not available") : games.Count(game =>
                        GameViewPolicy.LibraryName(game, library.State.ManualGames.Any(manual =>
                            PathComparers.FileSystemPath.Equals(manual.RootPath, game.RootPath))) == entry.Id).ToString());
            }
        }
        catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); report.Append(LanguageAppearance.Get("Linux_Diagnostic26", "Library details unavailable: ")).AppendLine(error.Message); }
        return report.ToString();
    }

    internal static string? BuildMetadata(string key) => typeof(MainWindow).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .SingleOrDefault(attribute => attribute.Key == key)?.Value is { Length: > 0 } value ? value : null;

    internal static string Collect(IEnumerable<(string Name, Func<string?> Read)> fields)
    {
        var text = new StringBuilder();
        foreach (var (name, read) in fields)
        {
            text.Append(name).Append(": ");
            try { text.AppendLine(read() is { Length: > 0 } value ? value : LanguageAppearance.Get("Linux_Diagnostic27", "Unavailable")); }
            catch (Exception error) { AppLog.Write(ApplicationLogLevel.Error, error.Message); text.Append(LanguageAppearance.Get("Linux_Diagnostic28", "Unavailable — ")).AppendLine(error.Message); }
        }
        return text.ToString();
    }
}
