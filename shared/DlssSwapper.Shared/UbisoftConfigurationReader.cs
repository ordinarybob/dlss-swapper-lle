namespace DlssSwapper.Shared;

public sealed record UbisoftConfigurationRecord(uint InstallId, uint LaunchId, int Offset, int Length);
public sealed record UbisoftConfigurationReadResult(IReadOnlyList<UbisoftConfigurationRecord> Records, IReadOnlyList<string> Warnings);

// Reads length-delimited records without interpreting YAML or accessing launcher files.
public static class UbisoftConfigurationReader
{
    public static UbisoftConfigurationReadResult Read(ReadOnlySpan<byte> data)
    {
        var records = new List<UbisoftConfigurationRecord>();
        var warnings = new List<string>();
        var position = 0;
        while (position < data.Length)
        {
            var start = position;
            int end;
            try
            {
                if (Number(data, ref position) != 10) throw new InvalidDataException("Expected a configuration record.");
                end = End(data, ref position);
            }
            catch (InvalidDataException error)
            {
                warnings.Add($"Configuration framing at byte {start}: {error.Message}");
                break; // No reliable next boundary; do not guess at bytes inside YAML.
            }
            try
            {
                var record = data[..end];
                uint? installId = null; uint launchId = 0;
                var yamlOffset = -1; var yamlLength = 0;
                while (position < end)
                {
                    var tag = Number(record, ref position);
                    switch (tag)
                    {
                        case 8: installId = Number(record, ref position); break;
                        case 16: launchId = Number(record, ref position); break;
                        case 26:
                            var yamlEnd = End(record, ref position);
                            yamlOffset = position; yamlLength = yamlEnd - position; position = yamlEnd;
                            break;
                        default:
                            if (tag >> 3 == 0) throw new InvalidDataException("Invalid field number.");
                            switch (tag & 7)
                            {
                                case 0: Number(record, ref position); break;
                                case 2: position = End(record, ref position); break;
                                case 1: Skip(record, ref position, 8); break;
                                case 5: Skip(record, ref position, 4); break;
                                default: throw new InvalidDataException("Unsupported field encoding.");
                            }
                            break;
                    }
                }
                if (installId is null || yamlOffset < 0) throw new InvalidDataException("Missing installation ID or YAML payload.");
                records.Add(new(installId.Value, launchId == 0 ? installId.Value : launchId, yamlOffset, yamlLength));
            }
            catch (InvalidDataException error) { warnings.Add($"Configuration record at byte {start}: {error.Message}"); }
            position = end;
        }
        return new(records, warnings);
    }

    private static int End(ReadOnlySpan<byte> data, ref int position)
    {
        var length = Number(data, ref position);
        if (length > data.Length - position) throw new InvalidDataException("Truncated payload.");
        return position + (int)length;
    }

    private static void Skip(ReadOnlySpan<byte> data, ref int position, int length)
    {
        if (data.Length - position < length) throw new InvalidDataException("Truncated field.");
        position += length;
    }

    private static uint Number(ReadOnlySpan<byte> data, ref int position)
    {
        uint number = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            if (position == data.Length) throw new InvalidDataException("Truncated integer.");
            var next = data[position++];
            if (shift == 28 && next > 15) throw new InvalidDataException("Integer overflow.");
            number |= (uint)(next & 127) << shift;
            if ((next & 128) == 0) return number;
        }
        throw new InvalidDataException("Invalid integer.");
    }
}
