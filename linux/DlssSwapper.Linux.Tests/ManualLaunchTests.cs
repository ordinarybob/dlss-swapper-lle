using DLSS_Swapper.Data.ManuallyAdded;

namespace DlssSwapper.Linux.Tests;

internal static class ManualLaunchTests
{
    internal static void Run()
    {
        var root = Directory.CreateTempSubdirectory("lle-launch-fixture-");
        try
        {
            var executable = Path.Combine(root.FullName, "My game.exe");
            File.WriteAllText(executable, "fixture, never executed");
            File.WriteAllText(Path.Combine(root.FullName, "setup.exe"), "fixture");
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "Binaries"));
            File.WriteAllText(Path.Combine(nested.FullName, "Game.exe"), "fixture");
            var manifest = ManualLaunchManifest.Validate(executable, "--profile \"A B\"", "");
            if (manifest.Executable != executable || manifest.WorkingDirectory != root.FullName || manifest.Arguments != "--profile \"A B\"") throw new Exception("Launch fields were changed.");
            if (ManualLaunchManifest.Validate(executable, "", nested.FullName).WorkingDirectory != nested.FullName) throw new Exception("Custom working folder lost.");
            var candidates = ManualLaunchManifest.FindCandidates(root.FullName);
            if (candidates.Count != 2 || candidates[0].Path != executable) throw new Exception("Executable suggestions incorrect.");
            var matching = Path.Combine(nested.FullName, "AlanWake2.exe");
            File.WriteAllText(matching, "fixture");
            File.WriteAllText(Path.Combine(root.FullName, "AlanWake2Helper.exe"), "fixture");
            if (ManualLaunchManifest.FindCandidates(root.FullName, "Alan Wake 2")[0].Path != matching)
                throw new Exception("Title match must outrank unrelated root executables and helpers.");
            var deep = root.FullName;
            for (var i = 0; i < 8; i++) deep = Directory.CreateDirectory(Path.Combine(deep, "nested")).FullName;
            var learned = Path.Combine(deep, "Learned.exe");
            File.WriteAllText(learned, "fixture");
            if (ManualLaunchManifest.FindCandidates(root.FullName, "Learned", new[] { deep })[0].Path != learned)
                throw new Exception("Learned directories must be scanned beyond fallback depth.");
            var normalizerUsed = false;
            ManualLaunchManifest.FindCandidates(root.FullName, "Game", normalizeTitle: value => { normalizerUsed = true; return value.ToLowerInvariant(); });
            if (!normalizerUsed) throw new Exception("Existing title normalizer was not used.");
            var partialRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "partial")).FullName;
            var citronRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "citron-test")).FullName;
            var citron = Path.Combine(citronRoot, "citron.exe");
            File.WriteAllText(citron, "fixture");
            File.WriteAllText(Path.Combine(citronRoot, "citron-cmd.exe"), "fixture");
            if (ManualLaunchManifest.FindCandidates(citronRoot, "Unrelated game")[0].Path != citron)
                throw new Exception("Standard executable must outrank its command-line counterpart.");
            var partialFolder = Directory.CreateDirectory(Path.Combine(partialRoot, "nested")).FullName;
            var partial = Path.Combine(partialFolder, "Wake-Win64-Shipping.exe");
            File.WriteAllText(partial, "fixture");
            File.WriteAllText(Path.Combine(partialRoot, "Launcher.exe"), "fixture");
            if (ManualLaunchManifest.FindCandidates(partialRoot, "Alan Wake 2")[0].Path != partial)
                throw new Exception("Partial game-name match must outrank unrelated root executable.");
            var artbook = Directory.CreateDirectory(Path.Combine(partialRoot, "Digital ARTBOOK bonus")).FullName;
            var excluded = new[] { Path.Combine(artbook, "AlanWake2.exe") };
            foreach (var path in excluded) File.WriteAllText(path, "fixture");
            if (ManualLaunchManifest.FindCandidates(partialRoot, "Alan Wake 2", new[] { artbook }).Any(item => excluded.Contains(item.Path)))
                throw new Exception("Excluded executable suggested.");
            foreach (var path in excluded)
            {
                try { ManualLaunchManifest.Validate(path, "", ""); throw new Exception("Excluded executable accepted."); }
                catch (IOException) { }
            }
            foreach (var name in new[] { "SampleGame.exe", "SampleGame-Alternate.exe", "SampleUtility.exe" })
            {
                var path = Path.Combine(partialRoot, name);
                File.WriteAllText(path, "fixture");
                if (ManualLaunchManifest.IsExcluded(path) || ManualLaunchManifest.Validate(path, "", "").Executable != path
                    || !ManualLaunchManifest.FindCandidates(partialRoot).Any(item => item.Path == path))
                    throw new Exception("An ordinary executable filename was rejected.");
            }
            foreach (var invalid in new[] { "relative.exe", Path.Combine(root.FullName, "missing.exe"), Path.Combine(root.FullName, "file.dll") })
            {
                try { ManualLaunchManifest.Validate(invalid, "", ""); throw new Exception("Invalid executable accepted."); }
                catch (IOException) { }
            }
            try { ManualLaunchManifest.Validate(executable, "", Path.Combine(root.FullName, "missing")); throw new Exception("Invalid working folder accepted."); }
            catch (IOException) { }
            if (File.ReadAllText(executable) != "fixture, never executed") throw new Exception("Validation changed game files.");
        }
        finally { root.Delete(true); }
    }
}
