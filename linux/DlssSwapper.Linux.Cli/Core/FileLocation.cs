namespace DlssSwapper.Linux.Cli.Core;

public static class FileLocation
{
    public static string ContainingFolder(string filePath)
    {
        if (!Path.IsPathFullyQualified(filePath)) throw new IOException("A full file path is required.");
        var parent = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (parent is null || !Directory.Exists(parent)) throw new DirectoryNotFoundException("The containing folder no longer exists.");
        return parent;
    }
}
