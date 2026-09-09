namespace DLSS_Swapper.UserControls;

internal static partial class ManualLaunchSetup
{
    internal static bool TryRememberChoice(bool choice, bool remember)
    {
        if (!remember) return true;
        var settings = Settings.Instance;
        var previous = (settings.DontShowManualLaunchPrompt, settings.SetupManualLaunchOnImport);
        settings.DontShowManualLaunchPrompt = true;
        settings.SetupManualLaunchOnImport = choice;
        if (settings.SaveJson()) return true;
        (settings.DontShowManualLaunchPrompt, settings.SetupManualLaunchOnImport) = previous;
        return false;
    }
}
