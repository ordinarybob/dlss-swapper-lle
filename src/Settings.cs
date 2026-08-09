using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Data;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.Messages;
using DLSS_Swapper.Pages;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace DLSS_Swapper;

internal enum GameLibraryStorageProfile
{
    Standard,
    HardDrive,
}

public class Settings
{
    public const string DefaultFallbackCoverArtApiUrl = "https://en.wikipedia.org/w/api.php";
    public const string DefaultFallbackCoverArtImageHost = "upload.wikimedia.org";
    public const int DefaultGridViewCardSize = 5;
    public const int MinGridViewCardSize = 1;
    public const int MaxGridViewCardSize = 10;

    public const int DefaultRecursiveScanConcurrency = 15;
    public const int HardDriveRecursiveScanConcurrency = 2;
    public const int MinRecursiveScanConcurrency = 1;
    public const int MaxRecursiveScanConcurrency = 26;

    public const int DefaultCoverHydrationConcurrency = 38;
    public const int HardDriveCoverHydrationConcurrency = 1;
    public const int MinCoverHydrationConcurrency = 1;
    public const int MaxCoverHydrationConcurrency = 64;

    public const int DefaultUiCollectionBatchSize = 550;
    public const int MinUiCollectionBatchSize = 10;
    public const int MaxUiCollectionBatchSize = 1000;

    public const int DefaultDatabaseWriteBatchSize = 550;
    public const int MinDatabaseWriteBatchSize = 10;
    public const int MaxDatabaseWriteBatchSize = 1000;

    public const int DefaultBatchSwapConcurrency = 15;
    public const int MinBatchSwapConcurrency = 1;
    public const int MaxBatchSwapConcurrency = 26;

    static Settings? _instance;

    public static Settings Instance => _instance ??= Settings.FromJson();
    //public event EnabledGameLibrariesChangedHandler EnabledGameLibrariesChanged;
    //public delegate Task EnabledGameLibrariesChangedHandler(object sender, EventArgs e);

    // We default this to false to prevent saves firing when loading from json.
    bool _autoSave;

