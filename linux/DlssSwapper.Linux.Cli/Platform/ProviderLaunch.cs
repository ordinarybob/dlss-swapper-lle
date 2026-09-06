using System.Diagnostics;

namespace DlssSwapper.Linux.Cli.Platform;

public enum ProviderLauncher { Legendary, Heroic, HeroicFlatpak, Wine }

public sealed record ProviderLaunch(ProviderLauncher Launcher, string AppName, string ConfigurationDirectory)
{
    public string HeroicRunner { get; init; } = "legendary";
    public string? WindowsExecutable { get; init; }
    public string? WindowsArgument { get; init; }
    public string? ClientName { get; init; }
    public static ProviderLaunch ForEa(string prefix, string offerId) => ForWineUri(prefix, offerId, "EA app",
        "origin2://game/launch?offerIds=" + Uri.EscapeDataString(offerId));

    public static ProviderLaunch ForEpic(string prefix, string id, string windowsInstallPath)
    {
        _ = EpicDiscovery.ResolveWinePath(prefix, windowsInstallPath); // Validate the Windows path; do not put a Linux host path in the URI.
        return ForWineUri(prefix, id, "Epic Games Launcher", "com.epicgames.launcher://apps/"
            + Uri.EscapeDataString(windowsInstallPath) + "?action=launch&silent=true");
    }

    private static ProviderLaunch ForWineUri(string prefix, string id, string client, string uri) =>
        new(ProviderLauncher.Wine, id, Path.GetFullPath(prefix))
        {
            WindowsExecutable = EpicDiscovery.ResolveWinePath(prefix, @"C:\windows\system32\start.exe"),
            WindowsArgument = uri,
            ClientName = client,
        };
    public string DisplayName => $"{(Launcher == ProviderLauncher.HeroicFlatpak ? "Heroic (Flatpak)" : ClientName is not null ? ClientName + " (Wine)" : Launcher.ToString())} · {ConfigurationDirectory}";
    // Building a request never starts a process or changes launcher configuration.
    public ProcessStartInfo CreateStartInfo(string? heroicExecutable = null, string? wineRunner = null)
    {
        if (string.IsNullOrWhiteSpace(AppName) || AppName.StartsWith('-') || AppName.Any(char.IsControl))
            throw new InvalidOperationException("The launcher's game identifier is invalid.");
        if (!Path.IsPathFullyQualified(ConfigurationDirectory) || !Directory.Exists(ConfigurationDirectory))
            throw new InvalidOperationException("The game's launcher configuration directory is unavailable.");

        if (Launcher == ProviderLauncher.Wine)
        {
            if (string.IsNullOrWhiteSpace(WindowsExecutable) || WindowsArgument is null)
                throw new InvalidOperationException("The Windows client launch configuration is incomplete.");
            return new Core.ManualGameLaunch(WindowsExecutable, Path.GetDirectoryName(WindowsExecutable)!,
                [WindowsArgument], Core.ManualLaunchKind.Wine, wineRunner, ConfigurationDirectory).CreateStartInfo();
        }

        if (Launcher is ProviderLauncher.Heroic or ProviderLauncher.HeroicFlatpak)
        {
            var directory = new DirectoryInfo(ConfigurationDirectory);
            var heroicRoot = HeroicRunner switch
            {
                "legendary" when directory.Name == "legendary" && directory.Parent?.Name == "legendaryConfig" => directory.Parent.Parent,
                "gog" => directory,
                _ => null,
            };
            if (heroicRoot?.Name != "heroic" || heroicRoot.Parent is null)
                throw new InvalidOperationException("The Heroic configuration path does not match the selected game store.");
            if (Launcher == ProviderLauncher.Heroic && !string.IsNullOrWhiteSpace(heroicExecutable)
                && (!Path.IsPathFullyQualified(heroicExecutable) || !File.Exists(heroicExecutable)))
                throw new InvalidOperationException("The configured Heroic executable is unavailable. Check its path in Settings.");
            // Address the selected client, not an OS URI association that may point at the other installation.
            var heroic = new ProcessStartInfo
            {
                FileName = Launcher == ProviderLauncher.HeroicFlatpak ? "flatpak"
                    : string.IsNullOrWhiteSpace(heroicExecutable) ? "heroic" : heroicExecutable,
                UseShellExecute = false,
            };
            if (Launcher == ProviderLauncher.HeroicFlatpak)
            {
                heroic.ArgumentList.Add("run");
                heroic.ArgumentList.Add("com.heroicgameslauncher.hgl");
            }
            else heroic.Environment["XDG_CONFIG_HOME"] = heroicRoot.Parent.FullName;
            heroic.ArgumentList.Add($"heroic://launch?appName={Uri.EscapeDataString(AppName)}&runner={HeroicRunner}");
            return heroic;
        }
        if (Launcher != ProviderLauncher.Legendary)
            throw new InvalidOperationException("This launcher is not supported.");

        var request = new ProcessStartInfo { FileName = "legendary", UseShellExecute = false };
        request.ArgumentList.Add("launch");
        request.ArgumentList.Add(AppName);
        request.Environment["LEGENDARY_CONFIG_PATH"] = Path.GetFullPath(ConfigurationDirectory);
        return request;
    }
}
