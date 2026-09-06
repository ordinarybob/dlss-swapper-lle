namespace DlssSwapper.Linux.Cli.Core;

public static class DllRecordDetails
{
    public static string Describe(DllCatalogEntry entry, Translations? translations = null)
    {
        string T(string key, string fallback, params object?[] args) => translations?.Format(key, fallback, args) ?? string.Format(fallback, args);
        var lines = new List<string>();
        Add("Linux_DllDetailsFamily", "Family", DllTypes.Get(entry.Type).DisplayName);
        Add("Linux_DllDetailsVersion", "Version", entry.Version);
        Add("LibraryPage_DllRecordInfo_Label", "Label", entry.AdditionalLabel, optional: true);
        Add("LibraryPage_DllRecordInfo_InternalName", "Internal name", entry.InternalName, optional: true);
        Add("LibraryPage_DllRecordInfo_InternalNameExtra", "Additional internal name", entry.InternalNameExtra, optional: true);
        Add("LibraryPage_DllRecordInfo_FileDescription", "Description", entry.FileDescription, optional: true);
        Add("LibraryPage_DllRecordInfo_FileSize", "DLL size", T("Linux_DllDetailsBytes", "{0} bytes", entry.FileSize));
        Add("LibraryPage_DllRecordInfo_DownloadFileSize", "Download size", T("Linux_DllDetailsBytes", "{0} bytes", entry.ZipFileSize));
        Add("Linux_DllDetailsHash", "DLL MD5", entry.Md5);
        Add("Linux_DllDetailsArchiveHash", "Archive MD5", entry.ZipMd5);
        Add("Linux_DllDetailsSignature", "Catalog signature status", entry.IsSignatureValid
            ? T("Linux_DllDetailsValid", "valid") : T("Linux_DllDetailsInvalid", "not valid"));
        lines.Add(T("Linux_DllDetailsHashNotice", "MD5 identifies file contents; it does not establish signature authenticity."));
        return string.Join("\n", lines);
        void Add(string key, string label, string? value, bool optional = false)
        { if (!optional || !string.IsNullOrWhiteSpace(value)) lines.Add(T("Linux_DllDetailsLine", "{0}: {1}", T(key, label), value)); }
    }
}
