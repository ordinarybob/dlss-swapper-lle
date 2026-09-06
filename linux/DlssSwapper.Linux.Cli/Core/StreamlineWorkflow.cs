using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Cli.Core;

/// <summary>Experimental explicit-game adapter. All writes use the shared component-set engine.</summary>
public static class StreamlineWorkflow
{
    public static StreamlineComponentOperationResult Execute(
        string gameRoot, string action, string? packageDirectory = null, bool dryRun = false, Translations? translations = null)
    {
        if (action is not ("inspect" or "update" or "restore" or "recover"))
            throw new ArgumentException("Unknown Streamline action.", nameof(action));
        var root = ValidateGameRoot(gameRoot);

        var installed = StreamlineComponentSet.FindInstalled(root);
        var pending = StreamlineComponentSet.HasPendingRecovery(root);
        if (action == "inspect")
            return new(true, installed.Count, Describe(root, installed, pending));
        if (dryRun)
        {
            // Do not validate/download/extract packages or acquire mutation locks on this path.
            return new(true, installed.Count,
                $"Dry run: Streamline {action}; no files changed.\n{Describe(root, installed, pending)}");
        }
        if (action == "recover")
        {
            var result = StreamlineComponentSet.RecoverInterrupted(root);
            if (translations is null || !result.Success) return result;
            return result with { Message = result.Message switch
            {
                "No interrupted Streamline operation was found." => translations.Get("Linux_StreamlineNoRecovery", result.Message),
                "Interrupted Streamline operations recovered." => translations.Get("Linux_StreamlineRecovered", result.Message),
                _ => result.Message,
            } };
        }
        if (pending) return new(false, 0, "An interrupted Streamline operation needs recovery before update or restore.");
        if (action == "restore") return StreamlineComponentSet.RestoreOriginals(installed);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        StreamlineComponentSet.ValidatePackage(packageDirectory);
        return StreamlineComponentSet.UpdateExisting(packageDirectory, installed);
    }

    public static string ValidateGameRoot(string gameRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if (root == Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!))
            throw new ArgumentException("Select a game installation folder, not a filesystem root.");
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException("Select the real game installation folder, not a symbolic link.");

        return root;
    }

    public static StreamlinePreviewSnapshot Preview(string gameRoot, string? packageDirectory)
    {
        var root = ValidateGameRoot(gameRoot);
        return StreamlineDecisionPreview.Create(root, StreamlineComponentSet.FindInstalled(root), packageDirectory);
    }

    public static StreamlineComponentOperationResult ApplySelection(string gameRoot, string? packageDirectory,
        StreamlinePreviewSnapshot confirmed, bool restore, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var root = ValidateGameRoot(gameRoot);
        if (StreamlineComponentSet.HasPendingRecovery(root))
            return new(false, 0, T("Linux_StreamlineRecoveryRequired", "Recover the interrupted operation before applying or restoring files."));
        var targets = confirmed.Components.Select(item => item.TargetPath).ToArray();
        if (targets.Length == 0) return new(false, 0, T("Linux_StreamlineSelectComponent", "Select at least one component."));
        var fresh = Preview(root, packageDirectory).SelectTargets(targets);
        if (!confirmed.IsEquivalentTo(fresh, restore))
            return new(false, 0, T("Linux_StreamlineFilesChanged", "Files changed since confirmation. Review the refreshed versions and try again."));
        if (restore)
        {
            if (!fresh.CanRestore) return new(false, 0, T("Linux_StreamlineNoRestorable", "No selected originals can be restored, or a source is unavailable."));
            return LocalizeResult(StreamlineComponentSet.RestoreOriginals(targets, expectedPreview: confirmed), true, translations);
        }
        if (!fresh.CanUpdate) return new(false, 0, T("Linux_StreamlineNoUpdatable", "No selected files need updating, or a source is unavailable."));
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        StreamlineComponentSet.ValidatePackage(packageDirectory);
        return LocalizeResult(StreamlineComponentSet.UpdateExisting(packageDirectory, targets, expectedPreview: confirmed), false, translations);
    }

    private static StreamlineComponentOperationResult LocalizeResult(StreamlineComponentOperationResult result,
        bool restore, Translations? translations)
    {
        // Shared engine diagnostics retain their original detail; successful operation counts
        // come from the engine rather than being inferred from translated message text.
        if (translations is null || !result.Success) return result;
        return result with { Message = result.ComponentCount == 0
            ? translations.Get("Linux_StreamlineNoChanges", "All Streamline components already match; no files changed.")
            : translations.Format(restore ? "Linux_StreamlineRestored" : "Linux_StreamlineUpdated",
                restore ? "Restored {0} existing Streamline component(s)." : "Updated {0} existing Streamline component(s).", result.ComponentCount) };
    }

    private static string Describe(string root, IReadOnlyList<string> installed, bool pending) =>
        $"Installed Streamline components: {installed.Count}. Recovery pending: {pending}.\n"
        + string.Join('\n', installed.Select(path => Path.GetRelativePath(root, path)
            + (File.Exists(path + StreamlineComponentSet.BackupSuffix) ? " (original backup available)" : "")));
}
