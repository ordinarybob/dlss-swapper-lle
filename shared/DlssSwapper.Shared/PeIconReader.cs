using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace DlssSwapper.Shared;

// Reads PE resources as data only. Never loads a module or executes its code.
public static class PeIconReader
{
    private const int Limit = 16 * 1024 * 1024;
    private sealed record Entry(uint Id, int Offset, bool Directory);

    public static byte[] Extract(Stream stream, int index = 0)
    {
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var resource = pe.PEHeaders.PEHeader?.ResourceTableDirectory ?? default;
        if (resource.RelativeVirtualAddress <= 0 || resource.Size <= 0 || resource.Size > Limit)
            throw new InvalidDataException("Executable has no supported icon resource directory.");
        var table = pe.GetSectionData(resource.RelativeVirtualAddress).GetContent(0, resource.Size).ToArray();
        ushort U16(int offset) { Check(offset, 2); return BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(offset)); }
        uint U32(int offset) { Check(offset, 4); return BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset)); }
        void Check(int offset, int count)
        { if (offset < 0 || count < 0 || offset > table.Length - count) throw new InvalidDataException("Icon resource is truncated."); }
        List<Entry> Entries(int offset)
        {
            Check(offset, 16);
            var count = U16(offset + 12) + U16(offset + 14);
            if (count > 4096) throw new InvalidDataException("Icon resource directory has too many entries.");
            Check(offset + 16, count * 8);
            var entries = new List<Entry>(count);
            for (var i = 0; i < count; i++)
            {
                var item = offset + 16 + i * 8; var target = U32(item + 4);
                entries.Add(new(U32(item), (int)(target & 0x7fffffff), (target & 0x80000000) != 0));
            }
            return entries;
        }
        int Directory(Entry? entry) => entry is { Directory: true } ? entry.Offset
            : throw new InvalidDataException("Icon resource directory is missing.");
        byte[] Payload(Entry entry)
        {
            var languages = Entries(Directory(entry));
            var leaf = languages.FirstOrDefault(item => item.Id == 0) ?? languages.FirstOrDefault(item => item.Id == 1033)
                ?? languages.FirstOrDefault() ?? throw new InvalidDataException("Icon language resource is missing.");
            if (leaf.Directory) throw new InvalidDataException("Invalid icon resource depth.");
            Check(leaf.Offset, 16);
            var rva = U32(leaf.Offset); var length = U32(leaf.Offset + 4);
            if (rva > int.MaxValue || length == 0 || length > Limit) throw new InvalidDataException("Invalid icon payload bounds.");
            return pe.GetSectionData((int)rva).GetContent(0, (int)length).ToArray();
        }
        var types = Entries(0);
        var groups = Entries(Directory(types.FirstOrDefault(entry => entry.Id == 14)));
        var selected = index < 0 ? groups.FirstOrDefault(entry => entry.Id == -(long)index)
            : index < groups.Count ? groups[index] : null;
        if (selected is null) throw new InvalidDataException("Requested executable icon was not found.");
        var group = Payload(selected);
        if (group.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(group) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(group.AsSpan(2)) != 1) throw new InvalidDataException("Invalid icon group header.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(group.AsSpan(4));
        if (count == 0 || count > 256 || group.Length < 6 + count * 14) throw new InvalidDataException("Invalid icon group length.");
        var icons = Entries(Directory(types.FirstOrDefault(entry => entry.Id == 3)));
        var images = new List<byte[]>(count); var lengthTotal = 6 + count * 16;
        for (var i = 0; i < count; i++)
        {
            var offset = 6 + i * 14;
            var id = BinaryPrimitives.ReadUInt16LittleEndian(group.AsSpan(offset + 12));
            var image = Payload(icons.FirstOrDefault(entry => entry.Id == id)
                ?? throw new InvalidDataException("Icon group references a missing image."));
            if (image.Length != BinaryPrimitives.ReadUInt32LittleEndian(group.AsSpan(offset + 8)) || image.Length > Limit - lengthTotal)
                throw new InvalidDataException("Icon image length is invalid.");
            images.Add(image); lengthTotal += image.Length;
        }
        var ico = new byte[lengthTotal]; group.AsSpan(0, 6).CopyTo(ico);
        var imageOffset = 6 + count * 16;
        for (var i = 0; i < count; i++)
        {
            group.AsSpan(6 + i * 14, 12).CopyTo(ico.AsSpan(6 + i * 16));
            BinaryPrimitives.WriteUInt32LittleEndian(ico.AsSpan(6 + i * 16 + 12), (uint)imageOffset);
            images[i].CopyTo(ico, imageOffset); imageOffset += images[i].Length;
        }
        return ico;
    }
}
