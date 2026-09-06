using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineComponentDescriptionTests
{
    internal static void Run()
    {
        var descriptions = new HashSet<string>(StringComparer.Ordinal);
        var fallback = StreamlineComponentDescriptions.GetDescription("unknown.dll");
        foreach (var fileName in StreamlineComponentSet.FileNames)
        {
            var description = StreamlineComponentDescriptions.GetDescription(fileName);
            if (string.IsNullOrWhiteSpace(description) || description == fallback || !descriptions.Add(description))
                throw new InvalidOperationException($"Missing or duplicate component description: {fileName}.");
            if (StreamlineComponentDescriptions.GetDescription(fileName.ToUpperInvariant()) != description)
                throw new InvalidOperationException($"Component description lookup is case-sensitive: {fileName}.");
        }
        if (descriptions.Count != 11)
            throw new InvalidOperationException("Expected descriptions for all 11 supported Streamline components.");
    }
}
