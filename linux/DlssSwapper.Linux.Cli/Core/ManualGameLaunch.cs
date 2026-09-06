using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DLSS_Swapper.Data.ManuallyAdded;

namespace DlssSwapper.Linux.Cli.Core;

public enum ManualLaunchKind { Native, Wine }

public sealed record ManualGameLaunch(string Executable, string WorkingDirectory,
    string[] Arguments, ManualLaunchKind Kind, string? Runner = null, string? WinePrefix = null)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    private (string, string, string[], ManualLaunchKind, string?, string?) EqualityKey =>
        (Executable, WorkingDirectory, Arguments, Kind, Runner, WinePrefix);
    public bool Equals(ManualGameLaunch? other) => other is not null && EqualityKey.Equals(other.EqualityKey);
    public override int GetHashCode() => EqualityKey.GetHashCode();

    public ManualGameLaunch Validate()
    {
        if (!Enum.IsDefined(Kind)) throw new IOException("Choose a supported launch method.");
        var executable = ExistingFile(Executable, "game executable");
        if (ManualLaunchManifest.IsExcluded(executable)) throw new IOException("This executable is excluded from game launching.");
        var directory = string.IsNullOrWhiteSpace(WorkingDirectory) ? Path.GetDirectoryName(executable)! : ExistingDirectory(WorkingDirectory, "working folder");
        if (Arguments is null || Arguments.Any(value => value is null || value.Contains('\0')))
            throw new IOException("Launch arguments must not contain null characters.");
        if (Kind == ManualLaunchKind.Wine)
        {
            if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a Windows .exe for Wine.");
            var runner = ExistingFile(Runner, "Wine executable");
            RequireExecutable(runner);
            var prefix = string.IsNullOrWhiteSpace(WinePrefix) ? null : ExistingDirectory(WinePrefix, "Wine prefix");
            return this with { Executable = executable, WorkingDirectory = directory, Arguments = Arguments.ToArray(), Runner = runner, WinePrefix = prefix };
        }
        if (executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Use Wine to launch a Windows .exe.");
        RequireExecutable(executable);
        return this with { Executable = executable, WorkingDirectory = directory, Arguments = Arguments.ToArray(), Runner = null, WinePrefix = null };
    }

    // Constructing this request never launches a process or invokes a shell.
    public ProcessStartInfo CreateStartInfo()
    {
        var launch = Validate();
        var start = new ProcessStartInfo { FileName = launch.Kind == ManualLaunchKind.Wine ? launch.Runner! : launch.Executable,
            WorkingDirectory = launch.WorkingDirectory, UseShellExecute = false };
        if (launch.Kind == ManualLaunchKind.Wine)
        {
            start.ArgumentList.Add(launch.Executable);
            if (launch.WinePrefix is not null) start.Environment["WINEPREFIX"] = launch.WinePrefix;
        }
        foreach (var argument in launch.Arguments) start.ArgumentList.Add(argument);
        return start;
    }

    private static string ExistingFile(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new IOException($"Choose the full path to the {label}.");
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new IOException($"The {label} does not exist.");
        return full;
    }
    private static string ExistingDirectory(string path, string label)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path)) throw new IOException($"Choose an existing full path for the {label}.");
        return Path.GetFullPath(path);
    }
    private static void RequireExecutable(string path)
    {
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            throw new IOException("The selected file is not executable. Choose an executable file; this app does not change file permissions.");
    }
}
