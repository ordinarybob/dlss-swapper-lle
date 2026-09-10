using DLSS_Swapper.Data.ManuallyAdded;

internal static class ExecutableRankingTests
{
    public static void Run()
    {
        var empty = new ManualLaunchManifest.ExecutableMetadata("", "", "");
        void First(string title, string expected, params string[] names)
        {
            var candidates = names.Select(name => new ManualLaunchManifest.Candidate(name, name)).ToArray();
            foreach (var order in new[] { candidates, candidates.Reverse().ToArray() })
            {
                var ranked = ManualLaunchManifest.RankCandidates(order, title, _ => empty);
                Check(ranked.Count > 0 && ranked[0].Path == expected, $"Wrong default for {title}: {string.Join(", ", ranked.Select(c => c.Path))}");
            }
        }
        First("Resident Evil 9", "re9.exe", "InstallerMessage.exe", "watchdog.exe", "re9.exe", "re8.exe", "steamclient_loader_x64.exe");
        First("Silver River 12", "sr12.exe", "SilverRiver2.exe", "sr12.exe", "SilverRiver12Helper.exe", "random.exe");
        First("Northern Sky IV", "ns4.exe", "ns5.exe", "NorthernSkyEditor.exe", "ns4.exe");
        First("Baldurs Gate 3", "bg3.exe", "BaldursGate3Updater.exe", "bg3.exe", "launcher.exe");
        First("Baldur's Gate 3", "bg3.exe", "bg2.exe", "BaldursGate3Updater.exe", "bg3.exe", "launcher.exe");
        First("Alan Wake 2", "AlanWake2-Win64-Shipping.exe", "AlanWake2Helper.exe", "AlanWake2-Win64-Shipping.exe", "AlanWake2Launcher.exe");
        First("Alan Wake 2", "Wake-Win64-Shipping.exe", "Launcher.exe", "Wake-Win64-Shipping.exe");
        First("Unrelated game", "citron.exe", "citron-cmd.exe", "citron.exe");
        First("Crash Bandicoot", "CrashBandicoot.exe", "CrashReport.exe", "CrashBandicoot.exe", "other.exe");
        First("Example Remastered", "example.exe", "other.exe", "example.exe");
        First("Unrelated adventure", "emu.exe", "QtWebEngineProcess.exe", "shader_tool.exe",
            "emu-room.exe", "emu-cmd.exe", "emu.exe", "netImguiServer.exe", "crs-uploader.exe");
        First("Unrelated adventure", "emu_ea.exe", "emu-room_ea.exe", "emu-cmd_ea.exe", "emu_ea.exe");
        First("Unrelated adventure", "emu.exe", "emu-server.exe", "emu-dedicated.exe", "emu-console.exe", "emu.exe");
        First("Silent Hill 2 Enhanced Edition", "sh2pc.exe", "SH2Config.exe", "sh2pc.exe");
        First("Test game", "Game.exe", "gamelaunchhelper.exe", "CefSharp.BrowserSubprocess.exe",
            "GamingRepairTool.exe", "UnityCrashHandler64.exe", "Game.exe");
        foreach (var helper in new[] { "QtWebEngineProcess.exe", "CefSharp.BrowserSubprocess.exe",
            "shader_tool.exe", "ShaderCompiler.exe", "GamingRepairTool.exe", "SH2Config.exe",
            "netImguiServer.exe", "crs-uploader.exe", "gamelaunchhelper.exe", "UnityCrashHandler64.exe" })
        {
            var helperOnly = ManualLaunchManifest.RankCandidates([new(helper, helper)],
                Path.GetFileNameWithoutExtension(helper), _ => empty);
            Check(helperOnly.Count == 0, $"Helper must not become the fallback default: {helper}");
        }
        var companions = ManualLaunchManifest.RankCandidates(new[] { "emu.exe", "emu-room.exe",
            "emu-server.exe", "emu-dedicated.exe", "emu-console.exe", "emu-cmd.exe" }
            .Select(name => new ManualLaunchManifest.Candidate(name, name)), "Unrelated adventure", _ => empty);
        Check(companions.Select(c => c.Path).ToHashSet().SetEquals(["emu.exe", "emu-console.exe", "emu-cmd.exe"]),
            "Server companions must be excluded while command alternatives remain available.");
        // Counterexamples: generic words and framework branding are not negative proof.
        foreach (var title in new[] { "The Room", "Process", "Shader", "Tool", "Server", "Console", "Repair", "Control" })
            First(title, title + ".exe", title + ".exe");
        First("App", "app.exe", "backup/app.exe", "app.exe");
        var qtGame = ManualLaunchManifest.RankCandidates([new("MyGame.exe", "MyGame.exe")], "My Game",
            _ => new("Qt5", "C++ Application Development Framework", ""));
        Check(qtGame.Count == 1, "Qt branding alone must not exclude an application.");
        var unrelatedRoom = ManualLaunchManifest.RankCandidates(
            [new("other/emu.exe", "other/emu.exe"), new("emu-room.exe", "emu-room.exe")],
            "Emu Room", _ => empty);
        Check(unrelatedRoom.Any(c => c.Path == "emu-room.exe"), "Companion matching must not cross directories.");
        var renamedHelper = ManualLaunchManifest.RankCandidates([new("renamed.exe", "renamed.exe"), new("game.exe", "game.exe")],
            "Game", path => path == "renamed.exe" ? new("Game", "Game", "QtWebEngineProcess.exe") : empty);
        Check(renamedHelper.Count == 1 && renamedHelper[0].Path == "game.exe", "Original filename must identify a renamed helper.");
        var metadataCandidates = new[] { "random.exe", "bh7.exe", "BeyondHorizonHelper.exe", "tool.exe" }
            .Select(name => new ManualLaunchManifest.Candidate(name, name));
        var metadataRank = ManualLaunchManifest.RankCandidates(metadataCandidates, "Beyond Horizon",
            path => path == "bh7.exe" ? new("Beyond Horizon", "Beyond Horizon", "")
                : path == "tool.exe" ? new("Beyond Horizon", "Crash Reporter", "") : empty);
        Check(metadataRank[0].Path == "bh7.exe" && metadataRank.All(c => c.Path != "tool.exe"),
            "Product metadata must identify opaque names without promoting a metadata-labelled utility.");
        var root = Directory.CreateTempSubdirectory("lle-executable-ranking-").FullName;
        try
        {
            var deep = root;
            for (var i = 0; i < 9; i++) deep = Directory.CreateDirectory(Path.Combine(deep, "nested")).FullName;
            var target = Path.Combine(deep, "DeepGame.exe");
            File.WriteAllBytes(target, []);
            // More than the former 1,000-match limit, without executing any fixture.
            for (var i = 0; i < 1005; i++) File.WriteAllBytes(Path.Combine(root, $"extra{i:D4}.exe"), []);
            var found = ManualLaunchManifest.FindCandidates(root, "Deep Game");
            Check(found.Count == 1006 && found[0].Path == target, "Exhaustive search must include deep and late candidates.");
            var artbook = Directory.CreateDirectory(Path.Combine(root, "Digital Artbook")).FullName;
            File.WriteAllBytes(Path.Combine(artbook, "DeepGame.exe"), []);
            var redist = Directory.CreateDirectory(Path.Combine(root, "_Redist")).FullName;
            File.WriteAllBytes(Path.Combine(redist, "DeepGame.exe"), []);
            found = ManualLaunchManifest.FindCandidates(root, "Deep Game", [artbook, redist]);
            Check(found.Count == 1006 && found[0].Path == target, "Support directories must not leak through preferred paths.");
        }
        finally { Directory.Delete(root, recursive: true); }
        Console.WriteLine("Executable ranking: acronyms/numbers, metadata, helper patterns, sibling roles, backup demotion, false-positive counterexamples and exhaustive discovery passed.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
