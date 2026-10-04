using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DLSS_Swapper.Helpers.FSR31;

internal class FSR31Helper
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate FfxApiReturnCodes ffxQueryDelegate(IntPtr context, ref QueryDescGetVersions header);

    public static List<string?> GetVersions(string dllPath)
    {
        if (Path.Exists(dllPath) == false)
        {
            return new List<string?>();
        }
        Logger.Info($"AMDFidelityFXAPI - Loading {dllPath}");
        var hModule = LoadLibrary(dllPath);
        if (hModule == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to load DLL.");
        }

        var versionQuery = new QueryDescGetVersions();
        try
        {
            var pAddressOfFunctionToCall = GetProcAddress(hModule, "ffxQuery");
            if (pAddressOfFunctionToCall == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get function address.");
            }

            var ffxQuery = Marshal.GetDelegateForFunctionPointer<ffxQueryDelegate>(pAddressOfFunctionToCall);

            versionQuery.createDescType = FxxConsts.FFX_API_CREATE_CONTEXT_DESC_TYPE_UPSCALE;
            versionQuery.device = IntPtr.Zero;

            ulong versionCount = 0;
            versionQuery.outputCount = Marshal.AllocHGlobal(sizeof(UInt64));
            Marshal.WriteInt64(versionQuery.outputCount, 0);

            Logger.Info("AMDFidelityFXAPI - Reading version count");
            var returnCode = ffxQuery(IntPtr.Zero, ref versionQuery);
            Logger.Info($"AMDFidelityFXAPI - returnCode: {returnCode}");

            if (returnCode != FfxApiReturnCodes.FFX_API_RETURN_OK)
            {
                throw new InvalidOperationException($"Failed to get version count. Return code: {returnCode}");
            }

            versionCount = (ulong)Marshal.ReadInt64(versionQuery.outputCount);
            Logger.Info($"AMDFidelityFXAPI - versionCount: {versionCount}");

            if (versionCount > 0)
            {
                if (versionCount > int.MaxValue)
                {
                    throw new InvalidOperationException($"The DLL reported an invalid version count: {versionCount}.");
                }

                var versionCountInt = (int)versionCount;

                var versionNames = new List<string?>(versionCountInt);
                var versionIds = new List<ulong>(versionCountInt);

                try
                {
                    for (var i = 0; i < versionCountInt; i++)
                    {
                        versionNames.Add(null);
                        versionIds.Add(0);
                    }

                    versionQuery.versionIds = Marshal.AllocHGlobal(checked(sizeof(UInt64) * versionCountInt));
                    versionQuery.versionNames = Marshal.AllocHGlobal(checked(IntPtr.Size * versionCountInt));

                    for (var i = 0; i < versionCountInt; i++)
                    {
                        Marshal.WriteInt64(versionQuery.versionIds, i * sizeof(UInt64), 0L);

                        Marshal.WriteIntPtr(versionQuery.versionNames, i * IntPtr.Size, IntPtr.Zero);
                    }

                    returnCode = ffxQuery(IntPtr.Zero, ref versionQuery);
                    Logger.Info($"AMDFidelityFXAPI - returnCode: {returnCode}");

                    if (returnCode != FfxApiReturnCodes.FFX_API_RETURN_OK)
                    {
                        throw new InvalidOperationException($"Failed to get version count. Return code: {returnCode}");
                    }

                    for (var i = 0; i < versionCountInt; i++)
                    {
                        versionIds[i] = (ulong)Marshal.ReadInt64(versionQuery.versionIds, i * sizeof(UInt64));
                        versionNames[i] = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(versionQuery.versionNames, i * IntPtr.Size));
                    }

                    Logger.Info("AMDFidelityFXAPI - Version Names and IDs:");

                    for (var i = 0; i < versionCountInt; i++)
                    {
                        var major = (versionIds[i] >> 22) & 0x3FF;
                        var minor = (versionIds[i] >> 12) & 0x3FF;
                        var patch = versionIds[i] & 0xFFF;

                        Logger.Info($"ID: {versionIds[i]}, {major}.{minor}.{patch}, Name: {versionNames[i]}");
                    }

                    return versionNames;
                }
                finally
                {
                    Marshal.FreeHGlobal(versionQuery.versionIds);
                    Marshal.FreeHGlobal(versionQuery.versionNames);
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, "AMDFidelityFXAPI");
        }
        finally
        {
            if (versionQuery.outputCount != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(versionQuery.outputCount);
            }
            _ = FreeLibrary(hModule);
        }

        return new List<string?>();
    }

    public static string GetLatestVersion(string dllPath)
    {
        // Version IDs are expected in newest-first order.
        var versions = GetVersions(dllPath);
        foreach (var version in versions)
        {
            if (string.IsNullOrWhiteSpace(version) == false)
            {
                return version;
            }
        }
        return string.Empty;
    }
}
