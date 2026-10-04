using System;
using System.IO;
using System.Text.Json;

namespace DLSS_Swapper;

/*
 * For notes on where data is stored please see https://github.com/beeradmoore/dlss-swapper/wiki/Local-Data-Structure 
 */
static class Storage
{
    static string? _storagePath;
#if   PORTABLE && DEBUG
    public static string StoragePath => _storagePath ??= Path.Combine(AppContext.BaseDirectory, "StoredData", "DEBUG");
#elif PORTABLE && !DEBUG
    public static string StoragePath => _storagePath ??= Path.Combine(AppContext.BaseDirectory, "StoredData");
#elif !PORTABLE && DEBUG
    public static string StoragePath => _storagePath ??= Path.Combine(Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"), "DLSS Swapper", "DEBUG");
#elif !PORTABLE && !DEBUG
    public static string StoragePath => _storagePath  ??= Path.Combine(Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"), "DLSS Swapper");
#endif

    static Storage()
    {
        CreateDirectoryIfNotExists(GetStorageFolder());
        CreateDirectoryIfNotExists(GetDynamicJsonFolder());
        CreateDirectoryIfNotExists(GetImageCachePath());
    }

    public static string GetTemp()
    {
#if PORTABLE
        var path = Path.Combine(StoragePath, "temp");
        CreateDirectoryIfNotExists(path);
#else
        var path = Path.Combine(Path.GetTempPath(), "DLSS Swapper");
        CreateDirectoryIfNotExists(path);
#endif
        return path;
    }

    public static string GetStorageFolder()
    {
        return StoragePath;
    }

    public static string GetDynamicJsonFolder()
    {
        return Path.Combine(StoragePath, "json");
    }

    public static string GetUpdatesFolder()
    {
        return Path.Combine(GetTemp(), "updates");
    }

    public static string GetDBPath()
    {
        CreateDirectoryIfNotExists(StoragePath);
        return Path.Combine(StoragePath, "dlss_swapper.db");
    }

    public static string GetImageCachePath()
    {
        return Path.Combine(StoragePath, "image_cache");
    }

    public static string GetManifestPath()
    {
        return Path.Combine(GetDynamicJsonFolder(), "manifest.json");
    }

    public static string GetImportedManifestPath()
    {
        return Path.Combine(GetDynamicJsonFolder(), "imported_manifest.json");
    }

    /// <summary>
    /// Creates the parent directory of a file path. Directory paths are rejected.
    /// </summary>
    /// <returns>True if the directory could be created</returns>
    public static bool CreateDirectoryForFileIfNotExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Logger.Error("A path should not be empty in CreateDirectoryForFileIfNotExists");
            return false;
        }

        if (Directory.Exists(path))
        {
            Logger.Error("A directory should not be passed to CreateDirectoryForFileIfNotExists");
            return false;
        }
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            Logger.Error("A directory should not be empty in CreateDirectoryForFileIfNotExists");
            return false;
        }
        return CreateDirectoryIfNotExists(directory);
    }

    /// <returns>True if the directory exists or was created successfully.</returns>
    public static bool CreateDirectoryIfNotExists(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }
            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return false;
        }
    }

    /// <returns>True after the staged settings file is committed.</returns>
    internal static bool SaveSettingsJson(Settings settings)
    {
        var settingsFile = Path.Combine(GetDynamicJsonFolder(), "settings.json");
        try
        {
            using var staged = new Helpers.StagedOutputFile(settingsFile);
            JsonSerializer.Serialize(staged.Stream, settings, SourceGenerationContext.Default.Settings);
            staged.Commit();
            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return false;
        }
    }

    /// <returns>The saved settings, or null if the file is missing or unreadable.</returns>
    internal static Settings? LoadSettingsJson()
    {
        var settingsFile = Path.Combine(GetDynamicJsonFolder(), "settings.json");

        if (File.Exists(settingsFile) == false)
        {
            return null;
        }

        try
        {
            using (var stream = File.OpenRead(settingsFile))
            {
                return JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.Settings);
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return null;
        }
    }
}
