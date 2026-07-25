using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

internal sealed class GameDatabaseWriteBatch
{
    static readonly Lazy<GameDatabaseWriteBatch> _instance = new(() => new GameDatabaseWriteBatch());

    public static GameDatabaseWriteBatch Instance => _instance.Value;

    readonly object _lock = new();
    readonly Dictionary<string, Game> _pendingGames = new(StringComparer.Ordinal);
    bool _active;

    public void Begin()
    {
        lock (_lock)
        {
            _pendingGames.Clear();
            _active = true;
        }
    }

    public bool TryEnqueue(Game game)
    {
        lock (_lock)
        {
            if (_active == false)
            {
                return false;
            }

            _pendingGames[game.ID] = game;
            return true;
        }
    }

    public async Task EndAndFlushAsync()
    {
        try
        {
            while (true)
            {
                List<Game> games;
                lock (_lock)
                {
                    if (_pendingGames.Count == 0)
                    {
                        _active = false;
                        return;
                    }

                    games = _pendingGames.Values
                        .Take(Settings.Instance.DatabaseWriteBatchSize)
                        .ToList();

                    foreach (var game in games)
                    {
                        _pendingGames.Remove(game.ID);
                    }
                }

                using (await Database.Instance.Mutex.LockAsync())
                {
                    await Database.Instance.Connection.RunInTransactionAsync(connection =>
                    {
                        foreach (var game in games)
                        {
                            var rowsChanged = connection.InsertOrReplace(game);
                            if (rowsChanged == 0)
                            {
                                Logger.Error($"Tried to save {game.Title} to database but rowsChanged was 0.");
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _active = false;
                _pendingGames.Clear();
            }
        }
    }
}
