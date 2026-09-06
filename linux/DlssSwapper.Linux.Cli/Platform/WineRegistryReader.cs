using System.Text;

namespace DlssSwapper.Linux.Cli.Platform;

public sealed record WineRegistrySection(string Key, IReadOnlyDictionary<string, string> Values);
public sealed record WineRegistryReadResult(IReadOnlyList<WineRegistrySection> Sections, IReadOnlyList<string> Warnings);

public static class WineRegistryReader
{
    public static WineRegistryReadResult Read(string path, Func<string, bool> include, CancellationToken token)
    {
        var sections = new List<WineRegistrySection>(); var warnings = new List<string>();
        if (!File.Exists(path)) return new(sections, warnings);
        try
        {
            if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new IOException("Registry exceeds 64 MiB.");
            using var reader = File.OpenText(path);
            if (reader.ReadLine() != "WINE REGISTRY Version 2") throw new IOException("Unsupported Wine registry format.");
            Dictionary<string, string>? values = null;
            string sectionKey = string.Empty;
            void AddSection()
            {
                if (values is not null) sections.Add(new(sectionKey, values));
            }
            while (reader.ReadLine() is { } raw)
            {
                token.ThrowIfCancellationRequested();
                var line = raw.Trim();
                try
                {
                    if (line.StartsWith('['))
                    {
                        AddSection(); values = null;
                        var index = 1;
                        sectionKey = Decode(line, ref index, ']');
                        if (include(sectionKey)) values = new(StringComparer.OrdinalIgnoreCase);
                    }
                    else if (values is not null && line.StartsWith('"'))
                    {
                        var index = 1;
                        var key = Decode(line, ref index, '"');
                        var remaining = line[index..].TrimStart();
                        if (!remaining.StartsWith('=')) throw new IOException("Malformed registry value.");
                        remaining = remaining[1..].TrimStart();
                        if (!remaining.StartsWith('"')) { values.Remove(key); continue; } // REG_SZ only; discard stale values of another type.
                        index = 1;
                        values[key] = Decode(remaining, ref index, '"');
                    }
                }
                catch (IOException error)
                {
                    warnings.Add($"Malformed Wine registry entry in {path}: {error.Message}");
                    values = null; // Discard this entry, then resume at the next section.
                }
            }
            AddSection();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { warnings.Add($"Could not read Wine registry {path}: {error.Message}"); }
        return new(sections, warnings);
    }

    private static string Decode(string input, ref int index, char terminator)
    {
        var result = new StringBuilder();
        while (index < input.Length)
        {
            var value = input[index++];
            if (value == terminator) return result.ToString();
            if (value == '\\')
            {
                if (index == input.Length) break;
                value = input[index++];
                if (value == 'x' && index < input.Length && char.IsAsciiHexDigit(input[index]))
                {
                    var number = 0; var digits = 0;
                    while (index < input.Length && digits < 4 && char.IsAsciiHexDigit(input[index]))
                    {
                        var digit = input[index++];
                        number = number * 16 + (digit <= '9' ? digit - '0' : char.ToLowerInvariant(digit) - 'a' + 10);
                        digits++;
                    }
                    value = (char)number;
                }
                else if (value is >= '0' and <= '7')
                {
                    var number = value - '0'; var digits = 1;
                    while (index < input.Length && digits < 3 && input[index] is >= '0' and <= '7')
                    { number = number * 8 + input[index++] - '0'; digits++; }
                    value = (char)number;
                }
                else value = value switch
                {
                    'a' => '\a',
                    'b' => '\b',
                    'e' => '\u001b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'v' => '\v',
                    _ => value
                };
            }
            result.Append(value);
        }
        throw new IOException("Unterminated registry string.");
    }
}
