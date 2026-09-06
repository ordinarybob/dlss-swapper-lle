using DlssSwapper.Linux.Cli.Core;
using DLSS_Swapper.Data.ManuallyAdded;

namespace DlssSwapper.Linux.Tests;

internal static class LinuxManualLaunchTests
{
    public static void Run()
    {
        var root = Directory.CreateTempSubdirectory("lle-linux-launch-");
        try
        {
            var executable = Path.Combine(root.FullName, "Citron.EXE");
            File.WriteAllText(executable, "never executed");
            File.WriteAllText(Path.Combine(root.FullName, "citron-cmd.exe"), "never executed");
            if (ManualLaunchManifest.FindCandidates(root.FullName, "Citron")[0].Path != executable)
                throw new Exception("Shared ranking or uppercase EXE discovery failed.");
            var deep = root.FullName;
            for (var i = 0; i < 8; i++) deep = Directory.CreateDirectory(Path.Combine(deep, "deep")).FullName;
            var learnedExecutable = Path.Combine(deep, "LearnedGame.exe");
            File.WriteAllText(learnedExecutable, "never executed");
            File.WriteAllText(Path.Combine(deep, "nvngx_dlss.dll"), "fixture");
            var pattern = Path.GetRelativePath(root.FullName, deep).Replace('\\', '/');
            if (LaunchSuggestions.Find(root.FullName, "Learned Game", [pattern])[0].Path != learnedExecutable)
                throw new Exception("Launch suggestions ignored learned DLL directories.");
            if (OperatingSystem.IsWindows())
            {
                // Format/ranking fixture only; Unix execute permissions require native acceptance.
                var native = Path.Combine(root.FullName, "NativeGame");
                File.WriteAllBytes(native, [0x7f, (byte)'E', (byte)'L', (byte)'F']);
                if (LaunchSuggestions.Find(root.FullName, "Native Game", [])[0].Path != native)
                    throw new Exception("Native title ranking failed.");
                var wine = Path.Combine(root.FullName, "wine"); File.WriteAllText(wine, "#!/bin/sh\n");
                if (LaunchSuggestions.FindWine(root.FullName) != wine || LaunchSuggestions.FindWine("relative") is not null)
                    throw new Exception("Wine path discovery failed.");
            }
            var launch = new ManualGameLaunch(executable, "", ["two words", "$(not-a-shell)", "--literal=\"quotes\""], ManualLaunchKind.Wine,
                Environment.ProcessPath, root.FullName);
            var request = launch.CreateStartInfo();
            if (request.UseShellExecute || request.ArgumentList.Count != 4 || request.ArgumentList[1] != "two words"
                || request.ArgumentList[2] != "$(not-a-shell)" || request.Environment["WINEPREFIX"] != root.FullName)
                throw new Exception("Launch arguments or Wine prefix were changed.");
            Reject(launch with { Runner = null });
            Reject(launch with { Kind = ManualLaunchKind.Native });
            Reject(launch with { Kind = (ManualLaunchKind)42 });
            Reject(launch with { WorkingDirectory = "relative" });
            var store = new LibraryStateStore(Path.Combine(root.FullName, "state"));
            var library = new PersistentLibrary(store);
            library.AddManualGames([root.FullName]);
            ManualLaunchSetupWorkflow.Save(library, root.FullName, launch);
            ManualLaunchSetupWorkflow.RememberOffer(library, false);
            var reopened = new PersistentLibrary(new LibraryStateStore(store.StateDirectory));
            if (!reopened.State.DontShowManualLaunchPrompt || reopened.State.SetupManualLaunchOnImport
                || reopened.State.ManualGames[0].Launch?.Arguments[1] != "$(not-a-shell)")
                throw new Exception("Saved launch details or remembered No did not survive reopening.");
            var before = File.ReadAllBytes(store.StatePath);
            try { ManualLaunchSetupWorkflow.Save(reopened, root.FullName, launch with { Runner = null }); }
            catch (IOException) { }
            if (!before.SequenceEqual(File.ReadAllBytes(store.StatePath))) throw new Exception("Invalid setup changed saved state.");
            ManualLaunchSetupWorkflow.RememberOffer(reopened, true);
            if (!new PersistentLibrary(new LibraryStateStore(store.StateDirectory)).State.SetupManualLaunchOnImport)
                throw new Exception("Remembered Yes did not survive reopening.");
            foreach (var name in new[] { "SampleGame.exe", "SampleUtility.exe" })
            {
                var path = Path.Combine(root.FullName, name); File.WriteAllText(path, "never executed");
                (launch with { Executable = path }).Validate();
            }
            var artbook = Directory.CreateDirectory(Path.Combine(root.FullName, "Artbook")).FullName;
            var artbookExecutable = Path.Combine(artbook, "SampleGame.exe");
            File.WriteAllText(artbookExecutable, "never executed");
            Reject(launch with { Executable = artbookExecutable });
            var serialized = System.Text.Json.JsonSerializer.Serialize(new LinuxLibraryState
            {
                DontShowManualLaunchPrompt = true, SetupManualLaunchOnImport = true,
                ManualGames = [new ManualGameState { Name = "Citron", RootPath = root.FullName, Launch = launch }]
            });
            var state = LibraryStateStore.Normalize(System.Text.Json.JsonSerializer.Deserialize<LinuxLibraryState>(serialized)!);
            if (!state.DontShowManualLaunchPrompt || !state.SetupManualLaunchOnImport || state.ManualGames[0].Launch?.Arguments[0] != "two words")
                throw new Exception("Launch state did not survive normalization and serialization.");
            File.Delete(executable);
            if (LibraryStateStore.Normalize(state).ManualGames[0].Launch is null) throw new Exception("Unavailable launch configuration was discarded.");
            Reject(launch);
        }
        finally { root.Delete(true); }
    }
    private static void Reject(ManualGameLaunch launch)
    {
        try { launch.Validate(); }
        catch (IOException) { return; }
        throw new Exception("Invalid launch configuration accepted.");
    }
}
