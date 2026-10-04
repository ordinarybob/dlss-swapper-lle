using System;
using System.Runtime.InteropServices;

namespace DLSS_Swapper;

// Based on https://docs.microsoft.com/en-us/windows/win32/seccrypto/example-c-program--verifying-the-signature-of-a-pe-file
// Interop reference: https://www.pinvoke.net/default.aspx/wintrust.winverifytrust

internal static class WinTrust
{

    internal const string WINTRUST_ACTION_GENERIC_VERIFY_V2 = "00AAC56B-CD44-11d0-8CC2-00C04FC295EE";

    internal enum WinVerifyTrustResult : uint
    {
        TRUST_E_NOSIGNATURE = 0x800B0100,

        TRUST_E_SUBJECT_FORM_UNKNOWN = 0x800B0003,

        TRUST_E_PROVIDER_UNKNOWN = 0x800B0001,

        TRUST_E_EXPLICIT_DISTRUST = 0x800B0111,

        ERROR_SUCCESS = 0x0,

        TRUST_E_SUBJECT_NOT_TRUSTED = 0x800B0004,

        CRYPT_E_SECURITY_SETTINGS = 0x80092026,

        CRYPT_E_FILE_ERROR = 0x80092003,
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern WinVerifyTrustResult WinVerifyTrust(
        [In] IntPtr hwnd,
        [In][MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
        [In] WinTrustData pWVTData
    );

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WinTrustFileInfo : IDisposable
    {
        public UInt32 cbStruct { get; private set; }                // = sizeof(WINTRUST_FILE_INFO)
        public IntPtr pcwszFilePath { get; private set; }           // required, file name to be verified
        public IntPtr hFile { get; private set; }                     // optional, open handle to FilePath
        public IntPtr pgKnownSubject { get; private set; }  // optional, subject type if it is known

        public WinTrustFileInfo(string filePath)
        {
            cbStruct = (UInt32)Marshal.SizeOf<WinTrustFileInfo>();
            pcwszFilePath = Marshal.StringToCoTaskMemUni(filePath);
            hFile = IntPtr.Zero;
            pgKnownSubject = IntPtr.Zero;
        }

        public void Dispose()
        {
            if (pcwszFilePath != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pcwszFilePath);
                pcwszFilePath = IntPtr.Zero;
            }
        }
    }

    internal enum WinTrustDataUIChoice : uint
    {
        All = 1,
        None = 2,
        NoBad = 3,
        NoGood = 4
    }

    internal enum WinTrustDataRevocationChecks : uint
    {
        None = 0x00000000,
        WholeChain = 0x00000001
    }

    internal enum WinTrustDataChoice : uint
    {
        File = 1,
        Catalog = 2,
        Blob = 3,
        Signer = 4,
        Certificate = 5
    }

    internal enum WinTrustDataStateAction : uint
    {
        Ignore = 0x00000000,
        Verify = 0x00000001,
        Close = 0x00000002,
        AutoCache = 0x00000003,
        AutoCacheFlush = 0x00000004
    }

    [FlagsAttribute]
    internal enum WinTrustDataProvFlags : uint
    {
        ProvFlagsMask = 0x0000FFFF,
        UseIe4TrustFlag = 0x00000001,
        NoIe4ChainFlag = 0x00000002,
        NoPolicyUsageFlag = 0x00000004,
        RevocationCheckNone = 0x00000010,
        RevocationCheckEndCert = 0x00000020,
        RevocationCheckChain = 0x00000040,
        RevocationCheckChainExcludeRoot = 0x00000080,
        SaferFlag = 0x00000100,
        HashOnlyFlag = 0x00000200,
        UseDefaultOsverCheck = 0x00000400,
        LifetimeSigningFlag = 0x00000800,
        CacheOnlyUrlRetrieval = 0x00001000, // affects CRL retrieval and AIA retrieval
        DisableMD2andMD4 = 0x00002000,
        MarkOfTheWeb = 0x00004000, // Mark-Of-The-Web
        CodeIntegrityDriverMode = 0x00008000,// Code Integrity driver mode
    }

