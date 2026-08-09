using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.UserControls;

public class GameHistoryControlModel
{
    readonly Game _game;

    public GameHistoryControlModelTranslationProperties TranslationProperties { get; } = new GameHistoryControlModelTranslationProperties();

    public List<GameHistory> HistoryRows { get; } = new List<GameHistory>();

    public GameHistoryControlModel(Game game)
    {
        _game = game;
    }

    public async Task LoadAsync()
    {
        var historyRows = await Database.Instance.Connection
            .Table<GameHistory>()
            .Where(x => x.GameId == _game.ID)
            .ToListAsync();
        HistoryRows.AddRange(historyRows.OrderByDescending(x => x.EventTime));
    }
}
