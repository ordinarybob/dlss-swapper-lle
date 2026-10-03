using System.Buffers.Binary;
using System.Text;
using DlssSwapper.Shared;

namespace DlssSwapper.Linux.Tests;

internal static class PeVersionInfoTests
{
    internal static void Run()
    {
        foreach (var pe64 in new[] { false, true })
        {
            var data = Fixture(pe64);
            var info = PeVersionInfo.Read(new MemoryStream(data));
            Check(info.FileVersion == "310,9,1,0" && info.FileMajorPart == 310 && info.FileMinorPart == 9
                && info.FileBuildPart == 1 && info.FilePrivatePart == 0 && info.IsDebug,
                "PE version numbers, string or debug flag lost");
            Check(info.ProductName == "NVIDIA DLSS" && info.FileDescription == "Unicode Δ description"
                && info.InternalName == "fixture.dll" && info.OriginalFilename == "original.dll", "PE descriptive metadata lost");
            Check(data.SequenceEqual(Fixture(pe64)), "Metadata reader changed source bytes");
            var fixedOnly = Fixture(pe64, strings: false);
            Check(PeVersionInfo.Read(new MemoryStream(fixedOnly)).FileVersion == "310.9.1.0", "Fixed version fallback lost");
            foreach (var offset in new[] { 512 + 20, 512 + 44, 512 + 76 })
            {
                var broken = data.ToArray();
                BinaryPrimitives.WriteUInt32LittleEndian(broken.AsSpan(offset), uint.MaxValue);
                Check(PeVersionInfo.Read(new MemoryStream(broken)).FileVersion is null, "Invalid resource bounds accepted");
            }
            var truncatedBlock = data.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(truncatedBlock.AsSpan(640), ushort.MaxValue);
            Check(PeVersionInfo.Read(new MemoryStream(truncatedBlock)).FileVersion is null, "Invalid version block accepted");
            var noResource = data.ToArray();
            noResource.AsSpan(152 + (pe64 ? 128 : 112), 8).Clear();
            Check(PeVersionInfo.Read(new MemoryStream(noResource)).FileVersion is null, "Missing metadata invented a version");
        }
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[] { 1, 2, 3 }, Fixture(true)[..200] })
            Check(PeVersionInfo.Read(new MemoryStream(bytes)).FileVersion is null, "Truncated/non-PE file accepted");
    }

    // A real PE resource tree, not managed assembly metadata or a mocked reader.
    private static byte[] Fixture(bool pe64, bool strings = true)
    {
        byte[] Text(string value) => Encoding.Unicode.GetBytes(value + '\0');
        var fixedInfo = new byte[52];
        void Fixed(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(offset), value);
        Fixed(0, 0xfeef04bd); Fixed(4, 0x10000); Fixed(8, (310u << 16) | 9); Fixed(12, 1u << 16);
        Fixed(28, 1); Fixed(32, 1);
        var stringBlocks = new[] { ("FileVersion", "310,9,1,0"), ("ProductName", "NVIDIA DLSS"),
            ("FileDescription", "Unicode Δ description"), ("InternalName", "fixture.dll"), ("OriginalFilename", "original.dll") }
            .Select(pair => Block(pair.Item1, Text(pair.Item2), true)).ToArray();
        var payload = Block("VS_VERSION_INFO", fixedInfo, false, strings
            ? [Block("StringFileInfo", [], true, Block("040904b0", [], true, stringBlocks))] : []);
        var data = new byte[2048];
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
        U16(0, 0x5a4d); U32(60, 128); U32(128, 0x4550);
        U16(132, pe64 ? (ushort)0x8664 : (ushort)0x14c); U16(134, 1);
        U16(148, pe64 ? (ushort)240 : (ushort)224); U16(150, 0x2022);
        const int optional = 152;
        U16(optional, pe64 ? (ushort)0x20b : (ushort)0x10b); U32(optional + 32, 4096); U32(optional + 36, 512);
        U32(optional + 56, 8192); U32(optional + 60, 512); U32(optional + (pe64 ? 108 : 92), 16);
        U32(optional + (pe64 ? 128 : 112), 4096); U32(optional + (pe64 ? 132 : 116), 1536);
        var section = optional + (pe64 ? 240 : 224);
        ".rsrc"u8.CopyTo(data.AsSpan(section)); U32(section + 8, 1536); U32(section + 12, 4096);
        U32(section + 16, 1536); U32(section + 20, 512); U32(section + 36, 0x40000040);
        U16(526, 1); U32(528, 16); U32(532, 0x80000018);
        U16(550, 1); U32(552, 1); U32(556, 0x80000030);
        U16(574, 1); U32(576, 1033); U32(580, 72);
        U32(584, 4096 + 128); U32(588, (uint)payload.Length);
        payload.CopyTo(data, 640);
        return data;
    }
    private static byte[] Block(string key, byte[] value, bool text, params byte[][] children)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.Unicode, true);
        writer.Write((ushort)0); writer.Write((ushort)(text ? value.Length / 2 : value.Length)); writer.Write((ushort)(text ? 1 : 0));
        writer.Write(Encoding.Unicode.GetBytes(key + '\0'));
        void Align() { while (stream.Position % 4 != 0) writer.Write((byte)0); }
        Align(); writer.Write(value);
        foreach (var child in children) { Align(); writer.Write(child); }
        var result = stream.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)result.Length); return result;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
