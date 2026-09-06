using System.Numerics;
using System.Text.RegularExpressions;

namespace DlssSwapper.Shared;

/// <summary>Orders displayed DLL versions by numeric components, without integer overflow.</summary>
public sealed class VersionTextComparer : IComparer<string?>
{
    public static VersionTextComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        var a = Components(left);
        var b = Components(right);
        if (a.Length == 0 || b.Length == 0)
            return a.Length == b.Length ? StringComparer.OrdinalIgnoreCase.Compare(left, right) : a.Length.CompareTo(b.Length);
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var result = (i < a.Length ? a[i] : BigInteger.Zero).CompareTo(i < b.Length ? b[i] : BigInteger.Zero);
            if (result != 0) return result;
        }
        return 0;
    }

    private static BigInteger[] Components(string? text) =>
        Regex.Matches(text ?? string.Empty, @"\d+")
            .Select(match => BigInteger.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
}