    bool _hasShownWarning;
    public bool HasShownWarning
    {
        get { return _hasShownWarning; }
        set
        {
            if (_hasShownWarning != value)
            {
                _hasShownWarning = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _hasShownMultiplayerWarning;
    public bool HasShownMultiplayerWarning
    {
        get { return _hasShownMultiplayerWarning; }
        set
        {
            if (_hasShownMultiplayerWarning != value)
            {
                _hasShownMultiplayerWarning = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _hasSelectedSystemPerformance;
    public bool HasSelectedSystemPerformance
    {
        get { return _hasSelectedSystemPerformance; }
        set
        {
            if (_hasSelectedSystemPerformance != value)
            {
                _hasSelectedSystemPerformance = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _hasCompletedInitialDeepScan;
    public bool HasCompletedInitialDeepScan
    {
        get { return _hasCompletedInitialDeepScan; }
        set
        {
            if (_hasCompletedInitialDeepScan != value)
            {
                _hasCompletedInitialDeepScan = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _hideNonDLSSGames = true;
    public bool HideNonDLSSGames
    {
        get { return _hideNonDLSSGames; }
        set
        {
            if (_hideNonDLSSGames != value)
            {
                _hideNonDLSSGames = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }


    bool _groupGameLibrariesTogether = true;
    public bool GroupGameLibrariesTogether
    {
        get { return _groupGameLibrariesTogether; }
        set
        {
            if (_groupGameLibrariesTogether != value)
            {
                _groupGameLibrariesTogether = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    ElementTheme _appTheme = ElementTheme.Default;
    public ElementTheme AppTheme
    {
        get { return _appTheme; }
        set
        {
            if (_appTheme != value)
            {
                _appTheme = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _allowDebugDlls;
    public bool AllowDebugDlls
    {
        get { return _allowDebugDlls; }
        set
        {
            if (_allowDebugDlls != value)
            {
                _allowDebugDlls = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }



    bool _allowUntrusted;
    public bool AllowUntrusted
    {
        get { return _allowUntrusted; }
        set
        {
            if (_allowUntrusted != value)
            {
                _allowUntrusted = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }


    ulong _lastPromptWasForVersion;
    public ulong LastPromptWasForVersion
    {
        get { return _lastPromptWasForVersion; }
        set
        {
            if (_lastPromptWasForVersion != value)
            {
                _lastPromptWasForVersion = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }


    // Don't forget to change this back to off.
#if DEBUG
    LoggingLevel _loggingLevel = LoggingLevel.Verbose;
#else
    LoggingLevel _loggingLevel = LoggingLevel.Error;
#endif
    public LoggingLevel LoggingLevel
    {
        get { return _loggingLevel; }
        set
        {
            if (_loggingLevel != value)
            {
                _loggingLevel = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    uint _enabledGameLibraries = uint.MaxValue;
    [Obsolete("This property is deprecated. Use GameLibrarySettings array instead.")]
    public uint EnabledGameLibraries
    {
        get { return _enabledGameLibraries; }
        set
        {
            if (_enabledGameLibraries != value)
            {
                _enabledGameLibraries = value;
            }
        }
    }


    bool _wasLoadingGames;
    public bool WasLoadingGames
    {
        get { return _wasLoadingGames; }
        set
        {
            if (_wasLoadingGames != value)
            {
                _wasLoadingGames = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }


    bool _dontShowManuallyAddingGamesNotice;
    public bool DontShowManuallyAddingGamesNotice
    {
        get { return _dontShowManuallyAddingGamesNotice; }
        set
        {
            if (_dontShowManuallyAddingGamesNotice != value)
            {
                _dontShowManuallyAddingGamesNotice = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    WindowPositionRect _lastWindowSizeAndPosition = new WindowPositionRect();
    public WindowPositionRect LastWindowSizeAndPosition
    {
        get { return _lastWindowSizeAndPosition; }
        set
        {
            if (_lastWindowSizeAndPosition != value)
            {
                _lastWindowSizeAndPosition = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    GameGridViewType _gameGridViewType = GameGridViewType.ListView;
    public GameGridViewType GameGridViewType
    {
        get { return _gameGridViewType; }
        set
        {
            if (_gameGridViewType != value)
            {
                _gameGridViewType = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    GameSortMode _gameSortMode = GameSortMode.NameAscending;
    public GameSortMode GameSortMode
    {
        get { return _gameSortMode; }
        set
        {
            var normalizedValue = Enum.IsDefined(value) ? value : GameSortMode.NameAscending;
            if (_gameSortMode != normalizedValue)
            {
                _gameSortMode = normalizedValue;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }


    int _gridViewCardSize = DefaultGridViewCardSize;
    bool _hasLoadedGridViewCardSize;
    public int GridViewCardSize
    {
        get { return _gridViewCardSize; }
        set
        {
            _hasLoadedGridViewCardSize = true;
            var normalizedValue = Math.Clamp(
                value,
                MinGridViewCardSize,
                MaxGridViewCardSize);
            if (_gridViewCardSize != normalizedValue)
            {
                _gridViewCardSize = normalizedValue;
                WeakReferenceMessenger.Default.Send(new GridDensityChangedMessage());
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    // Read the two checkpoint-era settings once, then omit them from newly
    // written files. They remain named explicitly so existing settings migrate
    // without a reset when the single card-size control replaces them.
    [JsonPropertyName("GridViewPreferredColumns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LegacyGridViewPreferredColumns { get; set; }

    [JsonPropertyName("GridViewPreferredRows")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LegacyGridViewPreferredRows { get; set; }

    int _recursiveScanConcurrency = DefaultRecursiveScanConcurrency;
    public int RecursiveScanConcurrency
    {
        get { return _recursiveScanConcurrency; }
        set
        {
            var clampedValue = Math.Clamp(value, MinRecursiveScanConcurrency, MaxRecursiveScanConcurrency);
            if (_recursiveScanConcurrency != clampedValue)
            {
                _recursiveScanConcurrency = clampedValue;
                GameScanQueue.UpdateConcurrency(clampedValue);
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _dontShowAddMultipleGameFoldersNotice;
    public bool DontShowAddMultipleGameFoldersNotice
    {
        get { return _dontShowAddMultipleGameFoldersNotice; }
        set
        {
            if (_dontShowAddMultipleGameFoldersNotice != value)
            {
                _dontShowAddMultipleGameFoldersNotice = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _dontShowAddMultiGameDirectoryNotice;
    public bool DontShowAddMultiGameDirectoryNotice
    {
        get { return _dontShowAddMultiGameDirectoryNotice; }
        set
        {
            if (_dontShowAddMultiGameDirectoryNotice != value)
            {
                _dontShowAddMultiGameDirectoryNotice = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    int _coverHydrationConcurrency = DefaultCoverHydrationConcurrency;
    public int CoverHydrationConcurrency
    {
        get { return _coverHydrationConcurrency; }
        set
        {
            var clampedValue = Math.Clamp(value, MinCoverHydrationConcurrency, MaxCoverHydrationConcurrency);
            if (_coverHydrationConcurrency != clampedValue)
            {
                _coverHydrationConcurrency = clampedValue;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    int _uiCollectionBatchSize = DefaultUiCollectionBatchSize;
    public int UiCollectionBatchSize
    {
        get { return _uiCollectionBatchSize; }
        set
        {
            var clampedValue = Math.Clamp(value, MinUiCollectionBatchSize, MaxUiCollectionBatchSize);
            if (_uiCollectionBatchSize != clampedValue)
            {
                _uiCollectionBatchSize = clampedValue;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    int _databaseWriteBatchSize = DefaultDatabaseWriteBatchSize;
    public int DatabaseWriteBatchSize
    {
        get { return _databaseWriteBatchSize; }
        set
        {
            var clampedValue = Math.Clamp(value, MinDatabaseWriteBatchSize, MaxDatabaseWriteBatchSize);
            if (_databaseWriteBatchSize != clampedValue)
            {
                _databaseWriteBatchSize = clampedValue;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    int _batchSwapConcurrency = DefaultBatchSwapConcurrency;
    public int BatchSwapConcurrency
    {
        get { return _batchSwapConcurrency; }
        set
        {
            var clampedValue = Math.Clamp(value, MinBatchSwapConcurrency, MaxBatchSwapConcurrency);
            if (_batchSwapConcurrency != clampedValue)
            {
                _batchSwapConcurrency = clampedValue;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    bool _onlyShowDownloadedDlls;
    public bool OnlyShowDownloadedDlls
    {
        get { return _onlyShowDownloadedDlls; }
        set
        {
            if (_onlyShowDownloadedDlls != value)
            {
                _onlyShowDownloadedDlls = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    string _language = string.Empty;
    public string Language
    {
        get { return _language; }
        set
        {
            if (_language != value)
            {
                _language = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    string[] _ignoredPaths = Array.Empty<string>();
    public string[] IgnoredPaths
    {
        get { return _ignoredPaths; }
        set
        {
            if (_ignoredPaths != value)
            {
                _ignoredPaths = value;
                if (_autoSave)
                {
                    SaveJson();
                    WeakReferenceMessenger.Default.Send(new Messages.GameLibrariesStateChangedMessage());
                }
            }
        }
    }

    string[] _customGameAssetDirectoryPatterns = Array.Empty<string>();
    public string[] CustomGameAssetDirectoryPatterns
    {
        get { return _customGameAssetDirectoryPatterns; }
        set
        {
            var normalizedPatterns = (value ?? Array.Empty<string>())
                .Select(static pattern => pattern?.Trim())
                .Where(static pattern => string.IsNullOrWhiteSpace(pattern) == false)
                .Select(static pattern => pattern!.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar))
                .Select(static pattern => pattern.Trim(Path.DirectorySeparatorChar))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static pattern => pattern, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (_customGameAssetDirectoryPatterns.SequenceEqual(normalizedPatterns, StringComparer.OrdinalIgnoreCase) == false)
            {
                _customGameAssetDirectoryPatterns = normalizedPatterns;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    string _fallbackCoverArtApiUrl = DefaultFallbackCoverArtApiUrl;
    public string FallbackCoverArtApiUrl
    {
        get { return _fallbackCoverArtApiUrl; }
        set
        {
            var normalizedValue = value?.Trim() ?? string.Empty;
            if (_fallbackCoverArtApiUrl != normalizedValue)
            {
                _fallbackCoverArtApiUrl = normalizedValue;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    string _fallbackCoverArtImageHost = DefaultFallbackCoverArtImageHost;
    public string FallbackCoverArtImageHost
    {
        get { return _fallbackCoverArtImageHost; }
        set
        {
            var normalizedValue = value?.Trim().TrimEnd('.') ?? string.Empty;
            if (_fallbackCoverArtImageHost.Equals(normalizedValue, StringComparison.OrdinalIgnoreCase) == false)
            {
                _fallbackCoverArtImageHost = normalizedValue;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    GameLibrarySettings[] _gameLibrarySettings = Array.Empty<GameLibrarySettings>();
    public GameLibrarySettings[] GameLibrarySettings
    {
        get { return _gameLibrarySettings; }
        set
        {
            if (_gameLibrarySettings != value)
            {
                _gameLibrarySettings = value;
                if (_autoSave)
                {
                    SaveJson();
                    WeakReferenceMessenger.Default.Send(new Messages.GameLibrariesOrderChangedMessage());
                }
            }
        }
    }

    string _lastLaunchVersion = string.Empty;
    public string LastLaunchVersion
    {
        get { return _lastLaunchVersion; }
        set
        {
            if (_lastLaunchVersion != value)
            {
                _lastLaunchVersion = value;
                if (_autoSave)
                {
                    SaveJson();
                }
            }
        }
    }

    internal static ProxySettings ProxySettings { get; } = new ProxySettings();

    internal void SaveJson()
    {
        Storage.SaveSettingsJson(this);
    }

    internal void ApplyGameLibraryStorageProfile(GameLibraryStorageProfile profile)
    {
        var shouldSave = _autoSave;
        _autoSave = false;
        try
        {
            RecursiveScanConcurrency = profile switch
            {
                GameLibraryStorageProfile.Standard => DefaultRecursiveScanConcurrency,
                GameLibraryStorageProfile.HardDrive => HardDriveRecursiveScanConcurrency,
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
            };
            CoverHydrationConcurrency = profile switch
            {
                GameLibraryStorageProfile.Standard => DefaultCoverHydrationConcurrency,
                GameLibraryStorageProfile.HardDrive => HardDriveCoverHydrationConcurrency,
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
            };
            UiCollectionBatchSize = DefaultUiCollectionBatchSize;
            DatabaseWriteBatchSize = DefaultDatabaseWriteBatchSize;
            BatchSwapConcurrency = DefaultBatchSwapConcurrency;

            HasSelectedSystemPerformance = true;
        }
        finally
        {
            _autoSave = shouldSave;
        }

        if (shouldSave)
        {
            SaveJson();
        }
    }

    static Settings FromJson()
    {
        var settings = Storage.LoadSettingsJson();
        var shouldSave = false;

        // If we couldn't load settings then save the defaults.
        if (settings is null)
        {
            settings = new Settings();
            shouldSave = true;
        }

        if (settings._hasLoadedGridViewCardSize == false)
        {
            settings._gridViewCardSize = ConvertLegacyColumnsToCardSize(
                settings.LegacyGridViewPreferredColumns);
            settings._hasLoadedGridViewCardSize = true;
            shouldSave = true;
        }

        if (settings.LegacyGridViewPreferredColumns != 0
            || settings.LegacyGridViewPreferredRows != 0)
        {
            settings.LegacyGridViewPreferredColumns = 0;
            settings.LegacyGridViewPreferredRows = 0;
            shouldSave = true;
        }

        shouldSave |= settings.CheckGameLibraries();
        var normalizedCustomPatterns = GameAssetCandidatePathIndex.NormalizeCustomDirectoryPatterns(
            settings.CustomGameAssetDirectoryPatterns);
        if (settings.CustomGameAssetDirectoryPatterns.SequenceEqual(
            normalizedCustomPatterns,
            StringComparer.OrdinalIgnoreCase) == false)
        {
            settings.CustomGameAssetDirectoryPatterns = normalizedCustomPatterns;
            shouldSave = true;
        }

        if (shouldSave)
        {
            settings.SaveJson();
        }

        // Re-enable auto save.
        settings._autoSave = true;
        return settings;
    }

    internal static int ConvertLegacyColumnsToCardSize(int columns) => columns > 0
        ? Math.Clamp(
            11 - columns,
            MinGridViewCardSize,
            MaxGridViewCardSize)
        : DefaultGridViewCardSize;

    /// <summary>
    /// Checks game libraries to see if there are any new ones to be added, or misconfigured settings.
    /// </summary>
    /// <returns></returns>
    private bool CheckGameLibraries()
    {
        var gameLibraries = Enum.GetValues<GameLibrary>().ToList();

        // Move manually added to the end of the list by default.
        gameLibraries.Remove(GameLibrary.ManuallyAdded);
        gameLibraries.Add(GameLibrary.ManuallyAdded);

        // If no items are in the list we are migrating them all
        // If there are some items in the list we are only checking and adding new libraries
        if (_gameLibrarySettings.Length == 0)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var enabledGameLibraries = (GameLibrary)EnabledGameLibraries;
#pragma warning restore CS0618 // Type or member is obsolete

            var tempGameLibraries = new List<GameLibrarySettings>(gameLibraries.Count);
            foreach (var gameLibrary in gameLibraries)
            {
                // Enaable libraries based on EnabledGameLibraries property.
                tempGameLibraries.Add(new GameLibrarySettings()
                {
                    GameLibrary = gameLibrary,
                    IsEnabled = enabledGameLibraries.HasFlag(gameLibrary),
                });
            }
            _gameLibrarySettings = tempGameLibraries.ToArray();
            return true;
        }
        else
        {
            // Remove each one of the loaded gameLibraries from the list.
            foreach (var gameLibrarySetting in _gameLibrarySettings)
            {
                gameLibraries.Remove(gameLibrarySetting.GameLibrary);
            }

            // If there are any items it could be a new launch, new library, or misconfigured settings.
            if (gameLibraries.Count > 0)
            {
                var tempGameLibraries = new List<GameLibrarySettings>(_gameLibrarySettings);
                foreach (var gameLibrary in gameLibraries)
                {
                    // Because this is not a full migration new libraries are enabled by default.
                    tempGameLibraries.Add(new GameLibrarySettings()
                    {
                        GameLibrary = gameLibrary,
                        IsEnabled = true,
                    });
                }
                _gameLibrarySettings = tempGameLibraries.ToArray();
                return true;
            }
        }

        return false;
    }
}
