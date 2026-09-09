using System;
using System.IO;
using System.Security.Cryptography;

namespace DLSS_Swapper.Helpers;

// A batch may reuse validation only while this read handle prevents source
// replacement/writes. Every destination is still independently staged and hashed.
internal sealed class VerifiedDllSource : IDisposable
{
    readonly FileStream _source;
    readonly string _path;
    readonly string _md5;
    readonly bool _signatureVerified;
    bool _disposed;

    public VerifiedDllSource(string path, string expectedMd5, bool requireSignature, Func<string, bool> verifySignature)
    {
        _path = Path.GetFullPath(path);
        _source = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            _md5 = Convert.ToHexString(MD5.HashData(_source));
            if (!_md5.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Unable to swap DLL because the source hash is invalid.");
            if (requireSignature)
            {
                if (!verifySignature(_path))
                    throw new IOException("Unable to swap DLL because its signature could not be verified.");
                _signatureVerified = true;
            }
        }
        catch { _source.Dispose(); throw; }
    }

    public bool Matches(string path, string expectedMd5, bool requireSignature) =>
        !_disposed && (!requireSignature || _signatureVerified)
        && _path.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
        && _md5.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _disposed = true;
        _source.Dispose();
    }
}
