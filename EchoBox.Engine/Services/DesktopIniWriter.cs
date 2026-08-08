using System;
using System.IO;
using EchoBox.Engine.Interop;

namespace EchoBox.Engine.Services;

public class DesktopIniWriter
{
    private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;

    public bool ApplyFolderIcon(string folderPath, string iconFilePath)
    {
        if (!Directory.Exists(folderPath) || !File.Exists(iconFilePath))
        {
            return false;
        }

        try
        {
            string iniPath = Path.Combine(folderPath, "desktop.ini");

            // Reset existing desktop.ini attributes if present so we can overwrite
            if (File.Exists(iniPath))
            {
                NativeMethods.SetFileAttributesW(iniPath, 0); // Normal
            }

            // Write section and key using Win32 API or standard stream
            NativeMethods.WritePrivateProfileStringW(".ShellClassInfo", "IconResource", $"{iconFilePath},0", iniPath);
            NativeMethods.WritePrivateProfileStringW(".ShellClassInfo", "IconFile", iconFilePath, iniPath);
            NativeMethods.WritePrivateProfileStringW(".ShellClassInfo", "IconIndex", "0", iniPath);

            // Set desktop.ini to System + Hidden
            NativeMethods.SetFileAttributesW(iniPath, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);

            // Set directory to System / ReadOnly so Windows Explorer reads desktop.ini
            var folderAttrs = (uint)File.GetAttributes(folderPath);
            NativeMethods.SetFileAttributesW(folderPath, folderAttrs | FILE_ATTRIBUTE_READONLY | FILE_ATTRIBUTE_SYSTEM);

            NativeMethods.NotifyFolderUpdated(folderPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
