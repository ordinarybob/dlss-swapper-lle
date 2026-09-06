using System.Globalization;

namespace DlssSwapper.Linux.Gui;

internal enum ApplicationLogLevel { Off, Verbose, Debug, Info, Warning, Error }

internal static class AppLog
{
    private static readonly object Gate = new();
    private static string? _directory;
    public static ApplicationLogLevel Level { get; private set; } = ApplicationLogLevel.Error;
    public static string? CurrentPath => _directory is null ? null :
        Path.Combine(_directory, "dlss_swapper_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

    public static void Configure(string stateDirectory, string level)
    {
        lock (Gate) { _directory = Path.Combine(Path.GetFullPath(stateDirectory), "logs"); ChangeLevel(level); }
    }

    public static void ChangeLevel(string level)
    {
        lock (Gate) Level = Enum.TryParse<ApplicationLogLevel>(level, out var parsed) && Enum.IsDefined(parsed)
            ? parsed : ApplicationLogLevel.Error;
    }

    public static void Write(ApplicationLogLevel level, string message)
    {
        lock (Gate)
        {
            if (_directory is null || Level == ApplicationLogLevel.Off || level == ApplicationLogLevel.Off || level < Level) return;
            try
            {
                Directory.CreateDirectory(_directory);
                File.AppendAllText(CurrentPath!, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
                var files = Directory.GetFiles(_directory, "dlss_swapper_*.log")
                    .Where(path => DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path)["dlss_swapper_".Length..],
                        "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(7);
                foreach (var path in files) File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Console.Error.WriteLine("Application log could not be written: " + error.Message); }
        }
    }
}
