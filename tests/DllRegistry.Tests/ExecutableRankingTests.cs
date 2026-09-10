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
        Console.WriteLine("Executable ranking: general acronyms/numbers, metadata, tools, launchers, command variants, deep/large trees and exclusions passed.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
