using System.Globalization;

namespace DlssSwapper.Shared;

public sealed record WindowsIconReference(string Path, int Index)
{
    public static WindowsIconReference Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new FormatException("Icon reference is empty or contains control characters.");
        value = value.Trim();
        string path; string suffix;
        if (value.StartsWith('"'))
        {
            var close = value.IndexOf('"', 1);
            if (close < 0) throw new FormatException("Icon path has an unmatched quote.");
            path = value[1..close]; suffix = value[(close + 1)..].Trim();
        }
        else
        {
            var comma = value.LastIndexOf(',');
            if (comma >= 0 && int.TryParse(value[(comma + 1)..].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            { path = value[..comma].TrimEnd(); suffix = value[comma..]; }
            else { path = value; suffix = ""; }
        }
        if (string.IsNullOrWhiteSpace(path) || path.Contains('"')) throw new FormatException("Icon path is invalid.");
        var index = 0;
        if (suffix.Length > 0 && (!suffix.StartsWith(',')
            || !int.TryParse(suffix[1..].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out index)))
            throw new FormatException("Icon resource index is invalid.");
        return new(path, index);
    }
}
