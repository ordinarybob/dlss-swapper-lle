namespace DlssSwapper.Shared;

/// <summary>Platform-neutral pattern catalog and exact-depth wildcard rules.
/// Filesystem enumeration and input normalization remain platform adapters.</summary>
public static class ScanPatternRules
{
    private static readonly string[] DirectoryPatterns =
    [
        "",
        "*",
        "*/*",
        "*/*/win64",
        "*/binaries/*",
        "*/windows/x64",
        "*/x64/dxr",
        "*/x64/dxr2",
        "*/x64/slinfo",
        "*/binaries/win64r/amd_fidelityfx",
        "*/binaries/win64r/streamline",
        "*/binaries/win64r/xess",
        "*/tscgame/binaries/win64",
        "*/intel-xess-feature-test/bin/x64/xess_1_0",
        "*/intel-xess-feature-test/bin/x64/xess_1_1",
        "*/intel-xess-feature-test/bin/x64/xess_1_2",
        "*/intel-xess-feature-test/bin/x64/xess_1_3",
        "*/nvidia-dlss-test/nvidia-dlss-test-1/bin/x64",
        "*/nvidia-dlss-test/nvidia-dlss-test-2/bin/x64",
        "*/nvidia-dlss-test/nvidia-dlss-test-3/bin/x64",
        "*/nvidia-dlss-test/nvidia-dlss-test-4/bin/x64",
        "*/*/dlss/binaries/thirdparty/win64",
        "*/*/streamline/binaries/thirdparty/win64",
        "*/*/thirdparty/nvidia/ngx/win64",
        "*/*/xess/binaries/thirdparty/win64",
        "*/binaries/*/nvidia/ngx/win64",
        "*/binaries/thirdparty/*/ngx/win64",
        "*/binaries/thirdparty/nvidia/*/win64",
        "*/binaries/thirdparty/nvidia/ngx/*",
        "*/plugins/*/binaries/thirdparty/win64",
        "*/plugins/dlss/*/thirdparty/win64",
        "*/plugins/dlss/binaries/*/win64",
        "*/plugins/dlss/binaries/thirdparty/*",
        "*/plugins/streamline/*/thirdparty/win64",
        "*/plugins/streamline/binaries/*/win64",
        "*/plugins/streamline/binaries/thirdparty/*",
        "*/plugins/xess/*/thirdparty/win64",
        "*/plugins/xess/binaries/*/win64",
        "*/plugins/xess/binaries/thirdparty/*",
        "*/*/marketplace/dlss/binaries/thirdparty/win64",
        "*/*/marketplace/xess/binaries/thirdparty/win64",
        "*/*/nvidia/dlss/binaries/thirdparty/win64",
        "*/plugins/*/dlss/binaries/thirdparty/win64",
        "*/plugins/*/streamline/binaries/thirdparty/win64",
        "*/plugins/*/xess/binaries/thirdparty/win64",
        "*/plugins/dlss/binaries/thirdparty/win64/development",
        "*/plugins/dlssplugin/streamlinecore/binaries/thirdparty/win64",
        "*/plugins/marketplace/*/binaries/thirdparty/win64",
        "*/plugins/marketplace/dlss/*/thirdparty/win64",
        "*/plugins/marketplace/dlss/binaries/*/win64",
        "*/plugins/marketplace/dlss/binaries/thirdparty/*",
        "*/plugins/marketplace/xess/*/thirdparty/win64",
        "*/plugins/marketplace/xess/binaries/*/win64",
        "*/plugins/marketplace/xess/binaries/thirdparty/*",
        "*/plugins/nvidia/*/binaries/thirdparty/win64",
        "*/plugins/nvidia/dlss/*/thirdparty/win64",
        "*/plugins/nvidia/dlss/binaries/*/win64",
        "*/plugins/nvidia/dlss/binaries/thirdparty/*",
        "*/*/runtime/intel/xess/binaries/thirdparty/win64",
        "*/*/runtime/nvidia/dlss/binaries/thirdparty/win64",
        "*/*/runtime/nvidia/streamline/binaries/thirdparty/win64",
        "*/hmdproject/plugins/nvidia/dlss/binaries/thirdparty/win64",
        "*/plugins/*/intel/xess/binaries/thirdparty/win64",
        "*/plugins/*/nvidia/dlss/binaries/thirdparty/win64",
        "*/plugins/*/nvidia/streamline/binaries/thirdparty/win64",
        "*/plugins/runtime/*/dlss/binaries/thirdparty/win64",
        "*/plugins/runtime/*/streamline/binaries/thirdparty/win64",
        "*/plugins/runtime/*/xess/binaries/thirdparty/win64",
        "*/plugins/runtime/intel/*/binaries/thirdparty/win64",
        "*/plugins/runtime/intel/xess/*/thirdparty/win64",
        "*/plugins/runtime/intel/xess/binaries/*/win64",
        "*/plugins/runtime/intel/xess/binaries/thirdparty/*",
        "*/plugins/runtime/nvidia/*/binaries/thirdparty/win64",
        "*/plugins/runtime/nvidia/dlss/*/thirdparty/win64",
        "*/plugins/runtime/nvidia/dlss/binaries/*/win64",
        "*/plugins/runtime/nvidia/dlss/binaries/thirdparty/*",
        "*/plugins/runtime/nvidia/streamline/*/thirdparty/win64",
        "*/plugins/runtime/nvidia/streamline/binaries/*/win64",
        "*/plugins/runtime/nvidia/streamline/binaries/thirdparty/*",
        "*/engine/plugins/*/nvidia/dlss/binaries/thirdparty/win64",
        "*/engine/plugins/marketplace/*/dlss/binaries/thirdparty/win64",
        "*/engine/plugins/marketplace/*/streamline/binaries/thirdparty/win64",
    ];


    public static IReadOnlyList<string> BuiltInPatterns { get; } = Array.AsReadOnly(DirectoryPatterns);

    public static string[] RemoveSubsumed(IEnumerable<string> patterns, Func<string, string?> normalize, char separator)
    {
        var distinct = patterns.Select(normalize).Where(pattern => pattern is not null)
            .Select(pattern => pattern!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return distinct.Where(pattern => !distinct.Any(other =>
            !other.Equals(pattern, StringComparison.OrdinalIgnoreCase)
            && Subsumes(other, pattern, separator))).ToArray();
    }

    private static bool Subsumes(string broader, string narrower, char separator)
    {
        var broad = broader.Length == 0 ? [] : broader.Split(separator);
        var narrow = narrower.Length == 0 ? [] : narrower.Split(separator);
        return broad.Length == narrow.Length && broad.Zip(narrow).All(pair =>
            pair.First == "*" || pair.First.Equals(pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryCreateAdaptivePattern(string relativeDirectory, char separator, bool lowerCase, out string pattern)
    {
        pattern = string.Empty;
        if (relativeDirectory == ".") return true;
        var components = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0 || components.Any(component => component is "." or "..")) return false;
        components[0] = "*";
        pattern = string.Join(separator, components);
        if (lowerCase) pattern = pattern.ToLowerInvariant();
        return true;
    }
}
