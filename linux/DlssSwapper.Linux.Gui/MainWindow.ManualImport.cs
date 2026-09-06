using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class MainWindow
{
    private async Task<bool> EditSingleImportAsync(PersistentLibrary library, string[] paths)
    {
        try
        {
            if (paths.Length != 1) throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingOneGameFolder", "Choose one local game folder."));
            IEnumerable<string> ExistingPaths() => _allRows.Select(row => row.RootPath)
                .Concat(library.State.ManualGames.Select(game => game.RootPath));
            var root = ManualGameImportWorkflow.Validate(paths[0], ExistingPaths());
            return await new ManualGameImportDialog(library, root, ExistingPaths)
                .ShowDialog<ManualGameState?>(this) is not null;
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = LanguageAppearance.Format("Linux_GamesMessage33", "Could not add game: {0}", ex.Message);
            return false;
        }
    }
}
