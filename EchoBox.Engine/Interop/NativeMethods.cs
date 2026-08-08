using System;
using System.Runtime.InteropServices;

namespace EchoBox.Engine.Interop;

public static class NativeMethods
{
    public const uint SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNE_UPDATEITEM = 0x00002000;
    public const uint SHFMT_OPT_FULL = 0x0001;
    public const uint SHCNF_IDLIST = 0x0000;
    public const uint SHCNF_PATHW = 0x0005;
    public const uint SHCNF_FLUSH = 0x1000;

    public const uint WM_SETTINGCHANGE = 0x001A;
    public const int HWND_BROADCAST = 0xffff;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern void SHChangeNotify(
        uint wEventId,
        uint uFlags,
        IntPtr dwItem1,
        IntPtr dwItem2);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WritePrivateProfileStringW(
        string lpAppName,
        string lpKeyName,
        string lpString,
        string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetFileAttributesW(
        string lpFileName,
        uint dwFileAttributes);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    public const uint SPI_SETDESKWALLPAPER = 0x0014;
    public const uint SPIF_UPDATEINIFILE = 0x01;
    public const uint SPIF_SENDCHANGE = 0x02;
    public const int COLOR_BACKGROUND = 1;
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        string? pvParam,
        uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetSysColors(
        int cElements,
        int[] lpaElements,
        uint[] lpaRgbValues);

    public static void RefreshShell()
    {
        // Notify Shell of icon/association changes and flush icon cache
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);

        // Broadcast environment change
        SendMessageTimeout(
            (IntPtr)HWND_BROADCAST,
            WM_SETTINGCHANGE,
            UIntPtr.Zero,
            "Environment",
            SMTO_ABORTIFHUNG,
            1000,
            out _);
    }

    public static void BroadcastThemeChange()
    {
        SendMessageTimeout(
            (IntPtr)HWND_BROADCAST,
            WM_SETTINGCHANGE,
            UIntPtr.Zero,
            "ImmersiveColorSet",
            SMTO_ABORTIFHUNG,
            1000,
            out _);
    }

    public static void NotifyFolderUpdated(string folderPath)
    {
        IntPtr pathPtr = Marshal.StringToHGlobalUni(folderPath);
        try
        {
            SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pathPtr, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeHGlobal(pathPtr);
        }
    }
}
