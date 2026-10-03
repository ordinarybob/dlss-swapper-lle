using System.Reflection.PortableExecutable;

namespace DlssSwapper.Linux.Cli.Core;

public sealed class DllImportWorkflow(DllCatalog catalog, DownloadCache cache, PersistentLibrary library,
    IDllSignatureVerifier? signatureVerifier = null, Translations? translations = null)
{
    private string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);

    public Task<string> ImportAsync(string name, Stream contents, CancellationToken token) =>
        ImportCoreAsync(name, contents, false, token);

    public async Task<string> ImportModelAsync(string path, CancellationToken token, string? expectedMd5 = null)
    {
        token.ThrowIfCancellationRequested();
        await using var contents = File.OpenRead(path);
        if (contents.Length > 512L * 1024 * 1024) throw new IOException(T("Linux_ImportModelTooLarge", "Model exceeds the supported DLL size limit."));
        return await ImportCoreAsync("", contents, true, token, expectedMd5).ConfigureAwait(false);
    }

    private async Task<string> ImportCoreAsync(string name, Stream contents, bool model, CancellationToken token, string? expectedMd5 = null)
    {
        token.ThrowIfCancellationRequested();
        DllTypes.TryFromFileName(name, out var family);
        if (!model && family is null) throw new IOException(T("Linux_ImportUnsupportedFamily", "Unsupported DLL family."));
        using (var pe = new PEReader(contents, PEStreamOptions.LeaveOpen))
        {
            if (pe.PEHeaders.PEHeader is null || pe.PEHeaders.CoffHeader.Machine != Machine.Amd64
                || (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) == 0
                || pe.PEHeaders.PEHeader.Magic != PEMagic.PE32Plus)
                throw new IOException(T("Linux_ImportChooseX64", "Choose a Windows x64 DLL for this family."));
        }
        contents.Position = 0;
        var temporary = Path.GetTempFileName();
        try
        {
            await using (var output = File.OpenWrite(temporary)) await contents.CopyToAsync(output, token).ConfigureAwait(false);
            var hash = DllScanner.ComputeMd5(temporary);
            if (expectedMd5 is not null && !hash.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase))
                throw new IOException(T("Linux_ImportModelChanged", "The selected model changed after inspection. Scan again before importing it."));
            if (model)
                family = NgxModelIdentity.Identify(DlssSwapper.Shared.PeVersionInfo.Read(temporary).ProductName);
            if (family is null) throw new IOException(T("Linux_ImportModelUnrecognized", "This is not a recognized NVIDIA DLSS model. Signature settings do not override model identity."));
            var known = catalog.FindByHash(family.Type, hash);
            var signature = known is { IsSignatureValid: true, IsImported: false }
                ? new DllSignatureResult(true, "Trusted catalog match.")
                : await (signatureVerifier ?? new DllSignatureVerifier(translations: translations)).VerifyAsync(temporary, token).ConfigureAwait(false);
            if (!signature.IsValid && !catalog.Policy.AllowUntrusted)
                throw new IOException(T("Linux_ImportNotPerformed", "{0} Import was not performed.", signature.Message));
            var metadata = DllImportMetadata.Read(temporary);
            var entry = known ?? new DllCatalogEntry(family.Type, metadata.Version, metadata.VersionNumber,
                hash, "", null, contents.Length, 0, signature.IsValid, metadata.IsDebug, InternalName: metadata.InternalName,
                FileDescription: metadata.Description, IsImported: true);
            if (entry.IsImported) entry = entry with
            {
                IsSignatureValid = signature.IsValid,
                Version = metadata.Version, VersionNumber = metadata.VersionNumber,
                IsDevFile = metadata.IsDebug, InternalName = metadata.InternalName, FileDescription = metadata.Description
            };
            token.ThrowIfCancellationRequested();
            if (entry.IsImported)
            {
                // Commit the recoverable record before publishing its payload. A failed state save
                // must leave the cache untouched; an interrupted copy can be retried by reimporting.
                library.UpdateState(state =>
                {
                    state.ImportedDlls ??= [];
                    state.ImportedDlls.RemoveAll(item => item.Type == entry.Type && item.Md5 == entry.Md5);
                    state.ImportedDlls.Add(entry);
                });
                catalog.AddImported(entry);
            }
            bool acquired;
            try { acquired = await cache.ImportAsync(entry, temporary, token).ConfigureAwait(false); }
            catch (Exception error) when (entry.IsImported && error is IOException or UnauthorizedAccessException)
            {
                throw new IOException(T("Linux_ImportRetry", "Import did not finish. Its Library record was saved; reimport this DLL to retry. {0}", error.Message), error);
            }
            return acquired ? T("Linux_ImportAcquired", "Imported {0} {1}. No game files changed.", family.DisplayName, entry.Version) : T("Linux_ImportAvailable", "Already available: {0} {1}.", family.DisplayName, entry.Version);
        }
        finally { File.Delete(temporary); }
    }
}
