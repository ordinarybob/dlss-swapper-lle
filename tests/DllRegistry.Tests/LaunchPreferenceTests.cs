using DLSS_Swapper;
using DLSS_Swapper.UserControls;

internal static class LaunchPreferenceTests
{
    public static void Run()
    {
        var settings = Settings.Instance;
        void Check(bool value)
        {
            if (!value) throw new Exception("Remembered launch preference contract failed.");
        }
        foreach (var choice in new[] { false, true })
        {
            settings.DontShowManualLaunchPrompt = false;
            settings.SetupManualLaunchOnImport = !choice;
            settings.SaveResult = false;
            settings.SaveCalls = 0;
            Check(!ManualLaunchSetup.TryRememberChoice(choice, true));
            Check(!settings.DontShowManualLaunchPrompt && settings.SetupManualLaunchOnImport == !choice);
            Check(settings.SaveCalls == 1);
            Check(ManualLaunchSetup.TryRememberChoice(choice, false));
            Check(settings.SaveCalls == 1 && !settings.DontShowManualLaunchPrompt);
            settings.SaveResult = true;
            Check(ManualLaunchSetup.TryRememberChoice(choice, true));
            Check(settings.DontShowManualLaunchPrompt && settings.SetupManualLaunchOnImport == choice);
            Check(settings.SaveCalls == 2);
        }
        Console.WriteLine("Launch preference: Yes/No save failure rollback, continue without remembering and successful retry passed.");
    }
}
