internal static class SourceCallSiteTests
{
    // Static regression checks only: these assert wiring, not native UI behavior.
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "src", "App.xaml.cs")))
            root = root.Parent;
        if (root is null) throw new Exception("Repository source required for call-site checks.");
        string Read(string path) => File.ReadAllText(Path.Combine(root.FullName, path)).Replace("\r\n", "\n");
        void Require(string source, string text, string defect)
        {
            if (!source.Contains(text, StringComparison.Ordinal))
                throw new Exception(defect + " call-site regression.");
        }
        var library = Read("src/Pages/LibraryPageModel.cs");
        Require(library, "archive.Entries.Where(x => x.Name.EndsWith(\".dll\", StringComparison.OrdinalIgnoreCase))", "WIN-003");
        var manager = Read("src/Data/DLLManager.cs");
        Require(manager, "IsDevFile = versionInfo.IsDebug,", "WIN-004");
        var grid = Read("src/Pages/GameGridPageModel.cs");
        Require(grid, "_activeSearch = textBox.Text ?? string.Empty;", "WIN-015");
        Require(grid, "CurrentCollectionView = null;\n        CurrentCollectionView = GameManager.Instance.GetGameCollection(_activeSearch);", "WIN-015");
        var checkedSave = "if (!await game.SaveToDatabaseAsync(bypassBatch: true))";
        if (grid.Split(checkedSave).Length - 1 != 3)
            throw new Exception("WIN-016 manual import/restoration must check all three immediate saves.");
        Require(grid, "game.IsHidden = previous;\n                    failed.Add(game.Title);", "WIN-016");
        var startup = Read("src/App.xaml.cs");
        var firstLaunch = startup[startup.IndexOf("var lastLaunchVersion", StringComparison.Ordinal)..startup.IndexOf("Database.Instance.Init();", StringComparison.Ordinal)];
        if (firstLaunch.Contains("File.Create(", StringComparison.Ordinal) || firstLaunch.Contains("GetManifestPath()", StringComparison.Ordinal))
            throw new Exception("WIN-026 startup must not replace the catalog cache.");
        Require(manager, "await memoryStream.CopyToAsync(staged.Stream).ConfigureAwait(false);\n                    staged.Commit();", "WIN-026");
        var toolbox = Read("src/TranslationToolboxWindowModel.cs");
        var save = toolbox[toolbox.IndexOf("async Task SaveAsync()", StringComparison.Ordinal)..toolbox.IndexOf("async Task LoadExistingTranslationAsync()", StringComparison.Ordinal)];
        var formatCheck = save.IndexOf("throw new InvalidOperationException(\"Choose a JSON or CSV filename.\")", StringComparison.Ordinal);
        if (formatCheck < 0 || formatCheck > save.IndexOf("new StagedOutputFile(outputPath)", StringComparison.Ordinal))
            throw new Exception("WIN-020 must validate output format before creating staged output.");
        Require(save, "new StreamWriter(fileStream, System.Text.Encoding.UTF8, leaveOpen: true)", "WIN-020");
        var publish = toolbox[toolbox.IndexOf("async Task PublishAsync()", StringComparison.Ordinal)..];
        foreach (var operation in new[] { save, publish })
        {
            Require(operation, "new StagedOutputFile(outputPath)", "WIN-020");
            Require(operation, "staged.Commit();", "WIN-020");
            if (operation.Contains("File.Create(outputPath)", StringComparison.Ordinal))
                throw new Exception("WIN-020 must not truncate the destination before serialization.");
        }
        Require(publish, "new ZipArchive(staged.Stream, ZipArchiveMode.Create, true)", "WIN-020");
        var steam = Read("src/Data/Steam/SteamLibrary.cs");
        var gameManager = Read("src/Data/GameManager.cs");
        var loadGames = gameManager[gameManager.IndexOf("public async Task LoadGamesAsync(", StringComparison.Ordinal)..gameManager.IndexOf("public ICollectionView GetGameCollection", StringComparison.Ordinal)];
        var gateAcquired = loadGames.IndexOf("await _loadGate.WaitAsync()", StringComparison.Ordinal);
        var protectedScope = loadGames.IndexOf("try\n        {", gateAcquired, StringComparison.Ordinal);
        var batchCreated = loadGames.IndexOf("using var gameAssetPathIndex = GameAssetPathIndex.BeginBatch", StringComparison.Ordinal);
        if (protectedScope < 0 || batchCreated < protectedScope)
            throw new Exception("Scan session must be created inside the load gate's protected scope so it is disposed before gate release, including creation failure.");
        Require(steam, "if (!DiscoveryMetadata.CanRemoveSteamCache(cachedGame.InstallPath, indexRead, readableSteamApps))", "WIN-022");
        Require(steam, "the Steam index or its library could not be fully read; installation status was not verified.", "WIN-022");
        Require(steam, "else if (!await cachedGame.DeleteAsync().ConfigureAwait(false))", "WIN-022");
        var gameControl = Read("src/UserControls/GameControl.xaml");
        var gameControlModel = Read("src/UserControls/GameControlModel.cs");
        Require(gameControl, "ManageStreamlineComponentsCommand", "Retained Streamline action");
        Require(gameControlModel, "new StreamlineComponentsControl(Game, dialog)", "Retained Streamline dialog");
        foreach (var path in new[] { "src/Data/NeuralRendering", "src/Assets/NeuralRendering", "src/UserControls/NeuralRenderingControl.cs" })
            if (File.Exists(Path.Combine(root.FullName, path))
                || Directory.Exists(Path.Combine(root.FullName, path))
                && Directory.EnumerateFileSystemEntries(Path.Combine(root.FullName, path)).Any())
                throw new Exception("Removed DLSS 5 integration remains: " + path);
        foreach (var source in new[] { gameControl, gameControlModel, grid, gameManager, Read("src/Data/Game.cs"), Read("src/Pages/GameGridPage.xaml"), Read("src/DLSS Swapper.csproj") })
            foreach (var removedSymbol in new[] { "NeuralRendering", "DLSS 5", "SharpCompress", "NonNative", "_nonNative", "DlssClassification", "DlssTabIndex", "UpscalingClassification" })
                if (source.Contains(removedSymbol, StringComparison.Ordinal))
                    throw new Exception("Removed experimental integration reintroduced: " + removedSymbol);
        Require(gameManager, "return IsVisibleForSwappableFilter(game, hideNonDLSSGames) && matchesText;", "Single-list game filtering");
        Console.WriteLine("Release wiring: single game list and Streamline retained; DLSS 5 and non-native integration absent.");
        Console.WriteLine("Static call-site checks passed: ZIP case handling, debug metadata, search retention, checked import/restoration saves, translation staging and startup cache preservation (not runtime UI proof).");
    }
}
