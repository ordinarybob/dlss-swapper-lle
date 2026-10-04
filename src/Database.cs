using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.BattleNet;
using DLSS_Swapper.Data.EAApp;
using DLSS_Swapper.Data.EpicGamesStore;
using DLSS_Swapper.Data.GOG;
using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.Data.Steam;
using DLSS_Swapper.Data.UbisoftConnect;
using DLSS_Swapper.Data.Xbox;
using Nito.AsyncEx;
using SQLite;

namespace DLSS_Swapper;

#if DEBUG
public class SQLiteTableInfo
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
#endif

internal class Database
{
    static Database? _instance;
    internal static Database Instance => _instance ??= new Database();

    internal AsyncLock Mutex { get; init; }
    internal SQLiteAsyncConnection Connection { get; init; }

    public Database()
    {

    void RenameTable(SQLiteConnection connection, string from, string to)
        {
            try
            {
                var tableExists = connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{from}'") > 0;
                if (tableExists)
                {
                    connection.Execute($"ALTER TABLE {from} RENAME TO {to}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                DebuggerHelper.BreakIfAttached();
            }
        }

        void RenameColumn(SQLiteConnection connection, string table, string from, string to)
        {
            try
            {
                var columns = connection.Query<SQLiteConnection.ColumnInfo>($"PRAGMA table_info({table})");

                if (columns.Any(c => c.Name == from))
                {
                    connection.Execute($"ALTER TABLE {table} RENAME COLUMN {from} TO {to}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                DebuggerHelper.BreakIfAttached();
            }
        }

        using (var syncConnection = new SQLiteConnection(Storage.GetDBPath()))
        {
            // Migrate pre-1.2.2 table names to the current schema.
            RenameTable(syncConnection, "SteamGame", "steam_game");
            RenameTable(syncConnection, "ManuallyAddedGame", "manually_added_game");
            RenameTable(syncConnection, "GOGGame", "gog_game");
            RenameTable(syncConnection, "eaapp_game", "ea_app_game");
            RenameTable(syncConnection, "EpicGamesStoreGame", "epic_games_store_game");
            RenameTable(syncConnection, "UbisoftConnectGame", "ubisoft_connect_game");
            RenameTable(syncConnection, "XboxGame", "xbox_game");
            RenameTable(syncConnection, "BattleNetGame", "battlenet_game");
            RenameTable(syncConnection, "GameHistory", "game_history");
            RenameTable(syncConnection, "GameAsset", "game_asset");

            RenameColumn(syncConnection, "battlenet_game", "StatePlayable", "state_playable");
            RenameColumn(syncConnection, "battlenet_game", "RemoteCoverImage", "remote_cover_image");
            RenameColumn(syncConnection, "battlenet_game", "LauncherId", "launcher_id");
            RenameColumn(syncConnection, "gog_game", "FallbackHeaderUrl", "fallback_header_url");
            RenameColumn(syncConnection, "game_asset", "Hash", "hash");

            syncConnection.Execute("DROP INDEX IF EXISTS GameAsset_id");
            syncConnection.Execute("DROP INDEX IF EXISTS GameHistory_game_id");

            // Reject unintended persistence: mapped properties need explicit snake_case table/column names.
#if DEBUG
            var validDbParts = new List<string>()
            {
                "id",
                "platform",
                "title",
                "install",
                "path",
                "cover",
                "image",
                "has",
                "swappable",
                "items",
                "notes",
                "is",
                "hidden",
                "epic",
                "games",
                "store",
                "game",
                "remote",
                "header",
                "history",
                "event",
                "type",
                "asset",
                "steam",
                "state",
                "flags",
                "favourite",
                "gog",
                "ubisoft",
                "connect",
                "xbox",
                "application",
                "battlenet",
                "time",
                "version",
                "manually",
                "added",
                "playable",
                "ea",
                "app",
                "display",
                "icon",
                "fallback",
                "header",
                "url",
                "launcher",
                "hash",
                "last",
                "scan",
                "file",
                "length",
                "write",
                "utc",
                "ticks",
            };

            var hasIssues = false;
            var validRegex = new Regex(@"^[a-z_]+$", RegexOptions.Compiled);
            var tables = syncConnection.Query<SQLiteTableInfo>("SELECT name FROM sqlite_master WHERE type='table'");
            foreach (var table in tables)
            {
                if (validRegex.IsMatch(table.Name) == false)
                {
                    Logger.Error($"{table.Name} is an invalid table name. Table names can only be lowercase a-z and underscore.");
                    hasIssues = true;
                }

                var tableNameParts = table.Name.Split('_');
                foreach (var tableNamePart in tableNameParts)
                {
                    if (validDbParts.Contains(tableNamePart) == false)
                    {
                        Logger.Error($"validDbParts list does not contain '{tableNamePart}'.");
                        hasIssues = true;
                    }
                }

                var columns = syncConnection.Query<SQLiteConnection.ColumnInfo>($"PRAGMA table_info({table.Name})");
                foreach (var column in columns)
                {
                    if (validRegex.IsMatch(column.Name) == false)
                    {
                        Logger.Error($"{column.Name} in {table.Name} is an invalid column name. Column names can only be lowercase a-z and underscore.");
                        hasIssues = true;
                    }

                    var columnNameParts = column.Name.Split('_');
                    foreach (var columnNamePart in columnNameParts)
                    {
                        if (validDbParts.Contains(columnNamePart) == false)
                        {
                            Logger.Error($"validDbParts list does not contain '{columnNamePart}'.");
                            hasIssues = true;
                        }
                    }
                }
            }

            if (hasIssues)
            {
                Logger.Error($"You will need to delete {Storage.GetDBPath()} to remove these errors.");
                DebuggerHelper.BreakIfAttached();
            }
#endif

            try
            {
                syncConnection.CreateTable<GameHistory>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<SteamGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<GOGGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<EpicGamesStoreGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<UbisoftConnectGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<XboxGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<ManuallyAddedGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<BattleNetGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<EAAppGame>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            try
            {
                syncConnection.CreateTable<GameAsset>();
            }
            catch (Exception err)
            {
                Logger.Error(err);
                DebuggerHelper.BreakIfAttached();
            }

            syncConnection.Close();
        }

        Mutex = new AsyncLock();
        Connection = new SQLiteAsyncConnection(Storage.GetDBPath());
    }

    public void Init()
    {
        Logger.Verbose("Database Init");
    }

}
