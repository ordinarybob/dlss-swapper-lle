using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;

namespace DlssSwapper.Shared;

// Descriptive metadata only: never loads the DLL or establishes publisher trust.
public sealed record PeVersionInfo(string? FileVersion = null, int FileMajorPart = 0,
    int FileMinorPart = 0, int FileBuildPart = 0, int FilePrivatePart = 0,
    bool IsDebug = false, string? InternalName = null, string? FileDescription = null,
    string? ProductName = null, string? OriginalFilename = null)
{
    public static PeVersionInfo Read(string path)
    {
        // Keep the original Windows language-selection behavior.
        if (OperatingSystem.IsWindows())
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new(info.FileVersion, info.FileMajorPart, info.FileMinorPart, info.FileBuildPart,
                info.FilePrivatePart, info.IsDebug, info.InternalName, info.FileDescription,
                info.ProductName, info.OriginalFilename);
        }
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    // Also usable by tests on Windows, so they exercise the actual Linux reader.
    public static PeVersionInfo Read(Stream stream)
    {
        try
        {
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var resource = pe.PEHeaders.PEHeader?.ResourceTableDirectory ?? default;
            if (resource.RelativeVirtualAddress <= 0 || resource.Size <= 0 || resource.Size > 16 * 1024 * 1024)
                return new();
            var table = pe.GetSectionData(resource.RelativeVirtualAddress).GetContent(0, resource.Size).ToArray();
            uint U32(int offset) { Check(table, offset, 4); return BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset)); }
            List<(uint Id, int Offset, bool Directory)> Entries(int offset)
            {
                Check(table, offset, 16);
                var count = U16(table, offset + 12) + U16(table, offset + 14);
                if (count > 4096) throw new InvalidDataException("Too many version resource entries.");
                Check(table, offset + 16, count * 8);
                var entries = new List<(uint, int, bool)>(count);
                for (var i = 0; i < count; i++)
                {
                    var item = offset + 16 + i * 8;
                    var target = U32(item + 4);
                    entries.Add((U32(item), (int)(target & 0x7fffffff), (target & 0x80000000) != 0));
                }
                return entries;
            }
            var type = Entries(0).FirstOrDefault(entry => entry.Id == 16 && entry.Directory);
            if (type == default) return new();
            foreach (var name in Entries(type.Offset).Where(entry => entry.Directory).OrderBy(entry => entry.Id == 1 ? 0 : 1))
            foreach (var language in Entries(name.Offset).Where(entry => !entry.Directory)
                .OrderBy(entry => entry.Id == 0 ? 0 : entry.Id == 1033 ? 1 : 2).ThenBy(entry => entry.Id))
            {
                Check(table, language.Offset, 16);
                var rva = U32(language.Offset); var size = U32(language.Offset + 4);
                if (rva > int.MaxValue || size == 0 || size > 1024 * 1024) continue;
                var payload = pe.GetSectionData((int)rva).GetContent(0, (int)size).ToArray();
                return Parse(payload);
            }
        }
        catch (Exception error) when (error is BadImageFormatException or InvalidDataException or EndOfStreamException or ArgumentOutOfRangeException)
        {
            // Missing/malformed optional metadata must not invent a version or stop a scan.
        }
        return new();
    }

    private readonly record struct Block(string Key, int Value, int ValueSize, int Children, int End);
    private static int Align(int offset) => (offset + 3) & ~3;
    private static void Check(byte[] data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count)
            throw new InvalidDataException("Truncated version resource.");
    }
    private static ushort U16(byte[] data, int offset)
    { Check(data, offset, 2); return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset)); }
    private static Block ReadBlock(byte[] data, int start, int limit)
    {
        Check(data, start, 6);
        var length = U16(data, start);
        if (length < 6 || start > limit - length) throw new InvalidDataException("Invalid version block length.");
        var end = start + length;
        var keyEnd = start + 6;
        while (keyEnd + 2 <= end && U16(data, keyEnd) != 0) keyEnd += 2;
        if (keyEnd + 2 > end) throw new InvalidDataException("Unterminated version key.");
        var key = Encoding.Unicode.GetString(data, start + 6, keyEnd - start - 6);
        var value = Align(keyEnd + 2);
        var valueSize = U16(data, start + 2) * (U16(data, start + 4) == 1 ? 2 : 1);
        if (valueSize != 0 && value > end - valueSize) throw new InvalidDataException("Invalid version value length.");
        return new(key, value, valueSize, Align(value + valueSize), end);
    }
    private static IEnumerable<Block> Children(byte[] data, Block parent)
    {
        for (var offset = parent.Children; offset + 6 <= parent.End;)
        {
            var child = ReadBlock(data, offset, parent.End);
            yield return child;
            offset = Align(child.End);
        }
    }
    private static PeVersionInfo Parse(byte[] data)
    {
        var root = ReadBlock(data, 0, data.Length);
        if (root.Key != "VS_VERSION_INFO") throw new InvalidDataException("Invalid version resource key.");
        var result = new PeVersionInfo();
        if (root.ValueSize != 0)
        {
            if (root.ValueSize < 52) throw new InvalidDataException("Truncated fixed version info.");
            uint Fixed(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(root.Value + offset, 4));
            if (Fixed(0) != 0xfeef04bd) throw new InvalidDataException("Invalid fixed version signature.");
            var ms = Fixed(8); var ls = Fixed(12);
            result = new($"{ms >> 16}.{ms & 0xffff}.{ls >> 16}.{ls & 0xffff}",
                (int)(ms >> 16), (int)(ms & 0xffff), (int)(ls >> 16), (int)(ls & 0xffff),
                (Fixed(28) & Fixed(32) & 1) != 0);
        }
        var strings = Children(data, root).FirstOrDefault(block => block.Key == "StringFileInfo");
        if (strings == default) return result;
        var selected = Children(data, strings)
            .OrderBy(block => block.Key.StartsWith("0000", StringComparison.Ordinal) ? 0
                : block.Key.StartsWith("0409", StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(block => block.Key, StringComparer.Ordinal).FirstOrDefault();
        if (selected == default) return result;
        foreach (var item in Children(data, selected))
        {
            if (item.ValueSize == 0 || (item.ValueSize & 1) != 0) continue;
            var value = Encoding.Unicode.GetString(data, item.Value, item.ValueSize).TrimEnd('\0');
            result = item.Key switch
            {
                "FileVersion" when !string.IsNullOrWhiteSpace(value) => result with { FileVersion = value },
                "InternalName" => result with { InternalName = value },
                "FileDescription" => result with { FileDescription = value },
                "ProductName" => result with { ProductName = value },
                "OriginalFilename" => result with { OriginalFilename = value },
                _ => result
            };
        }
        return result;
    }
}
