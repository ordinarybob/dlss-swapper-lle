using System.Text.Json;

namespace DlssSwapper.Linux.Cli.Core;

public sealed record DllCatalogEntry(
    DllType Type,
    string Version,
    ulong VersionNumber,
    string Md5,
    string ZipMd5,
    Uri DownloadUri,
    long FileSize,
    long ZipFileSize,
    bool IsSignatureValid,
    bool IsDevFile);

public sealed class DllCatalog
{
    private const long MaximumDllBytes = 512L * 1024 * 1024;
    private const long MaximumZipBytes = 1024L * 1024 * 1024;

    private readonly IReadOnlyDictionary<DllType, IReadOnlyList<DllCatalogEntry>> _entries;
    private readonly IReadOnlyDictionary<(DllType Type, string Md5), string> _knownVersions;

    private DllCatalog(
        IReadOnlyDictionary<DllType, IReadOnlyList<DllCatalogEntry>> entries,
        IReadOnlyDictionary<(DllType Type, string Md5), string> knownVersions)
    {
        _entries = entries;
        _knownVersions = knownVersions;
    }

    public static DllCatalog Load(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        using var stream = File.OpenRead(manifestPath);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Manifest root must be a JSON object.");
        }

        var entries = new Dictionary<DllType, IReadOnlyList<DllCatalogEntry>>();
        var knownVersions = new Dictionary<(DllType Type, string Md5), string>();
        foreach (var definition in DllTypes.All)
        {
            if (!document.RootElement.TryGetProperty(definition.ManifestKey, out var section)
                || section.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    $"Manifest is missing array '{definition.ManifestKey}'.");
            }

            var familyEntries = section.EnumerateArray()
                .Select(item => ParseEntry(definition.Type, item))
                .OrderByDescending(entry => entry.VersionNumber)
                .ThenBy(entry => entry.Md5, StringComparer.Ordinal)
                .ToArray();
            entries.Add(definition.Type, familyEntries);
            foreach (var entry in familyEntries)
            {
                knownVersions.TryAdd((entry.Type, entry.Md5), entry.Version);
            }
        }

        if (document.RootElement.TryGetProperty("known_dlls", out var knownDlls)
            && knownDlls.ValueKind == JsonValueKind.Object)
        {
            foreach (var definition in DllTypes.All)
            {
                if (!knownDlls.TryGetProperty(definition.ManifestKey, out var section)
                    || section.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in section.EnumerateArray())
                {
                    var hash = NormalizeMd5(GetRequiredString(item, "hash"));
                    var version = GetRequiredString(item, "version");
                    knownVersions.TryAdd((definition.Type, hash), version);
                }
            }
        }

        return new DllCatalog(entries, knownVersions);
    }

    public DllCatalogEntry GetLatest(DllType type) =>
        GetEligibleEntries(type).FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"The manifest has no eligible release for {DllTypes.Get(type).DisplayName}.");

    public IReadOnlyList<DllCatalogEntry> GetEntries(DllType type) =>
        GetEligibleEntries(type).ToArray();

    public IReadOnlyList<DllCatalogEntry> GetEntries() =>
        DllTypes.All
            .SelectMany(definition => GetEligibleEntries(definition.Type))
            .ToArray();

    public DllCatalogEntry Resolve(DllType type, string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        var version = selector.Trim();
        string? hashPrefix = null;
        var separator = version.LastIndexOf('@');
        if (separator >= 0)
        {
            hashPrefix = version[(separator + 1)..].Trim();
            version = version[..separator].Trim();
            if (hashPrefix.Length == 0 || hashPrefix.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException(
                    $"Invalid MD5 prefix in version selector '{selector}'.");
            }
        }

        var candidates = GetEligibleEntries(type)
            .Where(entry => entry.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
            .Where(entry => hashPrefix is null
                || entry.Md5.StartsWith(hashPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return candidates.Length switch
        {
            0 => throw new ArgumentException(
                $"No eligible {DllTypes.Get(type).ManifestKey} release matches '{selector}'."),
            1 => candidates[0],
            _ => throw new ArgumentException(
                $"Version '{version}' has multiple builds; use version@MD5-prefix."),
        };
    }

    public bool TryGetKnownVersion(DllType type, string md5, out string version) =>
        _knownVersions.TryGetValue((type, NormalizeMd5(md5)), out version!);

    public static string NormalizeMd5(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 32 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"Invalid MD5 value '{value}'.");
        }

        return normalized;
    }

    private IEnumerable<DllCatalogEntry> GetEligibleEntries(DllType type) =>
        _entries[type].Where(entry => entry.IsSignatureValid && !entry.IsDevFile);

    private static DllCatalogEntry ParseEntry(DllType type, JsonElement item)
    {
        var url = GetRequiredString(item, "download_url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var downloadUri)
            || !downloadUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(downloadUri.UserInfo))
        {
            throw new InvalidDataException($"Manifest download URL is not HTTPS: {url}");
        }

        var fileSize = GetRequiredInt64(item, "file_size");
        var zipFileSize = GetRequiredInt64(item, "zip_file_size");
        if (fileSize > MaximumDllBytes || zipFileSize > MaximumZipBytes)
        {
            throw new InvalidDataException("Manifest payload size exceeds the supported limit.");
        }

        return new DllCatalogEntry(
            type,
            GetRequiredString(item, "version"),
            GetRequiredUInt64(item, "version_number"),
            NormalizeMd5(GetRequiredString(item, "md5_hash")),
            NormalizeMd5(GetRequiredString(item, "zip_md5_hash")),
            downloadUri,
            fileSize,
            zipFileSize,
            GetRequiredBoolean(item, "is_signature_valid"),
            GetRequiredBoolean(item, "is_dev_file"));
    }

    private static string GetRequiredString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Manifest item is missing string '{name}'.");
        }

        return value.GetString()!;
    }

    private static ulong GetRequiredUInt64(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || !value.TryGetUInt64(out var result))
        {
            throw new InvalidDataException($"Manifest item is missing integer '{name}'.");
        }

        return result;
    }

    private static long GetRequiredInt64(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)
            || !value.TryGetInt64(out var result)
            || result <= 0)
        {
            throw new InvalidDataException($"Manifest item has invalid integer '{name}'.");
        }

        return result;
    }

    private static bool GetRequiredBoolean(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"Manifest item is missing boolean '{name}'.");
        }

        return value.GetBoolean();
    }
}
