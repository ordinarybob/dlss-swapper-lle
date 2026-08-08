using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Extensions;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml.Controls;
using NvAPIWrapper.DRS;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

public abstract partial class Game : ObservableObject, IComparable<Game>, IEquatable<Game> //, INotifyPropertyChanged
{
    static readonly TimeSpan NegativeScanCacheLifetime = TimeSpan.FromDays(7);

    [PrimaryKey]
    [Column("id")]
    public string ID { get; set; } = string.Empty;

    [Column("platform_id")]
    public string PlatformId { get; set; } = string.Empty;

    [ObservableProperty]
    [Column("title")]
    public partial string Title { get; set; } = string.Empty;

    // Used to cache the title as a base64 string
    string? _titleBase64;
    [Ignore]
    public string TitleBase64 => _titleBase64 ??= Convert.ToBase64String(Encoding.UTF8.GetBytes(Title));

    [Column("install_path")]
    public string InstallPath { get; set; } = string.Empty;

    [ObservableProperty]
    [Column("cover_image")]
    public partial string? CoverImage { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial uint? DlssPreset { get; set; }

    [ObservableProperty]
    [Ignore]
    public partial uint? DlssDPreset { get; set; }


    [ObservableProperty]
    [Ignore]
    public partial uint? DlssGPreset { get; set; }

    [Ignore]
    public DriverSettingsProfile? DriverSettingsProfile { get; set; }

    /*
    [ObservableProperty]
    [property: Column("base_dlss_version")]
    string baseDLSSVersion = string.Empty;

    [ObservableProperty]
    [property: Column("current_dlss_version")]
    string currentDLSSVersion = string.Empty;

    [ObservableProperty]
    [property: Column("current_dlss_hash")]
    string currentDLSSHash = string.Empty;

    [ObservableProperty]
    [property: Column("base_dlss_hash")]
    string baseDLSSHash = string.Empty;

    [ObservableProperty]
    [property: Column("has_dlss")]
    bool hasDLSS = false;
    */

    [ObservableProperty]
    [Column("has_swappable_items")]
    public partial bool HasSwappableItems { get; set; } = false;

    [Column("last_scan_time")]
    public DateTime? LastScanTimeUtc { get; set; }

    [ObservableProperty]
    [Column("notes")]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    [Column("is_favourite")]
    public partial bool IsFavourite { get; set; } = false;

    /// <summary>
    /// If the game is hidden from the main list or not. All hidden games are still processed.
    /// If the value is null the user has not set the value and this should be considered as not hidden.
    /// </summary>
    [ObservableProperty]
    [Column("is_hidden")]
    public partial bool? IsHidden { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEligibilityPending))]
    [Ignore]
    public partial bool Processing { get; set; } = false;

    [Ignore]
    public abstract GameLibrary GameLibrary { get; }

    [Ignore]
    //public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_600_900.jpg");
    //public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_600_900.png");
    public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_400_600.png");
    //public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_600_900.webp");

    [Ignore]
    //public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_600_900.jpg");
    //public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_600_900.png");
    public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_400_600.png");
    //public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_600_900.webp");

    [Ignore]
    public List<GameAsset> GameAssets { get; } = new List<GameAsset>();

    [Ignore]
    public bool NeedsProcessing { get; set; } = false;

    [Ignore]
    public bool IsEligibilityPending => NeedsProcessing || Processing;

    readonly SemaphoreSlim _coverImageGate = new(1, 1);

    // NOTE: DLL type
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentDLSSForSort))]
    [NotifyPropertyChangedFor(nameof(CurrentDLSSVersionSortKey))]
    [Ignore]
    public partial GameAsset? CurrentDLSS { get; set; } = null;

    [Ignore]
    public bool HasCurrentDLSSForSort => CurrentDLSS is not null;

    [Ignore]
    public ulong CurrentDLSSVersionSortKey
    {
        get
        {
            if (CurrentDLSS is null || Version.TryParse(CurrentDLSS.Version, out var version) == false)
            {
                return 0;
            }

            static ulong Component(int value) => (ulong)Math.Clamp(value, 0, ushort.MaxValue);

            return (Component(version.Major) << 48)
                | (Component(version.Minor) << 32)
                | (Component(version.Build) << 16)
                | Component(version.Revision);
        }
    }

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleDLSSFound { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentDLSS_G { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleDLSSGFound { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentDLSS_D { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleDLSSDFound { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentFSR_31_DX12 { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleFSR31DX12Found { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentFSR_31_VK { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleFSR31VKFound { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentXeSS { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleXeSSFound { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentXeLL { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleXeLLFound { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentXeSS_FG { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleXeSSFGFound { get; set; } = false;

    [ObservableProperty]
    [Ignore]
    public partial GameAsset? CurrentXeSS_DX11 { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool MultipleXeSSDX11Found { get; set; } = false;
    

    [Ignore]
    public abstract bool IsReadyToPlay { get; }

    protected void SetID()
    {
        // Seeing as we use ID, it sure would be a shame if a PlatformId was set to "C:\Program Files\"
        // So try to remove all funky characters before

        var platformId = PlatformId;
        foreach (var invalidPathChar in PathHelpers.InvalidFileNamePathChars)
        {
            if (platformId.Contains(invalidPathChar))
            {
                platformId = platformId.Replace(invalidPathChar, '_');
            }
        }

        ID = GameLibrary switch
        {
            GameLibrary.Steam => $"steam_{platformId}",
            GameLibrary.GOG => $"gog_{platformId}",
            GameLibrary.EpicGamesStore => $"epicgamesstore_{platformId}",
            GameLibrary.UbisoftConnect => $"ubisoftconnect_{platformId}",
            GameLibrary.XboxApp => $"xboxapp_{platformId}",
            GameLibrary.ManuallyAdded => $"manuallyadded_{platformId}",
            GameLibrary.BattleNet => $"battlenet_{platformId}",
            GameLibrary.EAApp => $"eaapp_{platformId}",
            _ => throw new Exception($"Unknown GameLibrary {GameLibrary} while setting ID"),
        };
    }

    /// <summary>
    /// Detects DLSS and updates cover image.
    /// </summary>
    public void ProcessGame(
        bool autoSave = true,
        bool forceNeedsProcessing = false,
        bool installPathValidated = false)
    {
        // If we are alreayd procssing we don't need to process again
        if (Processing == true)
        {
            return;
        }

        if (string.IsNullOrEmpty(InstallPath))
        {
            App.CurrentApp.RunOnUIThread(() =>
            {
                NeedsProcessing = false;
                OnPropertyChanged(nameof(IsEligibilityPending));
            });
            return;
        }

        if (installPathValidated == false && Directory.Exists(InstallPath) == false)
        {
            App.CurrentApp.RunOnUIThread(() =>
            {
                NeedsProcessing = false;
                OnPropertyChanged(nameof(IsEligibilityPending));
            });
            return;
        }

        void MarkProcessing()
        {
            Processing = true;
            NeedsProcessing = false;
            HasSwappableItems = false;
        }

        if (GameManager.Instance.ContainsGame(this))
        {
            App.CurrentApp.RunOnUIThread(MarkProcessing);
        }
        else
        {
            MarkProcessing();
        }

        var assetScan = GameAssetPathIndex.PrepareFind(InstallPath);
        var oldGameAssets = GameAssets.ToList();
        var candidateArtworkQueued = false;

        async Task<bool> FinalizeScanAsync(
            Task<IReadOnlyList<DiscoveredGameAsset>> scanResultTask,
            bool persistImmediately,
            bool keepProcessingOnFailure)
        {
            var newHasSwappableItems = false;
            var scanCompleted = false;
            var exhaustiveResultsAvailable = false;
            var replacementAssets = new List<GameAsset>();

            try
            {
                var discoveredAssets = await scanResultTask.ConfigureAwait(false);
                exhaustiveResultsAvailable = true;

                var dllHistory = new List<GameHistory>();
                var unknownGameAssets = new List<GameAsset>();

                void ProcessGame_ProcessGameAsset(GameAsset gameAsset)
                {
                    var oldGameAsset = oldGameAssets.FirstOrDefault(x => x.Path.Equals(gameAsset.Path, StringComparison.OrdinalIgnoreCase));
                    gameAsset.LoadVersion(oldGameAsset);

                    if (oldGameAsset is not null) // DLL existed previously
                    {
                        if (gameAsset.Version == oldGameAsset.Version)
                        {
                            // NOOP
                        }
                        else
                        {
                            dllHistory.Add(new GameHistory()
                            {
                                GameId = ID,
                                EventType = GameHistoryEventType.DLLChangedExternally,
                                EventTime = DateTime.Now,
                                AssetType = gameAsset.AssetType,
                                AssetPath = gameAsset.Path,
                                AssetVersion = gameAsset.DisplayName,
                            });

                            // If the DLL was changed externally (eg. game update) we delete the backup.
                            // This fixes the issue where looking at your game it may appear to be downgraded but
                            // in reality it is because the game updated to a newer version than you had swapped to.
                            var expectedBackupPath = $"{gameAsset.Path}.dlsss";
                            if (File.Exists(expectedBackupPath))
                            {
                                var tempBackupGameAsset = new GameAsset()
                                {
                                    Id = ID,
                                    AssetType = DLLManager.Instance.GetAssetBackupType(gameAsset.AssetType),
                                    Path = expectedBackupPath,
                                };
                                tempBackupGameAsset.LoadVersion();

                                dllHistory.Add(new GameHistory()
                                {
                                    GameId = ID,
                                    EventType = GameHistoryEventType.DLLBackupRemoved,
                                    EventTime = DateTime.Now,
                                    AssetType = tempBackupGameAsset.AssetType,
                                    AssetPath = tempBackupGameAsset.Path,
                                    AssetVersion = tempBackupGameAsset.DisplayName,
                                });

                                File.Delete(expectedBackupPath);
                            }
                        }
                    }
                    else // DLL is new
                    {
                        dllHistory.Add(new GameHistory()
                        {
                            GameId = ID,
                            EventType = GameHistoryEventType.DLLDetected,
                            EventTime = DateTime.Now,
                            AssetType = gameAsset.AssetType,
                            AssetPath = gameAsset.Path,
                            AssetVersion = gameAsset.DisplayName,
                        });
                    }

                    if (gameAsset.HasCurrentHash()
                        && DLLManager.Instance.IsInKnownGameAsset(gameAsset, this) == false)
                    {
                        unknownGameAssets.Add(gameAsset);
                    }

                    LoadBackupForGameAsset(gameAsset, replacementAssets, oldGameAssets);

                }

                foreach (var discoveredAsset in discoveredAssets)
                {
                    var gameAsset = new GameAsset()
                    {
                        Id = ID,
                        AssetType = discoveredAsset.AssetType,
                        Path = discoveredAsset.Path,
                    };
                    ProcessGame_ProcessGameAsset(gameAsset);
                    replacementAssets.Add(gameAsset);
                }

                if (oldGameAssets.Count > 0)
                {
                    using (await Database.Instance.Mutex.LockAsync())
                    {
                        await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
                    }
                }

                if (replacementAssets.Any())
                {
                    newHasSwappableItems = true;

                    //App.CurrentApp.Database.ExecuteAsync
                    //savePoint is not valid, and should be the result of a call to SaveTransactionPoint.
                    using (await Database.Instance.Mutex.LockAsync())
                    {
                        await Database.Instance.Connection.InsertAllAsync(dllHistory, false).ConfigureAwait(false);
                        await Database.Instance.Connection.InsertAllAsync(replacementAssets, false).ConfigureAwait(false);
                    }

                    if (unknownGameAssets.Any())
                    {
                        GameManager.Instance.AddUnknownGameAssets(GameLibrary, Title, unknownGameAssets);
                    }

                    var shouldUpdatedCover = true;

                    if (forceNeedsProcessing == true && File.Exists(ExpectedCustomCoverImage) == false)
                    {
                        // If we are forcing game load and custom cover image doesnt exist we will force load the cover no matter what.
                    }
                    else
                    {
                        // This shouldn't crash, bit if it does lets not take down the entire processing.
                        try
                        {
                            FileInfo? fileInfo = null;
                            var cachedCoverImage = GetCachedCoverImage();
                            if (cachedCoverImage == ExpectedCustomCoverImage)
                            {
                                // If we are using a custom cover we don't want to try reloading any cover so we don't set fileInfo.
                                shouldUpdatedCover = false;
                            }
                            else if (cachedCoverImage is not null)
                            {
                                fileInfo = new FileInfo(cachedCoverImage);
                            }

                            if (fileInfo is not null)
                            {
                                var daysSinceLastModified = (DateTime.Now - fileInfo.LastWriteTime).TotalDays;

                                // Add +/- 2 days so not all will process at the same time.
                                daysSinceLastModified += ((new Random()).NextDouble() - 0.5) * 4.0;

                                // If its less than 7 days lets not try refresh.
                                if (daysSinceLastModified < 7)
                                {
                                    shouldUpdatedCover = false;
                                }
                            }
                        }
                        catch (Exception err)
                        {
                            Logger.Error(err);
                            Debugger.Break();
                        }
                    }

                    try
                    {
                        if (candidateArtworkQueued == false)
                        {
                            await GameCoverHydrationQueue.Instance.EnqueueAsync(
                                this,
                                refreshFromSource: shouldUpdatedCover).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // Artwork failures are logged by the hydration queue and must
                        // not hold up the usable-library barrier.
                    }

                    // Cover implementations post their final property assignment to
                    // the dispatcher. Drain that assignment before exhaustive work.
                    await App.CurrentApp.RunOnUIThreadAsync(async () =>
                    {
                        await Task.Yield();
                    }).ConfigureAwait(false);
                }

                scanCompleted = true;
            }
            catch (Exception err)
            {
                Logger.Error(err);
                Debugger.Break();
            }
            finally
            {
                // Now update all the data on the UI thread.
                await App.CurrentApp.RunOnUIThreadAsync(async () =>
                {
                    NeedsProcessing = scanCompleted == false;
                    if (scanCompleted == false && exhaustiveResultsAvailable == false)
                    {
                        GameAssets.Clear();
                        GameAssets.AddRange(oldGameAssets);
                        UpdateCurrentDLLsFromGameAssets();
                        HasSwappableItems = oldGameAssets.Any();
                    }
                    else
                    {
                        if (scanCompleted)
                        {
                            GameAssets.Clear();
                            GameAssets.AddRange(replacementAssets);
                            UpdateCurrentDLLsFromGameAssets();
                        }
                        HasSwappableItems = scanCompleted && newHasSwappableItems;
                    }
                    if (scanCompleted)
                    {
                        LastScanTimeUtc = DateTime.UtcNow;
                    }

                    if (autoSave)
                    {
                        scanCompleted &= await SaveToDatabaseAsync(persistImmediately);
                    }

                    NeedsProcessing = scanCompleted == false;
                    Processing = keepProcessingOnFailure && scanCompleted == false;
                }).ConfigureAwait(false);
            }

            return scanCompleted;
        }

        GameScanQueue.Instance.Enqueue(async () =>
        {
            IReadOnlyList<DiscoveredGameAsset> candidateAssets = [];
            var candidatePublished = false;
            try
            {
                candidateAssets = await assetScan.ExecuteCandidatesAsync().ConfigureAwait(false);
                if (candidateAssets.Count > 0 && oldGameAssets.Count > 0)
                {
                    var retainedCandidates = candidateAssets.ToList();
                    var retainedPaths = retainedCandidates
                        .Select(static asset => asset.Path)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var oldGameAsset in oldGameAssets)
                    {
                        if (File.Exists(oldGameAsset.Path)
                            && GameAssetPathIndex.TryGetAssetType(
                                Path.GetFileName(oldGameAsset.Path),
                                out var assetType)
                            && retainedPaths.Add(oldGameAsset.Path))
                        {
                            retainedCandidates.Add(new DiscoveredGameAsset(oldGameAsset.Path, assetType));
                        }
                    }

                    candidateAssets = retainedCandidates;
                }
                if (candidateAssets.Count == 0)
                {
                    return;
                }

                var provisionalAssets = candidateAssets
                    .Select(discoveredAsset => new GameAsset()
                    {
                        Id = ID,
                        AssetType = discoveredAsset.AssetType,
                        Path = discoveredAsset.Path,
                    })
                    .ToList();
                foreach (var provisionalAsset in provisionalAssets)
                {
                    var cachedAsset = oldGameAssets.FirstOrDefault(x =>
                        x.Path.Equals(provisionalAsset.Path, StringComparison.OrdinalIgnoreCase));
                    provisionalAsset.LoadVersion(cachedAsset);
                }

                var localCoverImage = FindLocalCoverImage();

                await App.CurrentApp.RunOnUIThreadAsync(() =>
                {
                    if (localCoverImage is not null)
                    {
                        CoverImage = localCoverImage;
                    }
                    GameAssets.Clear();
                    GameAssets.AddRange(provisionalAssets);
                    UpdateCurrentDLLsFromGameAssets();
                    HasSwappableItems = true;
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                if (localCoverImage is null)
                {
                    candidateArtworkQueued = true;
                    GameCoverHydrationQueue.Instance.Enqueue(this);
                }

                candidatePublished = true;
            }
            catch (Exception err)
            {
                // Candidate discovery is an acceleration layer. The exhaustive scan
                // below remains authoritative if this pass cannot complete.
                Logger.Error(err);
            }
            finally
            {
                assetScan.CompleteCandidatePublication(candidatePublished);
                if (forceNeedsProcessing == false && candidateAssets.Count > 0)
                {
                    var candidateResultTask = Task.FromResult(candidateAssets);
                    GameScanQueue.Instance.Enqueue(async () =>
                    {
                        var succeeded = false;
                        try
                        {
                            succeeded = await FinalizeScanAsync(
                                candidateResultTask,
                                persistImmediately: true,
                                keepProcessingOnFailure: true).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (succeeded == false)
                            {
                                var exhaustiveResultTask = assetScan.ExecuteAsync();
                                GameScanQueue.Instance.EnqueueAfter(
                                    exhaustiveResultTask,
                                    () => FinalizeScanAsync(
                                        exhaustiveResultTask,
                                        persistImmediately: false,
                                        keepProcessingOnFailure: false));
                            }

                            assetScan.CompleteCandidateProcessing(succeeded);
                        }
                    });
                }
                else
                {
                    var exhaustiveResultTask = assetScan.ExecuteAsync();
                    GameScanQueue.Instance.EnqueueAfter(
                        exhaustiveResultTask,
                        () => FinalizeScanAsync(
                            exhaustiveResultTask,
                            persistImmediately: false,
                            keepProcessingOnFailure: false));
                }
            }
        }, trackProgress: false);
    }

    void LoadBackupForGameAsset(
        GameAsset gameAsset,
        List<GameAsset> replacementAssets,
        IReadOnlyList<GameAsset> cachedAssets)
    {
        var backupPath = $"{gameAsset.Path}.dlsss";
        if (File.Exists(backupPath))
        {
            var gameAssetBackup = new GameAsset()
            {
                Id = ID,
                AssetType = DLLManager.Instance.GetAssetBackupType(gameAsset.AssetType),
                Path = backupPath,
            };
            var cachedBackup = cachedAssets.FirstOrDefault(x =>
                x.Path.Equals(backupPath, StringComparison.OrdinalIgnoreCase));
            gameAssetBackup.LoadVersion(cachedBackup);
            replacementAssets.Add(gameAssetBackup);
        }
    }


    public async Task LoadCoverImageAsync()
    {
        await _coverImageGate.WaitAsync().ConfigureAwait(false);
        try
        {
            // TODO: Update if the image last write is > 1 week old or something

            var cachedCoverImage = GetCachedCoverImage();
            if (cachedCoverImage is not null)
            {
                App.CurrentApp.RunOnUIThread(() =>
                {
                    CoverImage = cachedCoverImage;
                });
            }
            else
            {
                // If no cover exists use the abstracted method to get the game as expect for this library.
                await UpdateCacheImageAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _coverImageGate.Release();
        }
    }

    string? GetCachedCoverImage()
    {
        if (File.Exists(ExpectedCustomCoverImage))
        {
            return ExpectedCustomCoverImage;
        }

        if (File.Exists(ExpectedCoverImage))
        {
            return ExpectedCoverImage;
        }

        if (string.IsNullOrWhiteSpace(CoverImage) == false && File.Exists(CoverImage))
        {
            return CoverImage;
        }

        return null;
    }

    protected virtual string? FindLocalCoverImage()
    {
        return GetCachedCoverImage();
    }

    internal bool PrimeCachedCoverImage()
    {
        var cachedCoverImage = GetCachedCoverImage();
        if (cachedCoverImage is null)
        {
            return false;
        }

        CoverImage = cachedCoverImage;
        return true;
    }

    protected void UseLocalCoverImage(string coverImagePath)
    {
        App.CurrentApp.RunOnUIThread(() =>
        {
            CoverImage = coverImagePath;
        });
    }

    protected abstract Task UpdateCacheImageAsync();

    internal async Task RefreshCoverFromSourceAsync()
    {
        await _coverImageGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await UpdateCacheImageAsync().ConfigureAwait(false);
        }
        finally
        {
            _coverImageGate.Release();
        }
    }

    internal async Task EnsureAssetHashesAsync(IReadOnlyList<GameAsset> assets)
    {
        var assetsToCheck = assets
            .GroupBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (assetsToCheck.Count == 0)
        {
            return;
        }

        var assetsToPersist = await Task.Run(() =>
        {
            var changedAssets = new List<GameAsset>();
            foreach (var asset in assetsToCheck)
            {
                if (asset.HasCurrentHash() == false)
                {
                    asset.EnsureHashLoaded();
                    changedAssets.Add(asset);
                }
            }
            return changedAssets;
        }).ConfigureAwait(false);
        if (assetsToPersist.Count == 0)
        {
            return;
        }

        using (await Database.Instance.Mutex.LockAsync())
        {
            foreach (var asset in assetsToPersist)
            {
                await Database.Instance.Connection.ExecuteAsync(
                    "UPDATE game_asset SET version = ?, hash = ?, file_length = ?, last_write_time_utc_ticks = ? WHERE id = ? AND asset_type = ? AND path = ?",
                    asset.Version,
                    asset.Hash,
                    asset.FileLength,
                    asset.LastWriteTimeUtcTicks,
                    asset.Id,
                    asset.AssetType,
                    asset.Path).ConfigureAwait(false);
            }
        }
    }

    internal async Task<(bool Success, string Message, bool PromptToRelaunchAsAdmin)> ResetDllAsync(GameAssetType gameAssetType)
    {
        var backupRecordType = DLLManager.Instance.GetAssetBackupType(gameAssetType);
        var existingBackupRecords = this.GameAssets.Where(x => x.AssetType == backupRecordType).ToList();

        if (existingBackupRecords.Count == 0)
        {
            Logger.Info("No backup records found.");
            return (false, "Unable to reset to default. Please repair your game manually.", false);
        }
        else
        {
            var dllHistory = new List<GameHistory>();
            foreach (var existingBackupRecord in existingBackupRecords)
            {
                var primaryRecordName = existingBackupRecord.Path.Replace(".dlsss", string.Empty);
                var existingRecords = this.GameAssets.Where(x => x.AssetType == gameAssetType && x.Path.Equals(primaryRecordName)).ToList();

                if (existingRecords.Count != 1)
                {
                    Logger.Info("Backup record was found, existing records were not.");
                    return (false, "Unable to reset to default. Please repair your game manually.", false);
                }

                var existingRecord = existingRecords[0];
                var backupHash = existingBackupRecord.HasCurrentHash()
                    ? existingBackupRecord.Hash
                    : string.Empty;

                try
                {
                    File.Move(existingBackupRecord.Path, existingRecord.Path, true);
                }
                catch (UnauthorizedAccessException err)
                {
                    Logger.Error(err);
                    if (App.CurrentApp.IsAdminUser() is false)
                    {
                        return (false, "Unable to reset to default. Running DLSS Swapper as administrator may fix this.", true);
                    }
                    else
                    {
                        return (false, "Unable to reset to default. Please repair your game manually.", false);
                    }
                }
                catch (Exception err)
                {
                    Logger.Error(err);
                    return (false, "Unable to reset to default. Please repair your game manually.", false);
                }

                var newGameAsset = new GameAsset()
                {
                    Id = ID,
                    AssetType = gameAssetType,
                    Path = existingRecord.Path,
                };
                newGameAsset.SetKnownVersionAndHash(existingBackupRecord.Version, backupHash);


                dllHistory.Add(new GameHistory()
                {
                    GameId = ID,
                    EventType = GameHistoryEventType.DLLReset,
                    EventTime = DateTime.Now,
                    AssetType = gameAssetType,
                    AssetPath = existingRecord.Path,
                    AssetVersion = existingBackupRecord.DisplayName,
                });

                UpdateCurrentAsset(newGameAsset, gameAssetType);

                GameAssets.Remove(existingRecord);
                GameAssets.Remove(existingBackupRecord);
                GameAssets.Add(newGameAsset);
            }

            using (await Database.Instance.Mutex.LockAsync())
            {
                await Database.Instance.Connection.InsertAllAsync(dllHistory, false);

                // Update game assets list by deleting and re-adding.
                await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
                await Database.Instance.Connection.InsertAllAsync(GameAssets, false).ConfigureAwait(false);
            }

            return (true, string.Empty, false);
        }
    }

    /// <summary>
    /// Attempts to update a DLSS dll in a given game.
    /// </summary>
    /// <param name="dlssRecord"></param>
    /// <returns>Tuple containing a boolean of Success, if this is false there will be an error message in the Message response.</returns>
    internal async Task<(bool Success, string Message, bool PromptToRelaunchAsAdmin)> UpdateDllAsync(DLLRecord dllRecord)
    {
        if (dllRecord is null)
        {
            return (false, "Unable to swap dll as your dll record was not found.", false);
        }

        if (dllRecord.LocalRecord is null)
        {
            return (false, "Unable to swap dll as your local dll record was not found.", false);
        }

        if (File.Exists(dllRecord.LocalRecord.ExpectedPath) == false)
        {
            return (false, "Downloaded dll not found.", false);
        }

        var existingRecords = this.GameAssets.Where(x => x.AssetType == dllRecord.AssetType).ToList();
        if (existingRecords.Count == 0)
        {
            return (false, "Unable to swap dll as there were no dll records to update.", false);
        }

        var backupRecordType = DLLManager.Instance.GetAssetBackupType(dllRecord.AssetType);
        var existingBackupRecords = this.GameAssets.Where(x => x.AssetType == backupRecordType).ToList();

        var versionInfo = FileVersionInfo.GetVersionInfo(dllRecord.LocalRecord.ExpectedPath);
        var dllVersion = versionInfo.GetFormattedFileVersion();
        var md5Hash = versionInfo.GetMD5Hash();
        if (dllRecord.MD5Hash != md5Hash)
        {
            return (false, "Unable to swap dll because dll hash was invalid.", false);
        }


        // Validate new DLL
        if (Settings.Instance.AllowUntrusted == false)
        {
            var isTrusted = WinTrust.VerifyEmbeddedSignature(dllRecord.LocalRecord.ExpectedPath);
            if (isTrusted == false)
            {
                return (false, "Unable to swap dll as we are unable to verify the signature of the version you are trying to use.\nIf you wish to override this decision please enable 'Allow Untrusted' in settings.", false);
            }
        }

        var newGameAssets = new List<GameAsset>();

        if (existingBackupRecords.Count == 0)
        {
            // Backup old dlls if no backup exists.
            foreach (var existingRecord in existingRecords)
            {
                var dllPath = Path.GetDirectoryName(existingRecord.Path);
                if (string.IsNullOrEmpty(dllPath))
                {
                    Logger.Error("dllPath was null or empty.");
                    return (false, "Unable to swap dll. Please check your error log for more information.", false);
                }

                // Ensure we don't do anything if the target exists.
                var backupDllPath = $"{existingRecord.Path}.dlsss";
                if (File.Exists(backupDllPath) == false)
                {
                    try
                    {
                        File.Copy(existingRecord.Path, backupDllPath);

                        var backupGameAsset = new GameAsset()
                        {
                            Id = ID,
                            AssetType = backupRecordType,
                            Path = backupDllPath,
                        };
                        var existingHash = existingRecord.HasCurrentHash()
                            ? existingRecord.Hash
                            : string.Empty;
                        backupGameAsset.SetKnownVersionAndHash(existingRecord.Version, existingHash);
                        newGameAssets.Add(backupGameAsset);
                    }
                    catch (UnauthorizedAccessException err)
                    {
                        Logger.Error(err);
                        if (App.CurrentApp.IsAdminUser() is false)
                        {
                            return (false, "Unable to swap dll as we are unable to write to the target directory. Running DLSS Swapper as administrator may fix this.", true);

                        }
                        else
                        {
                            return (false, "Unable to swap dll as we are unable to write to the target directory.", false);
                        }
                    }
                    catch (Exception err)
                    {
                        Logger.Error(err);
                        return (false, "Unable to swap dll. Please check your error log for more information.", false);
                    }
                }
            }
        }

        var dllHistory = new List<GameHistory>();

        foreach (var existingRecord in existingRecords)
        {
            try
            {
                // Copy the DLL
                File.Copy(dllRecord.LocalRecord.ExpectedPath, existingRecord.Path, true);

                var newGameAsset = new GameAsset()
                {
                    Id = ID,
                    AssetType = dllRecord.AssetType,
                    Path = existingRecord.Path,
                };
                newGameAsset.SetKnownVersionAndHash(dllVersion, dllRecord.MD5Hash);
                // The downloaded record already supplies the exact version and hash.
                newGameAssets.Add(newGameAsset);

                dllHistory.Add(new GameHistory()
                {
                    GameId = ID,
                    EventType = GameHistoryEventType.DLLSwapped,
                    EventTime = DateTime.Now,
                    AssetType = dllRecord.AssetType,
                    AssetPath = existingRecord.Path,
                    AssetVersion = dllRecord.DisplayName,
                });
            }
            catch (UnauthorizedAccessException err)
            {
                Logger.Error(err);
                if (App.CurrentApp.IsAdminUser() is false)
                {
                    return (false, "Unable to swap dll as we are unable to write to the target directory. Running DLSS Swapper as administrator may fix this.", true);
                }
                else
                {
                    return (false, "Unable to DLSS dll as we are unable to write to the target directory.", false);
                }
            }
            catch (IOException err) when (err.HResult == -2147024864)
            {
                Logger.Error(err);
                return (false, "Unable to swap dll. It appears to be in use by another program. Is your game currently running?", false);
            }
            catch (Exception err)
            {
                Logger.Error(err);
                return (false, "Unable to swap dll. Please check your error log for more information.", false);
            }
        }

        foreach (var existingRecrod in existingRecords)
        {
            GameAssets.Remove(existingRecrod);
        }
        GameAssets.AddRange(newGameAssets);

        // This should never be null.
        // Using FirstOrDefault as there may be multiple, but we only care about using the information of the first.
        var firstNewGameAsset = newGameAssets.FirstOrDefault(x => x.AssetType == dllRecord.AssetType);
        if (firstNewGameAsset is not null)
        {
            UpdateCurrentAsset(firstNewGameAsset, dllRecord.AssetType);
        }

        // Update game assets list by deleting and re-adding.
        using (await Database.Instance.Mutex.LockAsync())
        {
            await Database.Instance.Connection.InsertAllAsync(dllHistory, false);
            await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
            await Database.Instance.Connection.InsertAllAsync(GameAssets, false).ConfigureAwait(false);
        }

        return (true, string.Empty, false);
    }

    void UpdateCurrentAsset(GameAsset newGameAsset, GameAssetType gameAssetType)
    {
        App.CurrentApp.RunOnUIThread(() =>
        {
            // NOTE: DLL type
            if (gameAssetType == GameAssetType.DLSS)
            {
                CurrentDLSS = null;
                CurrentDLSS = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.DLSS_G)
            {
                CurrentDLSS_G = null;
                CurrentDLSS_G = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.DLSS_D)
            {
                CurrentDLSS_D = null;
                CurrentDLSS_D = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.FSR_31_DX12)
            {
                CurrentFSR_31_DX12 = null;
                CurrentFSR_31_DX12 = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.FSR_31_VK)
            {
                CurrentFSR_31_VK = null;
                CurrentFSR_31_VK = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.XeSS)
            {
                CurrentXeSS = null;
                CurrentXeSS = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.XeSS_FG)
            {
                CurrentXeSS_FG = null;
                CurrentXeSS_FG = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.XeSS_DX11)
            {
                CurrentXeSS_DX11 = null;
                CurrentXeSS_DX11 = newGameAsset;
            }
            else if (gameAssetType == GameAssetType.XeLL)
            {
                CurrentXeLL = null;
                CurrentXeLL = newGameAsset;
            }
            else
            {
                Logger.Error($"Unknown AssetType: {gameAssetType}");
            }
        });
    }

    #region IComparable<Game>
    public int CompareTo(Game? other)
    {
        if (other is null)
        {
            return -1;
        }

        return Title.CompareTo(other.Title);
    }
    #endregion

    /*
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged = null;
    void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
    */


    protected async Task ResizeCoverAsync(Stream imageStream)
    {
        // TODO:
        // - find optimal format (eg, is displaying 100 webp images more intense than 100 png images)
        // - load image based on scale
        try
        {
            using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream).ConfigureAwait(false))
            {
                // If images are really big we resize to at least 2x the 200x300 we display as.
                // In future this should be updated to resize to display scale.
                // If the image is smaller than this we are just saving as png.
                var resizeOptions = new ResizeOptions()
                {
                    Size = new Size(200 * 2, 300 * 2),
                    Sampler = KnownResamplers.Lanczos5,
                    Mode = ResizeMode.Min, // If image is smaller it won't be resized up.
                };
                image.Mutate(x => x.Resize(resizeOptions));
                image.SaveAsPng(ExpectedCoverImage);
                //image.SaveAsWebp(ExpectedCoverImage);
                //image.SaveAsJpeg(ExpectedCoverImage);
            }

            App.CurrentApp.RunOnUIThread(() =>
            {
                CoverImage = null;
                CoverImage = ExpectedCoverImage;
            });
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }


    public void AddCustomCover(string imageSource)
    {
        using (var fileStream = File.OpenRead(imageSource))
        {
            AddCustomCover(fileStream);
        }
    }

    public void AddCustomCover(Stream stream)
    {
        // TODO:
        // - find optimal format (eg, is displaying 100 webp images more intense than 100 png images)
        // - load image based on scale
        try
        {
            using (var image = SixLabors.ImageSharp.Image.Load(stream))
            {
                // If images are really big we resize to at least 3x the 200x300 we display as.
                // In future this should be updated to resize to display scale.
                // If the image is smaller than this we are just saving as png.
                var resizeOptions = new ResizeOptions()
                {
                    Size = new Size(200 * 3, 300 * 3),
                    Sampler = KnownResamplers.Lanczos5,
                    Mode = ResizeMode.Min, // If image is smaller it won't be resized up.
                };
                image.Mutate(x => x.Resize(resizeOptions));
                image.SaveAsPng(ExpectedCustomCoverImage);
                //image.SaveAsWebp(ExpectedCustomCoverImage);
                //image.SaveAsJpeg(ExpectedCustomCoverImage);
            }

            App.CurrentApp.RunOnUIThread(() =>
            {
                CoverImage = ExpectedCustomCoverImage;
            });
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }

    protected async Task<bool> DownloadCoverAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Logger.Error($"Tried to download cover image but url was null or empty. Game: {Title}, Library: {GameLibrary}");
            return false;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == false &&
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == false)
        {
            Logger.Error($"Tried to download cover image but url was not valid. Game: {Title}, Library: {GameLibrary}, Url: {url}");
            return false;
        }


        var extension = Path.GetExtension(url);

        // Path.GetExtension retains query arguments, so ths will remove them if they exist.
        if (extension.Contains('?'))
        {
            extension = extension.Substring(0, extension.IndexOf("?"));
        }
        var tempFile = Path.Combine(Storage.GetTemp(), $"{ID}{extension}");


        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(url, 0);
                await fileDownloader.DownloadFileToStreamAsync(memoryStream).ConfigureAwait(false);
                memoryStream.Position = 0;

                // Now if the image is downloaded lets resize it,
                await ResizeCoverAsync(memoryStream).ConfigureAwait(false);
            }
            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err, $"For url: {url}");
            //Debugger.Break();
            return false;
        }
        finally
        {
            // Cleanup temp file.
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    public async Task<bool> SaveToDatabaseAsync(bool bypassBatch = false)
    {
        try
        {
            if (bypassBatch == false && GameDatabaseWriteBatch.Instance.TryEnqueue(this))
            {
                return true;
            }

            var rowsChanged = -1;
            using (await Database.Instance.Mutex.LockAsync())
            {
                rowsChanged = await Database.Instance.Connection.InsertOrReplaceAsync(this);
                // tODO: Configure await
            }
            if (rowsChanged == 0)
            {
                // TODO: Fix why this happens occasionally to reandom games.
                // This appears to change to different games in different libraries.
                Logger.Error($"Tried to save game to database but rowsChanged was 0.");
                //Debugger.Break();
                return false;
            }
            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            Debugger.Break();
            return false;
        }
    }

    public async Task<bool> DeleteAsync()
    {
        try
        {
            // Sometimes when a game is uninstalled the backup files are not removed, so ensure they are.
            // https://github.com/beeradmoore/dlss-swapper/issues/236

            List<GameAsset> gameAssets;
            using (await Database.Instance.Mutex.LockAsync())
            {
                gameAssets = await Database.Instance.Connection.Table<GameAsset>().Where(ga => ga.Id == ID).ToListAsync();
            }
            foreach (var cachedGameAsset in gameAssets)
            {
                // NOTE: DLL type
                // If its a file we made we should attempt to delete it.
                if (cachedGameAsset.AssetType == GameAssetType.DLSS_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.DLSS_G_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.DLSS_D_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.FSR_31_DX12_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.FSR_31_VK_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.XeSS_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.XeSS_FG_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.XeSS_DX11_BACKUP ||
                    cachedGameAsset.AssetType == GameAssetType.XeLL_BACKUP)
                {
                    if (File.Exists(cachedGameAsset.Path))
                    {
                        Logger.Info($"Deleting {cachedGameAsset.Path}");
                        try
                        {
                            File.Delete(cachedGameAsset.Path);
                        }
                        catch (Exception err)
                        {
                            Logger.Error(err, $"Could not delete {cachedGameAsset.Path}");
                        }
                    }
                }
            }
            using (await Database.Instance.Mutex.LockAsync())
            {
                await Database.Instance.Connection.Table<GameAsset>().DeleteAsync(ga => ga.Id == ID).ConfigureAwait(false);
            }

            // Delete the thumbnails.
            var thumbnailImages = Directory.GetFiles(Storage.GetImageCachePath(), $"{ID}_*", SearchOption.AllDirectories);
            foreach (var thumbnailImage in thumbnailImages)
            {
                try
                {
                    Logger.Info($"Deleting {thumbnailImage}");
                    File.Delete(thumbnailImage);
                }
                catch (Exception err)
                {
                    Logger.Error(err, $"Could not delete {thumbnailImage}");
                }
            }

            // Delete the game itself.
            using (await Database.Instance.Mutex.LockAsync())
            {
                await Database.Instance.Connection.DeleteAsync(this).ConfigureAwait(false);
            }

            // Remove the game from the list.
            GameManager.Instance.RemoveGame(this);
            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return false;
        }
    }

    public async Task PromptToRemoveCustomCover()
    {
        var dialog = new EasyContentDialog(App.CurrentApp.MainWindow.Content.XamlRoot)
        {
            Title = ResourceHelper.GetString("Game_CustomCoverRemove"),
            PrimaryButtonText = ResourceHelper.GetString("General_Remove"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = ResourceHelper.GetString("Game_AreYouSureRemoveCustomCover"),
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            CoverImage = null;

            if (File.Exists(ExpectedCustomCoverImage))
            {
                File.Delete(ExpectedCustomCoverImage);
            }

            if (this.GameLibrary == GameLibrary.ManuallyAdded)
            {
                await SaveToDatabaseAsync();
            }

            // Will load default or attempt to fetch fresh.
            await LoadCoverImageAsync();
        }
    }

    public void PromptToBrowseCustomCover()
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);

            var fileFilters = new List<FileSystemHelper.FileFilter>()
            {
                new FileSystemHelper.FileFilter("Image files", "*.jpg; *.jpeg; *.png; *.webp"),
            };

            var coverImageFile = FileSystemHelper.OpenFile(hWnd, fileFilters, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            //                    ViewMode = PickerViewMode.Thumbnail,


            if (string.IsNullOrWhiteSpace(coverImageFile))
            {
                return;
            }

            AddCustomCover(coverImageFile);
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }

    public bool Equals(Game? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ID == other.ID)
        {
            return true;
        }

        if (PlatformId == other.PlatformId)
        {
            return true;
        }

        return false;
    }

    protected bool ParentUpdateFromGame(Game game)
    {
        var didChange = false;

        if (Title != game.Title)
        {
            Title = game.Title;
            didChange = true;
        }

        if (InstallPath != game.InstallPath)
        {
            InstallPath = PathHelpers.NormalizePath(game.InstallPath);
            didChange = true;
        }

        if (CoverImage != game.CoverImage)
        {
            CoverImage = game.CoverImage;
            didChange = true;
        }

        if (HasSwappableItems != game.HasSwappableItems)
        {
            HasSwappableItems = game.HasSwappableItems;
            didChange = true;
        }

        // NOTE: DLL type
        if (CurrentDLSS != game.CurrentDLSS)
        {
            CurrentDLSS = game.CurrentDLSS;
            didChange = true;
        }

        if (DlssPreset != game.DlssPreset)
        {
            DlssPreset = game.DlssPreset;
            didChange = true;
        }

        if (DlssDPreset != game.DlssDPreset)
        {
            DlssDPreset = game.DlssDPreset;
            didChange = true;
        }

        if (CurrentDLSS_G != game.CurrentDLSS_G)
        {
            CurrentDLSS_G = game.CurrentDLSS_G;
            didChange = true;
        }

        if (CurrentDLSS_D != game.CurrentDLSS_D)
        {
            CurrentDLSS_D = game.CurrentDLSS_D;
            didChange = true;
        }

        if (CurrentFSR_31_DX12 != game.CurrentFSR_31_DX12)
        {
            CurrentFSR_31_DX12 = game.CurrentFSR_31_DX12;
            didChange = true;
        }

        if (CurrentFSR_31_VK != game.CurrentFSR_31_VK)
        {
            CurrentFSR_31_VK = game.CurrentFSR_31_VK;
            didChange = true;
        }

        if (CurrentXeSS != game.CurrentXeSS)
        {
            CurrentXeSS = game.CurrentXeSS;
            didChange = true;
        }

        if (CurrentXeSS_FG != game.CurrentXeSS_FG)
        {
            CurrentXeSS_FG = game.CurrentXeSS_FG;
            didChange = true;
        }

        if (CurrentXeSS_DX11 != game.CurrentXeSS_DX11)
        {
            CurrentXeSS_DX11 = game.CurrentXeSS_DX11;
            didChange = true;
        }

        if (CurrentXeLL != game.CurrentXeLL)
        {
            CurrentXeLL = game.CurrentXeLL;
            didChange = true;
        }

        // We don't copy across the following properties as it is assume this object has the latest revisions:
        // - Notes
        // - IsFavourite

        return didChange;
    }

    public abstract bool UpdateFromGame(Game game);

    void UpdateCurrentDLLsFromGameAssets()
    {
        CurrentDLSS = null;
        CurrentDLSS_G = null;
        CurrentDLSS_D = null;
        CurrentFSR_31_DX12 = null;
        CurrentFSR_31_VK = null;
        CurrentXeSS = null;
        CurrentXeSS_FG = null;
        CurrentXeSS_DX11 = null;
        CurrentXeLL = null;

        // NOTE: DLL type
        MultipleDLSSFound = GameAssets.Count(x => x.AssetType == GameAssetType.DLSS) > 1;
        MultipleDLSSGFound = GameAssets.Count(x => x.AssetType == GameAssetType.DLSS_G) > 1;
        MultipleDLSSDFound = GameAssets.Count(x => x.AssetType == GameAssetType.DLSS_D) > 1;
        MultipleFSR31DX12Found = GameAssets.Count(x => x.AssetType == GameAssetType.FSR_31_DX12) > 1;
        MultipleFSR31VKFound = GameAssets.Count(x => x.AssetType == GameAssetType.FSR_31_VK) > 1;
        MultipleXeSSFound = GameAssets.Count(x => x.AssetType == GameAssetType.XeSS) > 1;
        MultipleXeSSFGFound = GameAssets.Count(x => x.AssetType == GameAssetType.XeSS_FG) > 1;
        MultipleXeSSDX11Found = GameAssets.Count(x => x.AssetType == GameAssetType.XeSS_DX11) > 1;
        MultipleXeLLFound = GameAssets.Count(x => x.AssetType == GameAssetType.XeLL) > 1;

        // NOTE: DLL type
        foreach (var gameAsset in GameAssets)
        {
            if (gameAsset.AssetType == GameAssetType.DLSS)
            {
                CurrentDLSS = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.DLSS_G)
            {
                CurrentDLSS_G = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.DLSS_D)
            {
                CurrentDLSS_D = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.FSR_31_DX12)
            {
                CurrentFSR_31_DX12 = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.FSR_31_VK)
            {
                CurrentFSR_31_VK = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.XeSS)
            {
                CurrentXeSS = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.XeSS_FG)
            {
                CurrentXeSS_FG = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.XeSS_DX11)
            {
                CurrentXeSS_DX11 = gameAsset;
            }
            else if (gameAsset.AssetType == GameAssetType.XeLL)
            {
                CurrentXeLL = gameAsset;
            }
        }
    }

    public async Task RemoveGameAssetsFromCacheAsync()
    {
        using (await Database.Instance.Mutex.LockAsync())
        {
            await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
        }
    }

    public async Task LoadGameAssetsFromCacheAsync()
    {
        GameAssets.Clear();
        using (await Database.Instance.Mutex.LockAsync())
        {
            var gameAssets = await Database.Instance.Connection.Table<GameAsset>().Where(ga => ga.Id == ID).ToListAsync().ConfigureAwait(false);
            if (gameAssets?.Any() == true)
            {
                GameAssets.AddRange(gameAssets);
            }
        }

        UpdateCurrentDLLsFromGameAssets();

        // TODO: Add auto reload by storing last full reload time on game

        if (GameAssets.Any())
        {
            foreach (var gameAsset in GameAssets)
            {
                // Check that each of the game assets exist, after we will check if they are what we expect them to be
                if (File.Exists(gameAsset.Path) == false)
                {
                    NeedsProcessing = true;
                    break;
                }
            }

            if (NeedsProcessing == false)
            {
                var unknownGameAssets = new List<GameAsset>();
                foreach (var gameAsset in GameAssets)
                {
                    var cachedVersion = gameAsset.Version;
                    gameAsset.LoadVersion(gameAsset);

                    if (gameAsset.Version != cachedVersion)
                    {
                        NeedsProcessing = true;
                        break;
                    }

                    if (gameAsset.HasCurrentHash()
                        && DLLManager.Instance.IsInKnownGameAsset(gameAsset, this) == false)
                    {
                        unknownGameAssets.Add(gameAsset);
                    }
                }
                if (unknownGameAssets.Any())
                {
                    GameManager.Instance.AddUnknownGameAssets(GameLibrary, Title, unknownGameAssets);
                }
            }
        }
        else
        {
            // A successful empty scan is still useful cache data. Periodically re-scan
            // in case the game changed outside a launcher update or explicit refresh.
            NeedsProcessing = LastScanTimeUtc is null
                || LastScanTimeUtc < DateTime.UtcNow.Subtract(NegativeScanCacheLifetime);
        }
    }

    public bool IsInIgnoredPath()
    {
        // If there are no ignored paths we can skip this altogether.
        if (Settings.Instance.IgnoredPaths.Length == 0)
        {
            return false;
        }

        // If installed path is empty we should consider it ignored.
        if (string.IsNullOrWhiteSpace(InstallPath))
        {
            return true;
        }

        foreach (var ignoredPath in Settings.Instance.IgnoredPaths)
        {
            // Because we make IgnoredPaths have a / on the end it will fail the below check.
            // In the cases where the path could be off by one we will do a manual check.
            if (ignoredPath.Length - 1 == InstallPath.Length)
            {
                var tempInstallPath = InstallPath + Path.DirectorySeparatorChar;
                if (tempInstallPath.Equals(ignoredPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }


            if (InstallPath.StartsWith(ignoredPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
