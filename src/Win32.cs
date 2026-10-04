using System;
using System.Runtime.InteropServices;

namespace DLSS_Swapper;

// From https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/Common/Win32.cs
internal static class Win32
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(IntPtr moduleName);

    public const int WM_ACTIVATE = 0x0006;
    public const int WA_ACTIVE = 0x01;
    public const int WA_INACTIVE = 0x00;

    public const int WM_SETICON = 0x0080;
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
}