    internal enum WinTrustDataUIContext : uint
    {
        Execute = 0,
        Install = 1
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WinTrustData : IDisposable
    {
        public UInt32 cbStruct { get; set; }                                        // = sizeof(WINTRUST_DATA)
        public IntPtr pPolicyCallbackData { get; set; }                             // optional: used to pass data between the app and policy
        public IntPtr pSIPClientData { get; set; }                                  // optional: used to pass data between the app and SIP.
        public WinTrustDataUIChoice dwUIChoice { get; set; }                        // required: UI choice.
        public WinTrustDataRevocationChecks fdwRevocationChecks { get; set; }       // required: certificate revocation check options
        public WinTrustDataChoice dwUnionChoice { get; set; }                       // required: which structure is being passed in?
        public IntPtr pFile { get; set; }                                           // individual file
        public WinTrustDataStateAction dwStateAction { get; set; }                  // optional (Catalog File Processing)
        public IntPtr hWVTStateData { get; set; }                                   // optional (Catalog File Processing)
        public string? pwszURLReference { get; set; }                               // optional: (future) used to determine zone.
        public WinTrustDataProvFlags dwProvFlags { get; set; }
        public WinTrustDataUIContext dwUIContext { get; set; }

        // constructor for silent WinTrustDataChoice.File check
        public WinTrustData(WinTrustFileInfo fileInfo)
        {
            cbStruct = (UInt32)Marshal.SizeOf<WinTrustData>();
            pPolicyCallbackData = IntPtr.Zero;
            pSIPClientData = IntPtr.Zero;
            dwUIChoice = WinTrustDataUIChoice.None;
            fdwRevocationChecks = WinTrustDataRevocationChecks.None;
            dwUnionChoice = WinTrustDataChoice.File;
            pFile = IntPtr.Zero;
            dwStateAction = WinTrustDataStateAction.Ignore;
            hWVTStateData = IntPtr.Zero;
            pwszURLReference = null;
            dwProvFlags = WinTrustDataProvFlags.RevocationCheckChainExcludeRoot;
            dwUIContext = WinTrustDataUIContext.Execute;

            // On Win7SP1+, don't allow MD2 or MD4 signatures
            if ((Environment.OSVersion.Version.Major > 6) ||
                ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor > 1)) ||
                ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor == 1) && !string.IsNullOrEmpty(Environment.OSVersion.ServicePack)))
            {
                dwProvFlags |= WinTrustDataProvFlags.DisableMD2andMD4;
            }

            WinTrustFileInfo wtfiData = fileInfo;
            pFile = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
            Marshal.StructureToPtr(wtfiData, pFile, false);
        }

        public void Dispose()
        {
            if (pFile != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pFile);
                pFile = IntPtr.Zero;
            }
        }
    }

    public static bool VerifyEmbeddedSignature(string fileName)
    {
        WinVerifyTrustResult lStatus;
        uint dwLastError;

        var fileData = default(WinTrustFileInfo);
        var winTrustData = default(WinTrustData);
        var policyGuid = new Guid(WINTRUST_ACTION_GENERIC_VERIFY_V2);
        var trustStateOpened = false;

        var validSignature = false;
        try
        {
            fileData = new WinTrustFileInfo(fileName);

            winTrustData = new WinTrustData(fileData)
            {
                cbStruct = (UInt32)Marshal.SizeOf<WinTrustData>(),

                pPolicyCallbackData = IntPtr.Zero,

                pSIPClientData = IntPtr.Zero,

                dwUIChoice = WinTrustDataUIChoice.None,

                fdwRevocationChecks = WinTrustDataRevocationChecks.None,

                dwUnionChoice = WinTrustDataChoice.File,

                dwStateAction = WinTrustDataStateAction.Verify,

                hWVTStateData = IntPtr.Zero,

                pwszURLReference = null,

                dwUIContext = 0,

            };

            lStatus = WinVerifyTrust(IntPtr.Zero, policyGuid, winTrustData);
            trustStateOpened = true;

            switch (lStatus)
            {
                case WinVerifyTrustResult.ERROR_SUCCESS:
                    validSignature = true;
                    Logger.Info($"The file \"{fileName}\" is signed and the signature was verified.");
                    break;

                case WinVerifyTrustResult.TRUST_E_NOSIGNATURE:

                    dwLastError = (uint)Marshal.GetLastWin32Error();
                    if ((uint)WinVerifyTrustResult.TRUST_E_NOSIGNATURE == dwLastError ||
                        (uint)WinVerifyTrustResult.TRUST_E_SUBJECT_FORM_UNKNOWN == dwLastError ||
                        (uint)WinVerifyTrustResult.TRUST_E_PROVIDER_UNKNOWN == dwLastError)
                    {
                        Logger.Warning($"The file \"{fileName}\" is not signed.");
                    }
                    else
                    {
                        Logger.Error($"An unknown error occurred trying to verify the signature of the \"{fileName}\" file.");
                    }

                    break;

                case WinVerifyTrustResult.TRUST_E_EXPLICIT_DISTRUST:
                    Logger.Warning("The signature is present, but specifically disallowed.");
                    break;

                case WinVerifyTrustResult.TRUST_E_SUBJECT_NOT_TRUSTED:
                    Logger.Error("The signature is present, but not trusted.");
                    break;

                case WinVerifyTrustResult.CRYPT_E_SECURITY_SETTINGS:
                    Logger.Error("CRYPT_E_SECURITY_SETTINGS - The hash representing the subject or the publisher wasn't explicitly trusted by the admin and admin policy has disabled user trust. No signature, publisher or timestamp errors.");
                    break;

                case WinVerifyTrustResult.CRYPT_E_FILE_ERROR:
                    Logger.Error("CRYPT_E_FILE_ERROR - An error occurred while reading or writing to a file.");
                    break;

                default:
                    Logger.Error($"Error is: 0x{lStatus}.");
                    break;
            }

        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
        finally
        {
            if (trustStateOpened)
            {
                // Any hWVTStateData must be released by a call with close.
                winTrustData.dwStateAction = WinTrustDataStateAction.Close;
                _ = WinVerifyTrust(IntPtr.Zero, policyGuid, winTrustData);
            }

            winTrustData.Dispose();
            fileData.Dispose();
        }

        return validSignature;
    }
}
