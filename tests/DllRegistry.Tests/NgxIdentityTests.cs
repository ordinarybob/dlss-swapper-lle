using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using System.Buffers.Binary;

internal static class NgxIdentityTests
{
    public static void Run()
    {
        static byte[] Header(ushort machine = 0x8664, ushort magic = 0x20b, bool dll = true)
        {
            var bytes = new byte[512];
            void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
            Word(0, 0x5a4d);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c), 0x80);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x80), 0x4550);
            Word(0x84, machine);
            Word(0x94, magic == 0x20b ? (ushort)240 : (ushort)224);
            Word(0x96, dll ? (ushort)0x2002 : (ushort)2);
            Word(0x98, magic);
            return bytes;
        }
        static bool Match(byte[] bytes, string? name, GameAssetType family)
        {
            using var stream = new MemoryStream(bytes);
            var result = NgxPayloadIdentity.Matches(stream, name, family);
            if (!stream.CanRead) throw new Exception("Identity check closed caller stream.");
            return result;
        }
        void Check(bool value)
        {
            if (!value) throw new Exception("NGX identity contract failed.");
        }
        var valid = Header();
        Check(Match(valid, "NVIDIA Deep Learning SuperSampling", GameAssetType.DLSS));
        Check(Match(valid, "NGX DL SuperSampling", GameAssetType.DLSS));
        Check(Match(valid, "NVIDIA DLSS Ray Reconstruction", GameAssetType.DLSS_D));
        Check(Match(valid, "NVIDIA DLSS-G MFGLW", GameAssetType.DLSS_G));
        Check(!Match(valid, "unrelated", GameAssetType.DLSS));
        Check(!Match(valid, null, GameAssetType.DLSS));
        Check(!Match(valid, "NVIDIA DLSS-G MFGLW", GameAssetType.DLSS));
        Check(!Match(valid, "NGX DL SuperSampling", GameAssetType.XeSS));
        Check(!Match(Header(0x14c, 0x10b), "NGX DL SuperSampling", GameAssetType.DLSS));
        Check(!Match(Header(0xaa64), "NGX DL SuperSampling", GameAssetType.DLSS));
        Check(!Match(Header(dll: false), "NGX DL SuperSampling", GameAssetType.DLSS));
        Check(!Match([1, 2, 3], "NGX DL SuperSampling", GameAssetType.DLSS));
        Console.WriteLine("NGX identity: supported families, unknown/wrong products, x86/ARM64, executable and malformed PE rejection passed.");
    }
}
